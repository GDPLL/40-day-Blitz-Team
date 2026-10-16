using UnityEngine;
using UnityEngine.UI;

// 本机人形状态图，由 Player_Main 驱动
public class UI_Body : MonoBehaviour
{
    //姿态编号，与姿态图数组顺序一致
    const int Pose_Stand = 0;       //站立低持枪
    const int Pose_Walk = 1;        //行走低持枪
    const int Pose_Run = 2;         //冲刺低持枪
    const int Pose_Squat = 3;       //蹲姿低持枪
    const int Pose_SquatWalk = 4;   //蹲姿低持枪2
    const int Pose_StandUp = 5;     //站立举枪
    const int Pose_WalkUp = 6;      //行走举枪
    const int Pose_RunUp = 7;       //冲刺举枪
    const int Pose_SquatUp = 8;     //蹲姿举枪
    const int Pose_KneelUp = 9;     //半跪举枪
    const int Pose_Death = 10;      //死亡
    const int Pose_UpBegin = 5;     //举枪段起始编号

    public Image bodyImage;             //人形图
    public Sprite[] poseSprites;        //姿态图，按姿态编号
    public Vector2[] poseOffset;        //各姿态位置微调
    public float heavyHp = 30f;         //低血量线
    public float flashSpeed = 4f;       //红闪频率，次每秒
    public float damageLife = 0.25f;    //受伤闪时长，秒
    public float bloodLife = 0.28f;     //血滩铺开时长，秒
    public float outerLife = 0.38f;     //外溅退散时长，秒
    public float poolSizeMin = 90f;     //血滩起始直径
    public float poolSizeMax = 400f;    //血滩最大直径
    public float outerSizeMin = 280f;   //外溅起始直径
    public float outerSizeMax = 680f;   //外溅最大直径
    public float shakeAmp = 6f;         //受伤抖动幅度，像素
    public Vector2 bloodAnchor = new Vector2(0f, 30f);  //爆浆在人形上的位置
    public float bodyHeight = 300f;     //人形显示高度，像素
    public float colorSmooth = 0.8f;    //掉血变色过渡时长，秒
    public float[] poseHeightFactor;    //各姿态身高系数，留空用内置表

    //各姿态身高系数，站立为 1
    static readonly float[] Height_Default =
    {
        0.86f,      //站立低持枪
        0.86f,      //行走低持枪
        0.75f,      //冲刺低持枪
        0.70f,      //蹲姿低持枪
        0.70f,      //蹲走低持枪
        0.84f,      //站立举枪
        0.82f,      //行走举枪
        0.80f,      //冲刺举枪
        0.70f,      //蹲姿举枪
        0.70f,      //蹲走举枪
        0.480f      //死亡
    };

    Material mat;                       //人形材质
    int lastPose = -1;                  //上次姿态编号
    int lastHp = -1;                    //上次血量
    Color currentColor = Color.white;   //当前显示色
    float damageTimer;                  //受伤闪剩余
    Image poolImage;                    //血滩图
    Image outerImage;                   //外溅图
    Image mistImage;                    //血雾图
    float bloodTimer;                   //血滩剩余
    float outerTimer;                   //外溅剩余
    float bloodTier = 1f;               //本次受伤强度
    Vector2 basePos;                    //人形初始位置
    bool isReady;                       //引用就绪

    // 初始化，建人形材质与爆浆图层
    public void UI_Body_Init()
    {
        if (bodyImage == null)
        {
            Debug.LogError("UI_Body|UI_Body_Init|人形图未绑定");
            return;
        }
        if (poseSprites == null || poseSprites.Length == 0)
        {
            Debug.LogError("UI_Body|UI_Body_Init|姿态图未绑定");
            return;
        }
        basePos = bodyImage.rectTransform.anchoredPosition;

        //左对齐，pivot 移到左下角
        basePos += new Vector2(bodyImage.rectTransform.sizeDelta.x * 0.5f, 0f);
        bodyImage.rectTransform.pivot = new Vector2(0f, 0f);

        Shader shader = Resources.Load<Shader>("UI_Body");
        if (shader == null) shader = Shader.Find("UI/Body");
        if (shader == null)
        {
            Debug.LogError("UI_Body|UI_Body_Init|未找到 UI_Body 着色器");
            return;
        }

        mat = new Material(shader);
        bodyImage.material = mat;
        mat.SetColor("_FlashColor", new Color(1f, 0.25f, 0.2f));    //受伤闪亮红
        Blood_Layer_Init();
        isReady = true;

        Debug.Log("UI_Body|UI_Body_Init|完成初始化");
    }

    // 按血量与状态位刷新，state 为表现状态位
    public void UI_Body_Show(int hp, int maxHp, ushort state)
    {
        if (!isReady) return;

        bool dead = (state & Player_animation.BitDeath) != 0 || hp <= 0;
        bool gunUp = (state & Player_animation.BitGun) != 0;

        //姿态切换
        int pose = Pose_Index(state, dead, gunUp);
        if (pose != lastPose) Set_Pose(pose);

        //血量下降起一次受伤闪与爆浆
        if (lastHp >= 0 && hp < lastHp)
        {
            damageTimer = damageLife;
            Blood_Burst(Hp_Tier(hp, maxHp));
        }
        lastHp = hp;

        float damage = 0f;
        if (damageTimer > 0f)
        {
            damageTimer -= Time.deltaTime;
            damage = Mathf.Clamp01(damageTimer / damageLife);
        }

        //喂材质参数
        Color target = Hp_Color(hp, maxHp, dead, 0f);
        float fade = colorSmooth > 0f ? Mathf.Clamp01(Time.deltaTime / colorSmooth) : 1f;
        currentColor = Color.Lerp(currentColor, target, fade);
        mat.SetColor("_Color", currentColor);
        mat.SetFloat("_Damage", damage);

        //爆浆与受伤抖动
        Blood_Show(Time.deltaTime);
        Vector2 shake = damageTimer > 0f
            ? UnityEngine.Random.insideUnitCircle * shakeAmp * damage : Vector2.zero;
        bodyImage.rectTransform.anchoredPosition = basePos + Pose_Offset(lastPose) + shake;
    }

    // 按状态位取姿态编号，死亡优先
    int Pose_Index(ushort state, bool dead, bool gunUp)
    {
        if (dead) return Fallback_Index(Pose_Death);

        bool walk = (state & Player_animation.BitWalk) != 0;
        bool run = (state & Player_animation.BitRun) != 0;
        bool squat = (state & Player_animation.BitSquat) != 0;

        //蹲下分静止与移动
        if (squat && walk) return Fallback_Index(gunUp ? Pose_KneelUp : Pose_SquatWalk);
        if (squat) return Fallback_Index(gunUp ? Pose_SquatUp : Pose_Squat);
        if (run) return Fallback_Index(gunUp ? Pose_RunUp : Pose_Run);
        if (walk) return Fallback_Index(gunUp ? Pose_WalkUp : Pose_Walk);
        return Fallback_Index(gunUp ? Pose_StandUp : Pose_Stand);
    }

    // 缺图回退取相近姿态
    int Fallback_Index(int index)
    {
        if (!Has_Sprite(index))
        {
            Debug.LogError($"UI_Body|Fallback_Index|姿态图缺失 下标{index}");

            if (index >= Pose_UpBegin && index <= Pose_KneelUp) index -= Pose_UpBegin;
            else if (index != Pose_Stand) index = Pose_Stand;
        }

        if (Has_Sprite(index)) return index;
        return Pose_Stand;
    }

    // 该姿态是否已配图
    bool Has_Sprite(int index)
        => poseSprites != null && index >= 0 && index < poseSprites.Length
            && poseSprites[index] != null;

    // 切换姿态图与尺寸
    void Set_Pose(int index)
    {
        lastPose = index;

        bodyImage.sprite = poseSprites[index];
        Set_Auto_Size(poseSprites[index], index);
    }

    // 按人体高度自动定尺寸
    void Set_Auto_Size(Sprite sprite, int index)
    {
        if (sprite == null || sprite.rect.height <= 0f)
        {
            Debug.LogError("UI_Body|Set_Auto_Size|姿态图为空");
            return;
        }

        float scale = bodyHeight * Pose_Height(index) / sprite.rect.height;
        bodyImage.rectTransform.sizeDelta = sprite.rect.size * scale;
    }

    // 取该姿态身高系数，Inspector 填了优先
    float Pose_Height(int index)
    {
        if (poseHeightFactor != null && index >= 0 && index < poseHeightFactor.Length
            && poseHeightFactor[index] > 0f)
            return poseHeightFactor[index];

        if (index >= 0 && index < Height_Default.Length) return Height_Default[index];
        return 1f;
    }

    // 建爆浆图层，贴图由 Resources 取
    void Blood_Layer_Init()
    {
        Texture2D pool = Resources.Load<Texture2D>("爆浆/血渍");
        Texture2D mist = Resources.Load<Texture2D>("爆浆/血雾团");
        if (pool == null || mist == null)
        {
            Debug.LogError("UI_Body|Blood_Layer_Init|爆浆贴图缺失");
            return;
        }

        outerImage = New_Blood_Image(Sprite_From(pool), bodyImage.transform);
        poolImage = New_Blood_Image(Sprite_From(pool), bodyImage.transform);
        mistImage = New_Blood_Image(Sprite_From(mist), bodyImage.transform);
    }

    // 由贴图建 Sprite
    Sprite Sprite_From(Texture2D tex)
        => Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));

    // 建一张爆浆图，parent 为挂点
    Image New_Blood_Image(Sprite sprite, Transform parent)
    {
        GameObject go = new GameObject("Blood", typeof(Image));
        go.transform.SetParent(parent, false);

        Image img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        img.enabled = false;
        img.rectTransform.anchoredPosition = bloodAnchor;

        return img;
    }

    // 起一次爆浆，tier 为强度档
    void Blood_Burst(float tier)
    {
        bloodTier = tier;
        bloodTimer = bloodLife;
        outerTimer = outerLife;

        if (mistImage != null) mistImage.enabled = tier > 1.2f;    //轻重伤才带雾
    }

    // 推进爆浆计时，dt 为帧间隔
    void Blood_Show(float dt)
    {
        //血滩爆开
        if (bloodTimer > 0f && poolImage != null)
        {
            bloodTimer -= dt;
            float p = 1f - Mathf.Clamp01(bloodTimer / bloodLife);
            float e = 1f - (1f - p) * (1f - p);     //先快后慢
            poolImage.enabled = true;
            poolImage.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(poolSizeMin, poolSizeMax * bloodTier, e);
            Set_Alpha(poolImage, 1f - p);
        }
        else if (poolImage != null) poolImage.enabled = false;

        //外溅散开
        if (outerTimer > 0f && outerImage != null)
        {
            outerTimer -= dt;
            float p = 1f - Mathf.Clamp01(outerTimer / outerLife);
            outerImage.enabled = true;
            outerImage.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(outerSizeMin, outerSizeMax * bloodTier, p);
            Set_Alpha(outerImage, 0.6f * (1f - p));
        }
        else if (outerImage != null) outerImage.enabled = false;

        //血雾随血滩一起退
        if (mistImage != null && mistImage.enabled)
        {
            float p = 1f - Mathf.Clamp01(bloodTimer / bloodLife);
            mistImage.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(poolSizeMin, poolSizeMax * bloodTier, p);
            Set_Alpha(mistImage, 0.75f * (1f - p));
        }
    }

    // 按受击后血量取强度档
    float Hp_Tier(int hp, int maxHp)
    {
        float t = maxHp > 0 ? (float)hp / maxHp : 1f;
        if (t > 0.6f) return 1f;        //轻
        if (t > 0.3f) return 1.35f;     //重
        return 1.7f;                    //致命
    }

    // 设置图透明度
    void Set_Alpha(Image img, float alpha)
    {
        Color c = img.color;
        c.a = Mathf.Clamp01(alpha);
        img.color = c;
    }

    // 取该姿态的位置微调
    Vector2 Pose_Offset(int index)
        => poseOffset != null && index >= 0 && index < poseOffset.Length
            ? poseOffset[index] : Vector2.zero;

    // 血量映射主色，低血量红闪
    Color Hp_Color(int hp, int maxHp, bool dead, float damage)
    {
        Color low = new Color(0.95f, 0.15f, 0.12f);     //低血
        Color mid = new Color(0.98f, 0.82f, 0.25f);     //中血
        Color full = new Color(0.93f, 0.97f, 0.90f);    //满血
        Color bright = new Color(1f, 0.42f, 0.34f);     //红闪亮点
        Color grey = new Color(0.26f, 0.26f, 0.30f);    //死亡压暗

        if (dead) return grey;

        float t = maxHp > 0 ? Mathf.Clamp01((float)hp / maxHp) : 1f;

        //半血以下转红，半血以上转白
        Color color = t >= 0.5f
            ? Color.Lerp(mid, full, (t - 0.5f) * 2f)
            : Color.Lerp(low, mid, t * 2f);

        //低血量红闪
        if (hp < heavyHp)
        {
            float pulse = Mathf.Sin(Time.time * flashSpeed * Mathf.PI * 2f) * 0.5f + 0.5f;
            color = Color.Lerp(low, bright, pulse);
        }

        //受伤白闪
        return Color.Lerp(color, Color.white, damage * 0.75f);
    }
}
