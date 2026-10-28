using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

// 池化对象回调，复用时在这里重播表现
public interface IPooled_Effect
{
    float Pool_Lifetime { get; }
    void On_Pool_Spawn();
    void On_Pool_Despawn();
}

// 统一对象池，生成与回收都走这里
public class Object_Pool_Manager : MonoBehaviour
{
    const int Max_Idle_Per_Prefab = 64;     // 每种预制体最多缓存数量
    const float Default_Life = 1f;          // 无法推断寿命时的兜底

    static Object_Pool_Manager instance;

    static Object_Pool_Manager Instance
    {
        get
        {
            if (instance != null) return instance;

            Object_Pool_Manager found = FindObjectOfType<Object_Pool_Manager>();
            if (found != null)
            {
                instance = found;
                return instance;
            }

            GameObject go = new GameObject("[Object_Pool]");
            instance = go.AddComponent<Object_Pool_Manager>();
            return instance;
        }
    }

    // 缓存实例和组件查询结果
    class Pool_Node
    {
        public GameObject Go;
        public IPooled_Effect Effect;
        public Pool Owner;
    }

    // 同一种预制体对应一个池
    class Pool
    {
        public GameObject Prefab;
        public ObjectPool<Pool_Node> BuiltinPool;
        public float DefaultLife = -1f;
        public bool Released;
    }

    // 活跃实例的寿命记录，节点循环使用
    class Active_Item
    {
        public Pool_Node Item;
        public double EndTime;
        public int ListIndex;
    }

    readonly Dictionary<GameObject, Pool> pools = new Dictionary<GameObject, Pool>();
    readonly Dictionary<GameObject, Active_Item> activeMap = new Dictionary<GameObject, Active_Item>();
    readonly HashSet<GameObject> pooledObjects = new HashSet<GameObject>();
    readonly List<Active_Item> activeList = new List<Active_Item>();
    readonly Stack<Active_Item> activeCache = new Stack<Active_Item>();

    Transform poolRoot;
    bool shuttingDown;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        GameObject root = new GameObject("Pooled_Objects");
        root.transform.SetParent(transform, false);
        poolRoot = root.transform;
    }

    void OnDestroy()
    {
        shuttingDown = true;
        if (instance == this) instance = null;
    }

    void Update()
    {
        double now = Time.timeAsDouble;

        // 倒序遍历，自动回收超过寿命的实例
        for (int i = activeList.Count - 1; i >= 0; i--)
        {
            Active_Item active = activeList[i];
            if (active == null || active.Item == null)
            {
                Remove_Active_At(i);
                continue;
            }

            if (active.Item.Go == null)
            {
                activeMap.Remove(active.Item.Go);
                pooledObjects.Remove(active.Item.Go);
                Remove_Active_At(i);
                continue;
            }

            if (active.EndTime <= now) Despawn_Internal(active.Item.Go);
        }
    }

    // 生成一个池化对象，life 小于等于 0 时使用预制体自身寿命
    public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, float life = -1f)
    {
        if (prefab == null) return null;

        Object_Pool_Manager manager = Instance;
        if (manager == null || manager.shuttingDown) return null;

        return manager.Spawn_Internal(prefab, position, rotation, life);
    }

    // 回收对象，非池化对象直接销毁，避免误用造成泄漏
    public static void Despawn(GameObject target)
    {
        if (target == null) return;
        if (instance == null)
        {
            Destroy(target);
            return;
        }

        instance.Despawn_Internal(target);
    }

    // 预热，提前建好实例，避免第一次生成时集中实例化
    public static void Prewarm(GameObject prefab, int count)
    {
        if (prefab == null || count <= 0) return;

        Object_Pool_Manager manager = Instance;
        if (manager == null || manager.shuttingDown) return;

        Pool pool = manager.Get_Pool(prefab);
        manager.Prewarm_Pool(pool, count);
    }

    // 是否由池管理，空闲实例也要识别，避免预热对象误走自身销毁逻辑
    public static bool Is_Pooled(GameObject target)
        => target != null && instance != null && instance.pooledObjects.Contains(target);

    // 清空某种预制体的缓存，已激活的实例归还时销毁
    public static void Release_Pool(GameObject prefab)
    {
        if (prefab == null || instance == null) return;

        Pool pool;
        if (!instance.pools.TryGetValue(prefab, out pool)) return;

        instance.pools.Remove(prefab);
        pool.Released = true;
        pool.BuiltinPool.Clear();
    }

    GameObject Spawn_Internal(GameObject prefab, Vector3 position, Quaternion rotation, float life)
    {
        Pool pool = Get_Pool(prefab);
        Pool_Node item = pool.BuiltinPool.Get();
        if (item == null || item.Go == null) return null;

        float duration = life > 0f ? life : pool.DefaultLife;
        if (duration <= 0f) duration = Default_Life;

        item.Go.transform.SetParent(poolRoot, false);
        item.Go.transform.SetPositionAndRotation(position, rotation);

        // 先登记再激活，保证 Start 里能识别池化对象
        Active_Item active = Get_Active_Item();
        active.Item = item;
        active.EndTime = Time.timeAsDouble + duration;
        active.ListIndex = activeList.Count;
        activeList.Add(active);
        activeMap[item.Go] = active;

        item.Go.SetActive(true);
        item.Effect?.On_Pool_Spawn();

        return item.Go;
    }

    void Despawn_Internal(GameObject target)
    {
        Active_Item active;
        if (!activeMap.TryGetValue(target, out active))
        {
            // 已经回到池里的对象不重复处理
            if (!pooledObjects.Contains(target)) Destroy(target);
            return;
        }

        Pool_Node item = active.Item;
        activeMap.Remove(target);
        Remove_Active_At(active.ListIndex);

        if (item == null)
        {
            Destroy(target);
            return;
        }

        if (item.Go == null)
        {
            pooledObjects.Remove(target);
            return;
        }

        // 池已释放时不再放回，直接销毁
        if (item.Owner == null || item.Owner.Released)
        {
            Destroy_Node(item);
            return;
        }

        // Release 会触发 Release_Node，统一停止表现并放回 ObjectPool
        item.Owner.BuiltinPool.Release(item);
    }

    Pool Get_Pool(GameObject prefab)
    {
        Pool pool;
        if (pools.TryGetValue(prefab, out pool)) return pool;

        pool = new Pool
        {
            Prefab = prefab,
            DefaultLife = Calculate_Life(prefab)
        };
        pool.BuiltinPool = Create_Builtin_Pool(pool);
        pools.Add(prefab, pool);
        return pool;
    }

    // 预制体没有显式寿命时，从池化回调或粒子系统时长推断一次
    static float Calculate_Life(GameObject prefab)
    {
        IPooled_Effect effect = prefab.GetComponent<IPooled_Effect>();
        if (effect != null && effect.Pool_Lifetime > 0f) return effect.Pool_Lifetime;

        float maxLife = 0f;
        ParticleSystem[] systems = prefab.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem.MainModule main = systems[i].main;
            float systemLife = main.duration + main.startLifetime.constantMax;
            if (systemLife > maxLife) maxLife = systemLife;
        }

        return maxLife > 0f ? maxLife + 0.1f : Default_Life;
    }

    ObjectPool<Pool_Node> Create_Builtin_Pool(Pool pool)
    {
        // 编辑器和开发包开启重复回收检查，正式包关闭以减少检查开销
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        const bool collectionCheck = true;
#else
        const bool collectionCheck = false;
#endif

        return new ObjectPool<Pool_Node>(
            createFunc: () => Create_Node(pool),
            actionOnGet: null,
            actionOnRelease: Release_Node,
            actionOnDestroy: Destroy_Node,
            collectionCheck: collectionCheck,
            defaultCapacity: 12,
            maxSize: Max_Idle_Per_Prefab);
    }

    void Prewarm_Pool(Pool pool, int count)
    {
        count = Mathf.Clamp(count, 0, Max_Idle_Per_Prefab);
        if (count <= 0 || pool.BuiltinPool.CountInactive >= count) return;

        // ObjectPool 没有直接预热 API，先全部取出并持有，再统一放回
        Pool_Node[] temp = new Pool_Node[count];
        for (int i = 0; i < count; i++)
            temp[i] = pool.BuiltinPool.Get();

        for (int i = 0; i < count; i++)
            pool.BuiltinPool.Release(temp[i]);
    }

    Pool_Node Create_Node(Pool pool)
    {
        GameObject go = Instantiate(pool.Prefab);
        go.name = pool.Prefab.name;
        go.transform.SetParent(poolRoot, false);
        go.SetActive(false);

        Pool_Node item = new Pool_Node
        {
            Go = go,
            Effect = go.GetComponent<IPooled_Effect>(),
            Owner = pool
        };

        pooledObjects.Add(go);
        return item;
    }

    void Release_Node(Pool_Node node)
    {
        if (node == null || node.Go == null) return;

        node.Effect?.On_Pool_Despawn();
        node.Go.SetActive(false);
        node.Go.transform.SetParent(poolRoot, false);
    }

    void Destroy_Node(Pool_Node node)
    {
        if (node == null || node.Go == null) return;

        pooledObjects.Remove(node.Go);
        Destroy(node.Go);
    }

    Active_Item Get_Active_Item()
        => activeCache.Count > 0 ? activeCache.Pop() : new Active_Item();

    void Release_Active_Item(Active_Item active)
    {
        active.Item = null;
        active.EndTime = 0d;
        active.ListIndex = -1;
        activeCache.Push(active);
    }

    // 交换末尾元素后移除，避免数组搬移
    void Remove_Active_At(int index)
    {
        if (index < 0 || index >= activeList.Count) return;

        Active_Item remove = activeList[index];
        int lastIndex = activeList.Count - 1;
        Active_Item last = activeList[lastIndex];
        activeList[index] = last;
        last.ListIndex = index;
        activeList.RemoveAt(lastIndex);

        Release_Active_Item(remove);
    }
}
