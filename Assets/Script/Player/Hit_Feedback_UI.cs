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
    TMP_FontAsset uiFont;  // 界面字体

    const string FontPath = "Fonts & Materials/lemixisu SDF";   //界面字体资源路径

    [Header("命中X")]
    public float hitCrossSize = 40f;         // X尺寸
    public float hitCrossLife = 0.25f;       // X显示时长
    public float hitCrossPopTime = 0.12f;    // X弹出时长
    public float hitCrossPopHit = 1.3f;      // 命中弹出倍率
    public float hitCrossPopKill = 1.9f;     // 击杀弹出倍率
    public float hitCrossHold = 0.6f;        // 满亮占比
    public float hitCrossFlash = 0.06f;      // 出生闪白时长
    public float hitCrossGlowScale = 1.6f;   // 背衬X倍率
    public float hitCrossGlowAlpha = 0.45f;  // 背衬X透明度
    public float hitCrossKillScale = 1.6f;   // 击杀X放大倍率
    public float killWobbleSpeed = 30f;      // 击杀摆动速度
    public float killWobbleAngle = 25f;      // 击杀摆动角度
    public float killBurstSize = 90f;        // 击杀放射线尺寸
    public float killBurstLife = 0.22f;      // 击杀放射线时长
    Image hitCross;                          // 命中X核心
    Image hitCrossGlow;                      // 击杀X背衬
    float hitCrossTimer;                     // X计时
    float hitCrossMax;                       // X本次时长
    float hitCrossPopTimer;                  // X弹出计时
    bool hitCrossKill;                       // 本次击杀
    Sprite crossSprite;                      // X贴图
    Color hitColor = new Color(1f, 0.85f, 0.2f, 1f);   // 命中黄
    Color killColor = new Color(1f, 0.2f, 0.2f, 1f);   // 击杀红
    Color crossFlashColor = new Color(1f, 1f, 1f, 1f);  // 出生闪白
    Image killBurst;                         // 击杀放射线
    float killBurstTimer;                    // 放射线计时
    Sprite burstSprite;                      // 放射线贴图

    [Header("准心")]
    public float crossKick = 1.35f;          // 命中放大倍率
    public float crossKickBack = 12f;        // 回弹速度
    public float killShake = 0.12f;          // 击杀波动幅度
    float crossKickValue;                    // 当前放大倍率
    float killShakeTimer;                    // 击杀波动计时

    [Header("伤害跳字")]
    public float jumpTextLife = 0.7f;        // 跳字时长
    public float jumpTextRise = 60f;         // 跳字上浮高度
    public float jumpTextGravity = 900f;     // 跳字重力
    public float jumpTextGap = 70f;          // 跳字离身体间距
    public float jumpTextSize = 40f;         // 跳字字号
    public float jumpTextKillSize = 56f;     // 击杀跳字字号
    public float jumpTextPop = 0.15f;        // 跳字弹出时长
    public float jumpTextOvershoot = 0.25f;  // 跳字弹出过冲
    public float jumpTextKillOvershoot = 0.5f;   // 击杀跳字过冲
    public float jumpTextTilt = 6f;          // 跳字随机角度
    public float jumpTextOutline = 0.2f;     // 跳字描边宽度
    public int jumpTextCount = 8;            // 跳字池大小
    TextMeshProUGUI[] jumpTexts;             // 跳字池
    float[] jumpTimers;                      // 跳字计时
    Vector2[] jumpStart;                     // 跳字起点
    Vector2[] jumpVel;                       // 跳字初速
    float[] jumpOvershoot;                   // 跳字过冲
    int jumpIndex;                           // 跳字轮转
    Color32 jumpTextOutlineColor = new Color32(10, 10, 14, 255);   // 跳字描边色

    [Header("积分")]
    public float scoreFontSize = 40f;        // 积分字号
    public Vector2 scoreOffset = new Vector2(-40f, -40f);    // 右上内缩
    TextMeshProUGUI scoreText;               // 积分文本
    int scoreShown = -1;                     // 已显示积分

    [Header("积分列")]
    public float scoreAddFontSize = 30f;     // 加分字号
    public float scoreAddLife = 0.9f;        // 加分显示时长
    public float scoreAddSide = 60f;         // 离准星右侧距离
    public float scoreAddDown = -30f;        // 离准星下方距离
    public float scoreAddGap = 38f;          // 加分行距
    public float scoreAddSlide = 900f;       // 加分下滑速度
    public int scoreAddCount = 6;            // 加分池大小
    Color scoreAddColor = new Color(1f, 0.85f, 0.2f, 1f);   // 加分黄
    Color deadColor = new Color(0.55f, 0.55f, 0.6f, 1f);    // 无效命中灰
    TextMeshProUGUI[] scoreAdds;             // 加分池
    float[] scoreAddTimers;                  // 加分计时
    float[] scoreAddY;                       // 加分当前Y
    int[] scoreAddSeq;                       // 加分序号
    int scoreAddIndex;                       // 加分轮转
    int scoreAddCounter;                     // 加分序号累加

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

        uiFont = Resources.Load<TMP_FontAsset>(FontPath);
        if (uiFont == null)
        {
            Debug.LogError("Hit_Feedback_UI|Hit_Feedback_UI_Init|界面字体未找到");
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
    public void Hit_Result_Performance_Local(int damage, int killScore, Vector3 hitPoint,
        Vector3 hitCenter, Vector3 hitSize)
    {
        if (!isReady)
        {
            Debug.LogError("Hit_Feedback_UI|Hit_Result_Performance_Local|未完成初始化");
            return;
        }

        bool kill = killScore > 0;

        // 命中X，击杀红字与更长波动
        hitCrossKill = kill;
        hitCrossMax = kill ? hitCrossLife * 2f : hitCrossLife;
        hitCrossTimer = hitCrossMax;
        hitCrossPopTimer = hitCrossPopTime;
        hitCross.color = crossFlashColor;
        hitCross.rectTransform.position = focus.position;
        hitCross.rectTransform.localRotation = Quaternion.identity;
        hitCross.rectTransform.localScale = Vector3.one * (kill ? hitCrossPopKill * hitCrossKillScale : hitCrossPopHit);
        hitCrossGlow.rectTransform.position = focus.position;
        hitCrossGlow.rectTransform.localRotation = Quaternion.identity;
        hitCrossGlow.rectTransform.localScale = hitCross.rectTransform.localScale;
        hitCrossGlow.gameObject.SetActive(kill);
        hitCross.gameObject.SetActive(true);

        // 击杀放射线从准星外放
        killBurstTimer = kill ? killBurstLife : 0f;
        killBurst.rectTransform.position = focus.position;
        killBurst.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 60f));
        killBurst.rectTransform.localScale = Vector3.one * 0.45f;
        killBurst.gameObject.SetActive(kill);

        // 准心放大，击杀叠加波动
        crossKickValue = crossKick;
        if (kill) killShakeTimer = hitCrossLife * 2f;

        // 伤害跳字
        ShowJumpText(damage, kill, hitPoint, hitCenter, hitSize);

        // 伤害分与击杀分分开提示，打尸体不加分转灰
        ShowScoreAdd(damage, damage > 0 ? scoreAddColor : deadColor);
        if (kill) ShowScoreAdd(killScore, killColor);
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

            float k = Mathf.Clamp01(hitCrossTimer / hitCrossMax);
            float age = hitCrossMax - hitCrossTimer;

            // 满亮保持后快淡
            float alpha = k >= hitCrossHold ? 1f : Mathf.Pow(k / hitCrossHold, 0.7f);

            // 出生闪白过渡到命中色
            Color target = hitCrossKill ? killColor : hitColor;
            hitCross.color = age < hitCrossFlash
                ? Color.Lerp(crossFlashColor, target, age / hitCrossFlash) : target;

            // 出生放大弹回
            float pop = Mathf.Clamp01(hitCrossPopTimer / hitCrossPopTime);
            float popScale = Mathf.Lerp(1f, hitCrossKill ? hitCrossPopKill : hitCrossPopHit, pop * pop)
                * (hitCrossKill ? hitCrossKillScale : 1f);
            if (hitCrossPopTimer > 0f) hitCrossPopTimer -= Time.deltaTime;

            // 击杀两次快摆后收拢
            Quaternion rot = Quaternion.identity;
            if (hitCrossKill)
            {
                float decay = Mathf.Clamp01(1f - age / (hitCrossMax * 0.5f));
                rot = Quaternion.Euler(0f, 0f, Mathf.Sin(age * killWobbleSpeed) * killWobbleAngle * decay);
            }

            hitCross.rectTransform.localScale = Vector3.one * popScale;
            hitCross.rectTransform.localRotation = rot;
            hitCrossGlow.rectTransform.localScale = hitCross.rectTransform.localScale;
            hitCrossGlow.rectTransform.localRotation = rot;

            SetCrossAlpha(alpha);
        }

        // 击杀放射线外放收缩
        if (killBurstTimer > 0f)
        {
            killBurstTimer -= Time.deltaTime;
            float p = Mathf.Clamp01(1f - killBurstTimer / killBurstLife);

            killBurst.rectTransform.localScale = Vector3.one * (0.45f + 1.05f * Mathf.Sin(p * Mathf.PI));

            Color bc = killBurst.color;
            bc.a = 1f - p;
            killBurst.color = bc;

            if (killBurstTimer <= 0f) killBurst.gameObject.SetActive(false);
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

        // 跳字侧跳上抛回落淡出
        for (int i = 0; i < jumpTexts.Length; i++)
        {
            if (jumpTimers[i] <= 0f) continue;

            jumpTimers[i] -= Time.deltaTime;
            float k = Mathf.Clamp01(jumpTimers[i] / jumpTextLife);
            float age = jumpTextLife - jumpTimers[i];

            // 侧向飞出，纵向受重力回落
            float dy = jumpVel[i].y * age - 0.5f * jumpTextGravity * age * age;
            jumpTexts[i].rectTransform.position = jumpStart[i] + new Vector2(jumpVel[i].x * age, dy);

            // 出现时先缩小再弹出
            float popT = Mathf.Clamp01(age / jumpTextPop);
            jumpTexts[i].rectTransform.localScale = Vector3.one
                * (Mathf.Lerp(0.6f, 1f, popT) + Mathf.Sin(popT * Mathf.PI) * jumpOvershoot[i]);

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

        // 积分列排布
        UpdateScoreAdds();

        // 圆弧计时淡出
        if (arcTimer > 0f)
        {
            arcTimer -= Time.deltaTime;
            SetArcAlpha(arcAlpha * Mathf.Clamp01(arcTimer / arcLife));
        }
    }

    // 显示伤害跳字，kill为是否击杀，hitCenter与hitSize为命中体包围盒
    void ShowJumpText(int damage, bool kill, Vector3 hitPoint, Vector3 hitCenter, Vector3 hitSize)
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

        // 落点取身体外侧，半个包围盒宽加间距
        Vector3 body = cam.WorldToScreenPoint(hitCenter);
        float halfSize = Mathf.Max(hitSize.x, hitSize.z) * 0.5f;
        float halfW = Mathf.Abs(cam.WorldToScreenPoint(hitCenter + cam.transform.right * halfSize).x - body.x);

        float dir = start.x - focus.position.x >= 0f ? 1f : -1f;
        float landX = body.z > 0f ? body.x + dir * (halfW + jumpTextGap) : start.x + dir * jumpTextGap;

        TextMeshProUGUI t = jumpTexts[i];
        t.text = damage.ToString();
        t.fontSize = kill ? jumpTextKillSize : jumpTextSize;
        t.color = kill ? killColor : damage > 0 ? hitColor : deadColor;
        t.rectTransform.position = start;
        t.rectTransform.localScale = Vector3.one * 0.6f;
        t.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-jumpTextTilt, jumpTextTilt));
        t.gameObject.SetActive(true);

        jumpStart[i] = start;
        jumpVel[i] = new Vector2((landX - start.x) / jumpTextLife,
            Mathf.Sqrt(2f * jumpTextGravity * jumpTextRise));
        jumpOvershoot[i] = kill ? jumpTextKillOvershoot : jumpTextOvershoot;
        jumpTimers[i] = jumpTextLife;
    }

    // 设置X透明度
    void SetCrossAlpha(float alpha)
    {
        Color c = hitCross.color;
        c.a = alpha;
        hitCross.color = c;

        Color g = hitCrossGlow.color;
        g.a = alpha * hitCrossGlowAlpha;
        hitCrossGlow.color = g;

        bool visible = alpha > 0.001f;   //显示阈值
        if (hitCross.gameObject.activeSelf != visible) hitCross.gameObject.SetActive(visible);

        bool glowVisible = visible && hitCrossKill;
        if (hitCrossGlow.gameObject.activeSelf != glowVisible) hitCrossGlow.gameObject.SetActive(glowVisible);
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

    // 生成命中X与击杀放射线
    Image MakeCross()
    {
        GameObject glowGo = new GameObject("HitCrossGlow", typeof(Image));
        glowGo.transform.SetParent(canvas.transform, false);

        hitCrossGlow = glowGo.GetComponent<Image>();
        hitCrossGlow.sprite = GetCrossSprite();
        hitCrossGlow.color = killColor;
        hitCrossGlow.raycastTarget = false;
        hitCrossGlow.rectTransform.sizeDelta = Vector2.one * (hitCrossSize * hitCrossGlowScale / canvas.scaleFactor);
        glowGo.SetActive(false);

        GameObject burstGo = new GameObject("KillBurst", typeof(Image));
        burstGo.transform.SetParent(canvas.transform, false);

        killBurst = burstGo.GetComponent<Image>();
        killBurst.sprite = GetBurstSprite();
        killBurst.color = killColor;
        killBurst.raycastTarget = false;
        killBurst.rectTransform.sizeDelta = Vector2.one * (killBurstSize / canvas.scaleFactor);
        burstGo.SetActive(false);

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

    // 生成放射线贴图
    Sprite GetBurstSprite()
    {
        if (burstSprite != null) return burstSprite;

        int size = 128;
        float half = size * 0.5f;
        float inner = size * 0.16f;   //内圈留空
        float line = size * 0.05f;    //线宽
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 offset = new Vector2(x + 0.5f - half, y + 0.5f - half);
                float d = offset.magnitude;

                // 到最近一条放射线的夹角
                float ang = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
                float seg = Mathf.Repeat(ang + 30f, 60f) - 30f;

                // 线宽由内向外收细
                float thickness = Mathf.Lerp(line, line * 0.25f, Mathf.InverseLerp(inner, half, d));
                float halfAngle = Mathf.Atan(thickness / Mathf.Max(d, 1f)) * Mathf.Rad2Deg;

                bool inside = d >= inner && d <= half && Mathf.Abs(seg) <= halfAngle;
                tex.SetPixel(x, y, inside ? Color.white : Color.clear);
            }
        }

        tex.Apply();
        burstSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        return burstSprite;
    }

    // 生成跳字池
    void MakeJumpTexts()
    {
        jumpTexts = new TextMeshProUGUI[jumpTextCount];
        jumpTimers = new float[jumpTextCount];
        jumpStart = new Vector2[jumpTextCount];
        jumpVel = new Vector2[jumpTextCount];
        jumpOvershoot = new float[jumpTextCount];

        for (int i = 0; i < jumpTextCount; i++)
        {
            GameObject go = new GameObject("JumpText" + i, typeof(TextMeshProUGUI));
            go.transform.SetParent(canvas.transform, false);

            TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
            if (uiFont != null) t.font = uiFont;
            t.fontSize = jumpTextSize;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            t.rectTransform.sizeDelta = new Vector2(200f, 80f);

            // 描边提升数字可读性
            t.outlineWidth = jumpTextOutline;
            t.outlineColor = jumpTextOutlineColor;
            t.fontMaterial.EnableKeyword("OUTLINE_ON");

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
        if (uiFont != null) scoreText.font = uiFont;
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
        scoreAddY = new float[scoreAddCount];
        scoreAddSeq = new int[scoreAddCount];

        for (int i = 0; i < scoreAddCount; i++)
        {
            GameObject go = new GameObject("ScoreAdd" + i, typeof(TextMeshProUGUI));
            go.transform.SetParent(canvas.transform, false);

            TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
            if (uiFont != null) t.font = uiFont;
            t.fontSize = scoreAddFontSize;
            t.alignment = TextAlignmentOptions.Left;
            t.raycastTarget = false;
            t.color = scoreAddColor;
            t.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            t.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            t.rectTransform.pivot = new Vector2(0f, 0.5f);
            t.rectTransform.sizeDelta = new Vector2(240f, 60f);
            go.SetActive(false);

            scoreAdds[i] = t;
        }
    }

    // 显示加分条目，delta为加分，color为颜色
    void ShowScoreAdd(int delta, Color color)
    {
        if (scoreAdds == null || scoreAdds.Length == 0)
        {
            Debug.LogError("Hit_Feedback_UI|ShowScoreAdd|加分池未生成");
            return;
        }
        if (delta < 0) return;    //零分也要提示

        int i = scoreAddIndex;
        scoreAddIndex = (scoreAddIndex + 1) % scoreAdds.Length;

        scoreAddCounter++;
        scoreAddSeq[i] = scoreAddCounter;

        TextMeshProUGUI t = scoreAdds[i];
        t.text = "+" + delta;
        t.color = color;
        t.rectTransform.localScale = Vector3.one;
        t.gameObject.SetActive(true);

        // 从列上方滑入
        scoreAddY[i] = focus.position.y + scoreAddDown + scoreAddGap;
        t.rectTransform.position = new Vector3(focus.position.x + scoreAddSide, scoreAddY[i], 0f);

        scoreAddTimers[i] = scoreAddLife;
    }

    // 加分列排布与淡出，贴准星右侧，新条目在上旧条目往下推
    void UpdateScoreAdds()
    {
        for (int i = 0; i < scoreAdds.Length; i++)
        {
            if (scoreAddTimers[i] <= 0f) continue;

            scoreAddTimers[i] -= Time.deltaTime;

            // 排在本条之上且还活着的条数
            int above = 0;
            for (int j = 0; j < scoreAdds.Length; j++)
                if (scoreAddTimers[j] > 0f && scoreAddSeq[j] > scoreAddSeq[i]) above++;

            float targetY = focus.position.y + scoreAddDown - above * scoreAddGap;
            scoreAddY[i] = Mathf.MoveTowards(scoreAddY[i], targetY, scoreAddSlide * Time.deltaTime);

            scoreAdds[i].rectTransform.position = new Vector3(focus.position.x + scoreAddSide, scoreAddY[i], 0f);

            Color c = scoreAdds[i].color;
            c.a = Mathf.Clamp01(scoreAddTimers[i] / scoreAddLife);
            scoreAdds[i].color = c;

            if (scoreAddTimers[i] <= 0f) scoreAdds[i].gameObject.SetActive(false);
        }
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
