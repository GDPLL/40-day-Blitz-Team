using UnityEngine;
using UnityEngine.UI;

// 本机弹夹与子弹表现，由 UI_Player 驱动
public class UI_Magazine : MonoBehaviour
{
    const int Magazine_Max = 6;     // 弹夹显示上限
    const int Bullet_Max = 32;      // 子弹动画池大小

    [Header("弹夹")]
    public float magazineHeight = 72f;      // 弹夹显示高度，像素
    public float magazineGap = 5f;          // 弹夹间距，像素
    public Vector2 panelOffset = new Vector2(22f, 112f);    // 面板左下位置
    public float activeAlpha = 1f;          // 在用弹夹透明度
    public float idleAlpha = 0.28f;         // 备用弹夹透明度
    public float moveSmooth = 14f;          // 顶替移动速度
    public float fadeTime = 0.18f;          // 透明度过渡时长，秒
    public Color activeColor = new Color(0.93f, 0.97f, 0.90f, 1f);  // 弹夹剪影色

    [Header("换弹")]
    public float discardLife = 0.45f;       // 丢弃弹夹时长，秒
    public float discardSpin = -260f;       // 丢弃旋转速度，度每秒
    public Vector2 discardVelocity = new Vector2(-90f, 20f);    // 丢弃初速度
    public float discardGravity = -900f;    // 丢弃重力

    [Header("射击")]
    public float bulletLife = 1.6f;         // 子弹最长存活，秒
    public Vector2 bulletVelocity = new Vector2(55f, 230f);     // 子弹初速度
    public float bulletGravity = -700f;     // 子弹重力
    public float bulletSpin = -360f;        // 子弹自转速度，度每秒
    public float bulletWidth = 46f;         // 子弹显示宽度，像素
    public float bulletOffscreenMargin = 24f;   // 掉出屏幕后的额外余量，像素
    public Color bulletColor = new Color(0.98f, 0.82f, 0.25f, 1f);  // 子弹剪影色

    // 一个弹夹的显示状态
    class Magazine_View
    {
        public Image baseImage;         // 半透明底图
        public Image fillImage;         // 剩余子弹的实心层
        public RectTransform rect;      // 弹夹容器
        public Vector2 targetPos;       // 目标位置
        public float targetAlpha;       // 目标透明度
        public float alpha;             // 当前透明度
        public bool discarding;         // 正在丢弃
        public Vector2 discardPos;      // 丢弃位置
        public Vector2 discardVel;      // 丢弃速度
        public float discardAngle;      // 丢弃角度
        public float discardTimer;      // 丢弃剩余时长
    }

    // 一个子弹动画的显示状态
    class Bullet_View
    {
        public Image image;             // 子弹图
        public RectTransform rect;      // 子弹位置
        public Vector2 pos;             // 当前位置
        public Vector2 vel;             // 当前速度
        public float angle;             // 当前角度
        public float timer;             // 剩余时长
    }

    Magazine_View[] magazineViews = new Magazine_View[Magazine_Max];   // 全部弹夹
    Magazine_View[] activeViews = new Magazine_View[Magazine_Max];     // 未丢弃弹夹
    Bullet_View[] bulletViews = new Bullet_View[Bullet_Max];           // 子弹池

    RectTransform root;             // 弹夹面板
    RectTransform bulletRoot;       // 子弹层，盖在状态条上方
    Sprite magazineSprite;          // 弹夹图
    Sprite bulletSprite;            // 子弹图
    float magazineWidth;            // 弹夹显示宽度
    int activeCount;                // 未丢弃弹夹数
    int lastAmmo = -1;              // 上次弹药数
    int lastMagazine = -1;          // 上次弹夹数
    int bulletCursor;               // 子弹池轮转
    bool isReady;                   // 引用就绪

    // 初始化，建弹夹与子弹对象池
    public void UI_Magazine_Init()
    {
        if (isReady) return;

        Texture2D magazineTex = Resources.Load<Texture2D>("弹夹/弹夹");
        Texture2D bulletTex = Resources.Load<Texture2D>("弹夹/子弹");
        if (magazineTex == null || bulletTex == null)
        {
            Debug.LogError("UI_Magazine|UI_Magazine_Init|弹夹贴图缺失");
            return;
        }

        Shader shader = Resources.Load<Shader>("UI_Body");
        if (shader == null) shader = Shader.Find("UI/Body");
        if (shader == null)
        {
            Debug.LogError("UI_Magazine|UI_Magazine_Init|未找到 UI_Body 着色器");
            return;
        }

        magazineSprite = Sprite_From(magazineTex);
        bulletSprite = Sprite_From(bulletTex);
        magazineWidth = magazineHeight * magazineTex.width / magazineTex.height;

        root = New_Rect("弹夹", transform);
        root.anchorMin = new Vector2(0f, 0f);
        root.anchorMax = new Vector2(0f, 0f);
        root.pivot = new Vector2(0f, 0f);
        root.anchoredPosition = panelOffset;
        root.sizeDelta = new Vector2(
            magazineWidth * Magazine_Max + magazineGap * (Magazine_Max - 1),
            magazineHeight);

        // 子弹单独一层，避免下坠时被弹药与血量条挡住
        bulletRoot = New_Rect("子弹层", transform);
        bulletRoot.anchorMin = new Vector2(0f, 0f);
        bulletRoot.anchorMax = new Vector2(0f, 0f);
        bulletRoot.pivot = new Vector2(0f, 0f);
        bulletRoot.anchoredPosition = panelOffset;
        bulletRoot.sizeDelta = root.sizeDelta;

        // 弹夹图，每个弹夹由半透明底图和实心填充层组成
        for (int i = 0; i < Magazine_Max; i++)
        {
            RectTransform slot = New_Rect($"弹夹{i + 1}", root);
            slot.anchorMin = new Vector2(0f, 0f);
            slot.anchorMax = new Vector2(0f, 0f);
            slot.pivot = new Vector2(0.5f, 0f);
            slot.sizeDelta = new Vector2(magazineWidth, magazineHeight);
            slot.anchoredPosition = Slot_Pos(i);

            Image baseImage = New_Image("底图", magazineSprite,
                new Vector2(magazineWidth, magazineHeight), new Vector2(0.5f, 0f), slot);
            baseImage.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            baseImage.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            baseImage.rectTransform.anchoredPosition = Vector2.zero;
            baseImage.material = new Material(shader);
            baseImage.enabled = false;

            Image fillImage = New_Image("实心", magazineSprite,
                new Vector2(magazineWidth, magazineHeight), new Vector2(0.5f, 0f), slot);
            fillImage.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            fillImage.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            fillImage.rectTransform.anchoredPosition = Vector2.zero;
            fillImage.material = new Material(shader);
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Vertical;
            fillImage.fillOrigin = (int)Image.OriginVertical.Bottom;
            fillImage.fillAmount = 1f;
            fillImage.enabled = false;

            magazineViews[i] = new Magazine_View
            {
                baseImage = baseImage,
                fillImage = fillImage,
                rect = slot,
                alpha = idleAlpha
            };
            Set_Color(baseImage, activeColor, idleAlpha);
            Set_Color(fillImage, activeColor, idleAlpha);
        }

        // 子弹图池
        float bulletHeight = bulletWidth * bulletTex.height / bulletTex.width;
        for (int i = 0; i < Bullet_Max; i++)
        {
            Image image = New_Image($"子弹{i + 1}", bulletSprite,
                new Vector2(bulletWidth, bulletHeight), new Vector2(0.5f, 0.5f), bulletRoot);
            image.material = new Material(shader);
            image.enabled = false;

            bulletViews[i] = new Bullet_View
            {
                image = image,
                rect = image.rectTransform
            };
            Set_Color(image, bulletColor, 0f);
        }

        // 弹夹在状态条之后，子弹层在状态条之前、幕布之后
        root.SetSiblingIndex(Mathf.Clamp(2, 0, transform.childCount - 1));
        bulletRoot.SetSiblingIndex(Mathf.Clamp(5, 0, transform.childCount - 1));
        isReady = true;

        Debug.Log("UI_Magazine|UI_Magazine_Init|完成初始化");
    }

    // 按弹药、弹夹数与换弹状态刷新
    public void UI_Magazine_Show(int ammo, int maxAmmo, int magazineCount, bool isReload)
    {
        if (!isReady) return;

        magazineCount = Mathf.Clamp(magazineCount, 0, Magazine_Max);

        // 首次显示或数量回升，直接重建
        if (lastMagazine < 0 || magazineCount > lastMagazine)
        {
            Rebuild_Active(magazineCount);
        }
        else if (magazineCount < lastMagazine)
        {
            // 丢弃当前弹夹，剩余弹夹前移
            int discardCount = lastMagazine - magazineCount;
            for (int i = 0; i < discardCount && activeCount > 0; i++)
            {
                Magazine_View lost = activeViews[0];
                for (int j = 0; j < activeCount - 1; j++)
                    activeViews[j] = activeViews[j + 1];

                activeViews[activeCount - 1] = null;
                activeCount--;
                Discard_Start(lost);
            }
        }

        // 刷新目标位置与透明度
        for (int i = 0; i < activeCount; i++)
        {
            activeViews[i].targetPos = Slot_Pos(i);
            activeViews[i].targetAlpha = i == 0 ? activeAlpha : idleAlpha;
            activeViews[i].fillImage.enabled = i == 0;
        }

        // 当前弹夹从底部往上保留实心，打掉的部分露出半透明底图
        Magazine_Fill_Update(ammo, maxAmmo, isReload);

        // 弹药下降时弹出子弹，一帧最多补两发
        if (lastAmmo >= 0 && ammo < lastAmmo)
        {
            int shotCount = Mathf.Min(lastAmmo - ammo, 2);
            for (int i = 0; i < shotCount; i++) Bullet_Spawn();
        }

        lastAmmo = ammo;
        lastMagazine = magazineCount;

        float dt = Time.deltaTime;
        Magazine_Update(dt);
        Bullet_Update(dt);
    }

    // 更新当前弹夹的实心填充比例
    void Magazine_Fill_Update(int ammo, int maxAmmo, bool isReload)
    {
        if (activeCount <= 0) return;

        // 换弹期间新弹夹按满弹显示，换弹取消后回到原弹药比例
        float fill = isReload
            ? 1f
            : (maxAmmo > 0 ? Mathf.Clamp01((float)ammo / maxAmmo) : 0f);

        Magazine_View active = activeViews[0];
        active.fillImage.fillAmount = fill;
        active.fillImage.enabled = fill > 0.001f;
        if (active.fillImage.enabled)
            Set_Color(active.fillImage, activeColor, active.alpha);
    }

    // 按数量重建弹夹显示，用于首次显示与数量回升
    void Rebuild_Active(int count)
    {
        activeCount = count;

        for (int i = 0; i < Magazine_Max; i++)
        {
            Magazine_View view = magazineViews[i];
            view.discarding = false;
            view.targetPos = Slot_Pos(i);
            view.targetAlpha = i < count
                ? (i == 0 ? activeAlpha : idleAlpha)
                : idleAlpha;
            view.alpha = view.targetAlpha;
            view.rect.anchoredPosition = view.targetPos;
            view.rect.localRotation = Quaternion.identity;
            view.baseImage.enabled = i < count;
            view.fillImage.enabled = i == 0 && count > 0;
            Set_Color(view.baseImage, activeColor, idleAlpha);
            Set_Color(view.fillImage, activeColor, view.alpha);

            activeViews[i] = i < count ? view : null;
        }
    }

    // 弹夹平滑顶替与透明度过渡
    void Magazine_Update(float dt)
    {
        float move = 1f - Mathf.Exp(-moveSmooth * dt);
        float fade = fadeTime > 0f ? Mathf.Clamp01(dt / fadeTime) : 1f;

        for (int i = 0; i < activeCount; i++)
        {
            Magazine_View view = activeViews[i];
            view.rect.anchoredPosition = Vector2.Lerp(
                view.rect.anchoredPosition, view.targetPos, move);
            view.alpha = Mathf.Lerp(view.alpha, view.targetAlpha, fade);
            Set_Color(view.baseImage, activeColor, idleAlpha);
            if (i == 0) Set_Color(view.fillImage, activeColor, view.alpha);
        }

        for (int i = 0; i < Magazine_Max; i++)
        {
            if (magazineViews[i].discarding)
                Discard_Update(magazineViews[i], dt);
        }
    }

    // 开始丢弃当前弹夹
    void Discard_Start(Magazine_View view)
    {
        view.discarding = true;
        view.discardTimer = discardLife;
        view.discardPos = view.rect.anchoredPosition;
        view.discardVel = discardVelocity;
        view.discardAngle = 0f;
        view.baseImage.enabled = true;
        view.fillImage.enabled = view.fillImage.fillAmount > 0.001f;
        view.rect.localRotation = Quaternion.identity;
        Set_Color(view.baseImage, activeColor, idleAlpha);
        Set_Color(view.fillImage, activeColor, activeAlpha);
    }

    // 丢弃弹夹旋转下落并淡出
    void Discard_Update(Magazine_View view, float dt)
    {
        view.discardTimer -= dt;
        view.discardVel.y += discardGravity * dt;
        view.discardPos += view.discardVel * dt;
        view.discardAngle += discardSpin * dt;

        view.rect.anchoredPosition = view.discardPos;
        view.rect.localRotation = Quaternion.Euler(0f, 0f, view.discardAngle);

        float progress = 1f - Mathf.Clamp01(view.discardTimer / discardLife);
        float fade = 1f - progress;
        Set_Color(view.baseImage, activeColor, idleAlpha * fade);
        Set_Color(view.fillImage, activeColor, activeAlpha * fade);

        if (view.discardTimer <= 0f)
        {
            view.discarding = false;
            view.baseImage.enabled = false;
            view.fillImage.enabled = false;
        }
    }

    // 从当前弹夹顶部弹出一发子弹
    void Bullet_Spawn()
    {
        if (activeCount <= 0) return;

        Bullet_View view = null;
        for (int i = 0; i < Bullet_Max; i++)
        {
            if (bulletViews[i].timer <= 0f)
            {
                view = bulletViews[i];
                break;
            }
        }

        // 全部在飞时轮转复用最早的一发
        if (view == null)
        {
            view = bulletViews[bulletCursor];
            bulletCursor = (bulletCursor + 1) % Bullet_Max;
        }

        Vector2 start = activeViews[0].rect.anchoredPosition
            + new Vector2(magazineWidth * 0.15f, magazineHeight * 0.92f);

        view.timer = bulletLife;
        view.pos = start;
        view.vel = bulletVelocity;
        view.angle = Mathf.Atan2(bulletVelocity.y, bulletVelocity.x) * Mathf.Rad2Deg;
        view.rect.anchoredPosition = view.pos;
        view.rect.localRotation = Quaternion.Euler(0f, 0f, view.angle);
        view.image.enabled = true;
        Set_Color(view.image, bulletColor, 1f);
    }

    // 子弹上抛、旋转，持续下坠到屏幕外
    void Bullet_Update(float dt)
    {
        for (int i = 0; i < Bullet_Max; i++)
        {
            Bullet_View view = bulletViews[i];
            if (view.timer <= 0f) continue;

            view.timer -= dt;
            view.vel.y += bulletGravity * dt;
            view.pos += view.vel * dt;
            view.angle += bulletSpin * dt;

            view.rect.anchoredPosition = view.pos;
            view.rect.localRotation = Quaternion.Euler(0f, 0f, view.angle);

            // 子弹一路保持实色，掉出屏幕底部后再回收
            float offscreenY = -panelOffset.y - bulletOffscreenMargin;
            Set_Color(view.image, bulletColor, 1f);

            if (view.pos.y < offscreenY || view.timer <= 0f)
            {
                view.timer = 0f;
                view.image.enabled = false;
            }
        }
    }

    // 第几个弹夹的位置
    Vector2 Slot_Pos(int index)
        => new Vector2(magazineWidth * 0.5f + index * (magazineWidth + magazineGap), 0f);

    // 由贴图建 Sprite
    Sprite Sprite_From(Texture2D tex)
        => Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
            new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);

    // 建一个空 RectTransform
    RectTransform New_Rect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = gameObject.layer;
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    // 建一张 UI 图，pivot 由用途决定
    Image New_Image(string name, Sprite sprite, Vector2 size, Vector2 pivot, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image));
        go.layer = gameObject.layer;
        go.transform.SetParent(parent, false);

        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        image.rectTransform.anchorMin = new Vector2(0f, 0f);
        image.rectTransform.anchorMax = new Vector2(0f, 0f);
        image.rectTransform.pivot = pivot;
        image.rectTransform.sizeDelta = size;
        return image;
    }

    // 设置材质透明度
    void Set_Color(Image image, Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        if (image.material != null) image.material.SetColor("_Color", color);
        else image.color = color;
    }
}
