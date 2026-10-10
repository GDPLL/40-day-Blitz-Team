using TMPro;
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

    [Header("命中X")]
    public float hitCrossSize = 40f;         // X尺寸
    public float hitCrossLife = 0.25f;       // X显示时长
    public float killWobbleSpeed = 30f;      // 击杀波动速度
    public float killWobbleAngle = 18f;      // 击杀波动角度
    Image hitCross;                          // 命中X
    float hitCrossTimer;                     // X计时
    bool hitCrossKill;                       // 本次击杀
    Sprite crossSprite;                      // X贴图
    Color hitColor = new Color(1f, 0.85f, 0.2f, 1f);   // 命中黄
    Color killColor = new Color(1f, 0.2f, 0.2f, 1f);   // 击杀红

    [Header("准心")]
    public float crossKick = 1.35f;          // 命中放大倍率
    public float crossKickBack = 12f;        // 回弹速度
    public float killShake = 0.12f;          // 击杀波动幅度
    float crossKickValue;                    // 当前放大倍率
    float killShakeTimer;                    // 击杀波动计时

    [Header("伤害跳字")]
    public float jumpTextLife = 0.7f;        // 跳字时长
    public float jumpTextRise = 60f;         // 跳字上浮像素
    public float jumpTextSize = 40f;         // 跳字字号
    public int jumpTextCount = 8;            // 跳字池大小
    TextMeshProUGUI[] jumpTexts;             // 跳字池
    float[] jumpTimers;                      // 跳字计时
    Vector2[] jumpStart;                     // 跳字起点
    int jumpIndex;                           // 跳字轮转

    [Header("积分")]
    public float scoreFontSize = 40f;        // 积分字号
    public Vector2 scoreOffset = new Vector2(-40f, -40f);    // 右上内缩
    TextMeshProUGUI scoreText;               // 积分文本
    int scoreShown = -1;                     // 已显示积分

    [Header("加分提示")]
    public float scoreAddFontSize = 34f;     // 加分字号
    public float scoreAddLife = 0.8f;        // 加分显示时长
    public float scoreAddRise = 50f;         // 加分上浮像素
    public int scoreAddCount = 6;            // 加分池大小
    Color scoreAddColor = new Color(1f, 0.85f, 0.2f, 1f);   // 加分黄
    TextMeshProUGUI[] scoreAdds;             // 加分池
    float[] scoreAddTimers;                  // 加分计时
    Vector2[] scoreAddBase;                  // 加分起点
    int scoreAddIndex;                       // 加分轮转

    [Header("受击圆弧")]
    public float arcMargin = 0.08f;       // 离屏幕边缘比例
    public float arcSpread = 35f;         // 圆弧张开角度
    public float arcLife = 0.8f;          // 圆弧显示时长
    public float arcAlpha = 0.5f;         // 圆弧峰值透明度
    Image damageArc;                      // 受击圆弧
    float arcTimer;                       // 圆弧计时
    Sprite arcSprite;                     // 圆弧贴图

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

        hitCross = MakeCross();
        MakeJumpTexts();
        MakeScoreText();
        MakeScoreAdds();
        damageArc = MakeArc();
        isReady = true;

        Debug.Log("Hit_Feedback_UI|Hit_Feedback_UI_Init|完成初始化");
    }

    // 显示命中反馈，damage为实际伤害，killScore为击杀分
    public void Hit_Result_Performance_Local(int damage, int killScore, Vector3 hitPoint)
    {
        if (!isReady)
        {
            Debug.LogError("Hit_Feedback_UI|Hit_Result_Performance_Local|未完成初始化");
            return;
        }

        bool kill = killScore > 0;

        // 命中X，击杀红字与更长波动
        hitCrossKill = kill;
        hitCrossTimer = kill ? hitCrossLife * 2f : hitCrossLife;
        hitCross.color = kill ? killColor : hitColor;
        hitCross.rectTransform.position = focus.position;
        hitCross.rectTransform.localRotation = Quaternion.identity;
        hitCross.gameObject.SetActive(true);

        // 准心放大，击杀叠加波动
        crossKickValue = crossKick;
        if (kill) killShakeTimer = hitCrossLife * 2f;

        // 伤害跳字
        ShowJumpText(damage, kill, hitPoint);

        // 伤害分与击杀分分开提示
        ShowScoreAdd(damage, scoreAddColor, 0f);
        if (kill) ShowScoreAdd(killScore, killColor, scoreAddFontSize);
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

    // 本机每帧刷新反馈与积分，score为当前积分
    public void Hit_Feedback_UI_Show(int score)
    {
        if (!isReady) return;

        // 命中X淡出与击杀波动
        if (hitCrossTimer > 0f)
        {
            hitCrossTimer -= Time.deltaTime;

            SetCrossAlpha(Mathf.Clamp01(hitCrossTimer / hitCrossLife));

            if (hitCrossKill)
                hitCross.rectTransform.localRotation =
                    Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * killWobbleSpeed) * killWobbleAngle);
        }

        // 准心回弹
        crossKickValue = Mathf.MoveTowards(crossKickValue, 1f, crossKickBack * Time.deltaTime);

        // 击杀波动逐步减弱
        float shake = 0f;
        if (killShakeTimer > 0f)
        {
            killShakeTimer -= Time.deltaTime;
            shake = Mathf.Sin(Time.time * killWobbleSpeed) * killShake
                    * Mathf.Clamp01(killShakeTimer / (hitCrossLife * 2f));
        }
        focus.localScale = Vector3.one * (crossKickValue + shake);

        // 跳字上浮淡出
        for (int i = 0; i < jumpTexts.Length; i++)
        {
            if (jumpTimers[i] <= 0f) continue;

            jumpTimers[i] -= Time.deltaTime;
            float k = Mathf.Clamp01(jumpTimers[i] / jumpTextLife);

            jumpTexts[i].rectTransform.position = jumpStart[i] + Vector2.up * (jumpTextRise * (1f - k));

            Color c = jumpTexts[i].color;
            c.a = k;
            jumpTexts[i].color = c;

            if (jumpTimers[i] <= 0f) jumpTexts[i].gameObject.SetActive(false);
        }

        // 积分显示
        if (score != scoreShown)
        {
            scoreShown = score;
            scoreText.text = score.ToString();
        }

        // 加分上浮淡出
        for (int i = 0; i < scoreAdds.Length; i++)
        {
            if (scoreAddTimers[i] <= 0f) continue;

            scoreAddTimers[i] -= Time.deltaTime;
            float k = Mathf.Clamp01(scoreAddTimers[i] / scoreAddLife);

            scoreAdds[i].rectTransform.anchoredPosition = scoreAddBase[i] + Vector2.up * (scoreAddRise * (1f - k));

            Color c = scoreAdds[i].color;
            c.a = k;
            scoreAdds[i].color = c;

            if (scoreAddTimers[i] <= 0f) scoreAdds[i].gameObject.SetActive(false);
        }

        // 圆弧计时淡出
        if (arcTimer > 0f)
        {
            arcTimer -= Time.deltaTime;
            SetArcAlpha(arcAlpha * Mathf.Clamp01(arcTimer / arcLife));
        }
    }

    // 显示伤害跳字，kill为是否击杀
    void ShowJumpText(int damage, bool kill, Vector3 hitPoint)
    {
        if (jumpTexts == null || jumpTexts.Length == 0)
        {
            Debug.LogError("Hit_Feedback_UI|ShowJumpText|跳字池未生成");
            return;
        }

        int i = jumpIndex;
        jumpIndex = (jumpIndex + 1) % jumpTexts.Length;

        Vector3 sp = cam.WorldToScreenPoint(hitPoint);
        Vector2 start = sp.z > 0f ? (Vector2)sp : (Vector2)focus.position;   //背对时退到准星

        TextMeshProUGUI t = jumpTexts[i];
        t.text = damage.ToString();
        t.color = kill ? killColor : hitColor;
        t.rectTransform.position = start;
        t.gameObject.SetActive(true);

        jumpStart[i] = start;
        jumpTimers[i] = jumpTextLife;
    }

    // 设置X透明度
    void SetCrossAlpha(float alpha)
    {
        Color c = hitCross.color;
        c.a = alpha;
        hitCross.color = c;

        bool visible = alpha > 0.001f;   //显示阈值
        if (hitCross.gameObject.activeSelf != visible) hitCross.gameObject.SetActive(visible);
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

    // 生成命中X
    Image MakeCross()
    {
        GameObject go = new GameObject("HitCross", typeof(Image));
        go.transform.SetParent(canvas.transform, false);

        Image img = go.GetComponent<Image>();
        img.sprite = GetCrossSprite();
        img.color = hitColor;
        img.raycastTarget = false;
        img.rectTransform.sizeDelta = Vector2.one * (hitCrossSize / canvas.scaleFactor);
        go.SetActive(false);

        return img;
    }

    // 生成跳字池
    void MakeJumpTexts()
    {
        jumpTexts = new TextMeshProUGUI[jumpTextCount];
        jumpTimers = new float[jumpTextCount];
        jumpStart = new Vector2[jumpTextCount];

        for (int i = 0; i < jumpTextCount; i++)
        {
            GameObject go = new GameObject("JumpText" + i, typeof(TextMeshProUGUI));
            go.transform.SetParent(canvas.transform, false);

            TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
            t.fontSize = jumpTextSize;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            t.rectTransform.sizeDelta = new Vector2(200f, 80f);
            go.SetActive(false);

            jumpTexts[i] = t;
        }
    }

    // 生成积分文本
    void MakeScoreText()
    {
        GameObject go = new GameObject("ScoreText", typeof(TextMeshProUGUI));
        go.transform.SetParent(canvas.transform, false);

        scoreText = go.GetComponent<TextMeshProUGUI>();
        scoreText.fontSize = scoreFontSize;
        scoreText.alignment = TextAlignmentOptions.Right;
        scoreText.raycastTarget = false;
        scoreText.color = Color.white;
        scoreText.text = "0";
        scoreText.rectTransform.anchorMin = Vector2.one;
        scoreText.rectTransform.anchorMax = Vector2.one;
        scoreText.rectTransform.pivot = Vector2.one;
        scoreText.rectTransform.anchoredPosition = scoreOffset;
        scoreText.rectTransform.sizeDelta = new Vector2(300f, 80f);
    }

    // 生成加分池
    void MakeScoreAdds()
    {
        scoreAdds = new TextMeshProUGUI[scoreAddCount];
        scoreAddTimers = new float[scoreAddCount];
        scoreAddBase = new Vector2[scoreAddCount];

        for (int i = 0; i < scoreAddCount; i++)
        {
            GameObject go = new GameObject("ScoreAdd" + i, typeof(TextMeshProUGUI));
            go.transform.SetParent(canvas.transform, false);

            TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
            t.fontSize = scoreAddFontSize;
            t.alignment = TextAlignmentOptions.Right;
            t.raycastTarget = false;
            t.color = scoreAddColor;
            t.rectTransform.anchorMin = Vector2.one;
            t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.pivot = Vector2.one;
            t.rectTransform.sizeDelta = new Vector2(300f, 60f);
            go.SetActive(false);

            scoreAdds[i] = t;
        }
    }

    // 显示加分提示，delta为加分，stack为叠放偏移
    void ShowScoreAdd(int delta, Color color, float stack)
    {
        if (scoreAdds == null || scoreAdds.Length == 0)
        {
            Debug.LogError("Hit_Feedback_UI|ShowScoreAdd|加分池未生成");
            return;
        }
        if (delta <= 0) return;

        int i = scoreAddIndex;
        scoreAddIndex = (scoreAddIndex + 1) % scoreAdds.Length;

        Vector2 basePos = scoreOffset + Vector2.down * (scoreFontSize + stack);

        TextMeshProUGUI t = scoreAdds[i];
        t.text = "+" + delta;
        t.color = color;
        t.rectTransform.anchoredPosition = basePos;
        t.gameObject.SetActive(true);

        scoreAddBase[i] = basePos;
        scoreAddTimers[i] = scoreAddLife;
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

    // 生成X贴图
    Sprite GetCrossSprite()
    {
        if (crossSprite != null) return crossSprite;

        int size = 64;
        float half = size * 0.5f;
        float line = size * 0.12f;   //线宽
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // 两条对角线到像素的垂直距离
                float d1 = Mathf.Abs((x + 0.5f - half) - (y + 0.5f - half)) * 0.7071f;
                float d2 = Mathf.Abs((x + 0.5f - half) + (y + 0.5f - half)) * 0.7071f;
                bool inside = d1 <= line || d2 <= line;
                tex.SetPixel(x, y, inside ? Color.white : Color.clear);
            }
        }

        tex.Apply();
        crossSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        return crossSprite;
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
}
