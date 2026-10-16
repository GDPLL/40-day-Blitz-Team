using UnityEngine;
using UnityEngine.UI;

// 命中与受击反馈显示，只有本地玩家运行
public class Hit_Feedback_UI : MonoBehaviour
{
    // 外部引用
    RectTransform focus;   // 准星
    Camera cam;            // 本机相机
    Canvas canvas;         // 反馈所在画布
    bool isReady;          // 引用就绪

    [Header("命中箭头")]
    public float hitMarkDistance = 26f;   // 箭头离准星距离
    public float hitMarkSize = 20f;       // 箭头尺寸
    public float hitMarkLife = 0.25f;     // 箭头显示时长
    Image[] hitMarks = new Image[4];      // 四角箭头
    float hitMarkTimer;                   // 箭头计时
    Sprite arrowSprite;                   // 箭头贴图

    [Header("受击圆弧")]
    public float arcMargin = 0.08f;       // 离屏幕边缘比例
    public float arcSpread = 35f;         // 圆弧张开角度
    public float arcLife = 0.8f;          // 圆弧显示时长
    public float arcAlpha = 0.5f;         // 圆弧峰值透明度
    Image damageArc;                      // 受击圆弧
    float arcTimer;                       // 圆弧计时
    Sprite arcSprite;                     // 圆弧贴图

    [Header("回血边缘")]
    public float healPeak = 0.2f;         // 提示峰值透明度
    public float healBreath = 0.25f;      // 呼吸频率，次每秒
    public float healLife = 0.35f;        // 上升沿保持时长
    public float healFade = 1.2f;         // 透明度过渡速度
    public float healStart = 0.78f;       // 渐变起点，半屏比例
    public float healRise = 5f;           // 单帧增量上限
    public Color healColor = new Color(0.3f, 1f, 0.48f);   // 提示色
    Image healEdge;                       // 回血边缘图
    float healTimer;                      // 保持剩余
    float healShow;                       // 当前透明度
    int lastHealHp = -1;                  // 上次血量
    Sprite edgeSprite;                    // 边缘贴图

    static readonly Vector2[] MarkDirs =   // 四角方向
    {
        new Vector2(-1f, 1f), new Vector2(1f, 1f),
        new Vector2(1f, -1f), new Vector2(-1f, -1f)
    };

    static readonly float[] MarkAngles = { -135f, 135f, 45f, -45f };   // 四角朝向

    // 初始化，绑定准星与相机
    public void Hit_Feedback_UI_Init(RectTransform uiFocus, Camera camera)
    {
        focus = uiFocus;
        cam = camera;

        if (focus == null)
        {
            Debug.LogError("Hit_Feedback_UI|Hit_Feedback_UI_Init|准星 RectTransform 为空");
            return;
        }
        if (cam == null)
        {
            Debug.LogError("Hit_Feedback_UI|Hit_Feedback_UI_Init|相机为空");
            return;
        }

        canvas = focus.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("Hit_Feedback_UI|Hit_Feedback_UI_Init|准星不在画布下");
            return;
        }

        for (int i = 0; i < hitMarks.Length; i++) hitMarks[i] = MakeHitMark(i);

        damageArc = MakeArc();
        healEdge = MakeHealEdge();
        isReady = true;

        Debug.Log("Hit_Feedback_UI|Hit_Feedback_UI_Init|完成初始化");
    }

    // 显示命中箭头
    public void Hit_Mark_Performance_Local()
    {
        if (!isReady)
        {
            Debug.LogError("Hit_Feedback_UI|Hit_Mark_Performance_Local|未完成初始化");
            return;
        }

        LayoutHitMarks();

        hitMarkTimer = hitMarkLife;
        SetHitMarkAlpha(1f);
    }

    // 显示受击圆弧，sourcePos为攻击者位置
    public void Damage_Dir_Performance_Local(Vector3 sourcePos)
    {
        if (!isReady)
        {
            Debug.LogError("Hit_Feedback_UI|Damage_Dir_Performance_Local|未完成初始化");
            return;
        }

        // 攻击者相对本机相机的水平方位
        Vector3 toSource = sourcePos - cam.transform.position;
        toSource.y = 0f;

        Vector3 local = cam.transform.InverseTransformDirection(toSource);
        float bearing = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;

        // 屏幕内缩后的半宽半高
        float inset = Mathf.Min(Screen.width, Screen.height) * 0.5f * arcMargin;
        float halfW = Screen.width * 0.5f - inset;
        float halfH = Screen.height * 0.5f - inset;

        // 弧覆盖范围内横向纵向的最大占比
        float halfSpan = arcSpread * 0.5f;
        float maxSin = 0f;
        float maxCos = 0f;
        for (int i = 0; i <= 8; i++)
        {
            float a = (bearing - halfSpan + arcSpread * i / 8f) * Mathf.Deg2Rad;
            maxSin = Mathf.Max(maxSin, Mathf.Abs(Mathf.Sin(a)));
            maxCos = Mathf.Max(maxCos, Mathf.Abs(Mathf.Cos(a)));
        }

        // 整条弧都不越界的最大半径
        float radius = Mathf.Min(halfW / Mathf.Max(maxSin, 0.001f), halfH / Mathf.Max(maxCos, 0.001f));

        damageArc.rectTransform.position = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, focus.position.z);
        damageArc.rectTransform.sizeDelta = Vector2.one * (radius * 2f / canvas.scaleFactor);
        damageArc.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -bearing);

        arcTimer = arcLife;
        SetArcAlpha(arcAlpha);
    }

    // 回血边缘，hp为当前血量，healCap为回血上限
    public void Heal_Edge_Performance_Local(int hp, int healCap)
    {
        if (!isReady) return;

        if (lastHealHp < 0) lastHealHp = hp;    //开局记录基准

        int rise = hp - lastHealHp;
        lastHealHp = hp;

        // 回血上升沿才续期，挡复活大跳
        if (rise > 0 && rise <= healRise && hp <= healCap) healTimer = healLife;
    }

    // 本机每帧刷新反馈计时
    public void Hit_Feedback_UI_Show()
    {
        if (!isReady) return;

        // 箭头计时淡出
        if (hitMarkTimer > 0f)
        {
            hitMarkTimer -= Time.deltaTime;
            SetHitMarkAlpha(Mathf.Clamp01(hitMarkTimer / hitMarkLife));
        }

        // 圆弧计时淡出
        if (arcTimer > 0f)
        {
            arcTimer -= Time.deltaTime;
            SetArcAlpha(arcAlpha * Mathf.Clamp01(arcTimer / arcLife));
        }

        // 回血边缘呼吸脉动
        if (healTimer > 0f) healTimer -= Time.deltaTime;
        float healTarget = healTimer > 0f
            ? healPeak * (0.6f + 0.4f * Mathf.Sin(Time.time * healBreath * Mathf.PI * 2f))
            : 0f;
        healShow = Mathf.MoveTowards(healShow, healTarget, healFade * Time.deltaTime);
        SetHealAlpha(healShow);
    }

    // 箭头排到准星四角
    void LayoutHitMarks()
    {
        for (int i = 0; i < hitMarks.Length; i++)
        {
            hitMarks[i].rectTransform.position = focus.position + (Vector3)(MarkDirs[i] * hitMarkDistance);
            hitMarks[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, MarkAngles[i]);
        }
    }

    // 设置箭头透明度
    void SetHitMarkAlpha(float alpha)
    {
        bool visible = alpha > 0.001f;   //显示阈值

        for (int i = 0; i < hitMarks.Length; i++)
        {
            Color c = hitMarks[i].color;
            c.a = alpha;
            hitMarks[i].color = c;

            if (hitMarks[i].gameObject.activeSelf != visible) hitMarks[i].gameObject.SetActive(visible);
        }
    }

    // 设置圆弧透明度
    void SetArcAlpha(float alpha)
    {
        Color c = damageArc.color;
        c.a = alpha;
        damageArc.color = c;

        bool visible = alpha > 0.001f;   //显示阈值
        if (damageArc.gameObject.activeSelf != visible) damageArc.gameObject.SetActive(visible);
    }

    // 生成一个命中箭头
    Image MakeHitMark(int index)
    {
        GameObject go = new GameObject("HitMark" + index, typeof(Image));
        go.transform.SetParent(canvas.transform, false);

        Image img = go.GetComponent<Image>();
        img.sprite = GetArrowSprite();
        img.color = new Color(1f, 1f, 1f, 0f);
        img.raycastTarget = false;
        img.rectTransform.sizeDelta = Vector2.one * (hitMarkSize / canvas.scaleFactor);
        go.SetActive(false);

        return img;
    }

    // 生成受击圆弧
    Image MakeArc()
    {
        GameObject go = new GameObject("DamageArc", typeof(Image));
        go.transform.SetParent(canvas.transform, false);

        Image img = go.GetComponent<Image>();
        img.sprite = GetArcSprite();
        img.color = new Color(1f, 0.2f, 0.2f, 0f);
        img.raycastTarget = false;
        go.SetActive(false);

        return img;
    }

    // 生成回血边缘图，压在其余界面下
    Image MakeHealEdge()
    {
        GameObject go = new GameObject("HealEdge", typeof(Image));
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsFirstSibling();      // 底层，不挡HUD

        Image img = go.GetComponent<Image>();
        img.sprite = GetEdgeSprite();
        img.color = new Color(healColor.r, healColor.g, healColor.b, 0f);
        img.raycastTarget = false;
        img.rectTransform.anchorMin = Vector2.zero;
        img.rectTransform.anchorMax = Vector2.one;
        img.rectTransform.offsetMin = Vector2.zero;
        img.rectTransform.offsetMax = Vector2.zero;
        go.SetActive(false);

        return img;
    }

    // 设置边缘图透明度
    void SetHealAlpha(float alpha)
    {
        Color c = healEdge.color;
        c.a = alpha;
        healEdge.color = c;

        bool visible = alpha > 0.001f;   //显示阈值
        if (healEdge.gameObject.activeSelf != visible) healEdge.gameObject.SetActive(visible);
    }

    // 生成箭头贴图
    Sprite GetArrowSprite()
    {
        if (arrowSprite != null) return arrowSprite;

        int size = 64;
        float cx = size * 0.5f;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);

        for (int y = 0; y < size; y++)
        {
            float half = size * 0.3f * (size - y) / size;   // 三角半宽

            for (int x = 0; x < size; x++)
            {
                bool inside = Mathf.Abs(x + 0.5f - cx) <= half;
                tex.SetPixel(x, y, inside ? Color.white : Color.clear);
            }
        }

        tex.Apply();
        arrowSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        return arrowSprite;
    }

    // 生成圆弧贴图
    Sprite GetArcSprite()
    {
        if (arcSprite != null) return arcSprite;

        int size = 256;
        float half = size * 0.5f;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 offset = new Vector2(x + 0.5f - half, y + 0.5f - half);
                float d = offset.magnitude;

                // 环带内再取正上方扇面
                bool inside = d <= half && d > half - 3f
                    && Vector2.Angle(Vector2.up, offset) <= arcSpread * 0.5f;

                tex.SetPixel(x, y, inside ? Color.white : Color.clear);
            }
        }

        tex.Apply();
        arcSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        return arcSprite;
    }

    // 生成边缘贴图，中间透明四边渐亮
    Sprite GetEdgeSprite()
    {
        if (edgeSprite != null) return edgeSprite;

        int size = 256;
        float half = size * 0.5f;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // 归一化半径，1 落在四边中点
                float rx = (x + 0.5f - half) / half;
                float ry = (y + 0.5f - half) / half;
                float r = Mathf.Sqrt(rx * rx + ry * ry);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(healStart, 1f, r));

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }

        tex.Apply();
        edgeSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        return edgeSprite;
    }
}
