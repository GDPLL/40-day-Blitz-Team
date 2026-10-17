using UnityEngine;
using UnityEngine.UI;

// 本机弹夹与子弹表现，由 UI_Player 驱动
public class UI_Magazine : MonoBehaviour
{
    const int Magazine_Max = 6;     // 弹夹显示上限
    const int Stack_Max = 30;       // 弹夹内显示的子弹堆上限
    const int Bullet_Max = 32;      // 子弹动画池大小

    [Header("弹夹")]
    public float magazineHeight = 190f;     // 当前弹夹展开高度，像素
    public float reserveHeight = 100f;      // 备用弹夹高度，像素
    public float magazineGap = 5f;          // 弹夹间距，像素
    public Vector2 panelOffset = new Vector2(50f, 112f);    // 面板左下位置
    public float activeAlpha = 0.45f;       // 在用弹夹外壳透明度
    public float idleAlpha = 0.28f;         // 备用弹夹透明度
    public float moveSmooth = 14f;          // 顶替移动速度
    public float fadeTime = 0.18f;          // 透明度过渡时长，秒
    public Color activeColor = new Color(0.93f, 0.97f, 0.90f, 1f);  // 弹夹剪影色
    public Color outlineColor = new Color(0.10f, 0.11f, 0.08f, 0.55f); // 子弹轮廓色
    public float bulletOutline = 1.0f;      // 子弹轮廓厚度，像素
    public float stackTilt = 0f;            // 内胆倾斜角度，度（直弹夹为0）
    public float stackOffsetX = 0f;         // 内胆底部水平偏移，像素
    public float stackKickDistance = 5f;    // 弹堆上顶距离，像素
    public float stackKickTime = 0.09f;     // 弹堆回弹时长，秒

    [Header("换弹")]
    public float discardLife = 0.45f;       // 丢弃弹夹时长，秒
    public float discardSpin = -260f;       // 丢弃旋转速度，度每秒
    public Vector2 discardVelocity = new Vector2(-90f, 20f);    // 丢弃初速度
    public float discardGravity = -900f;    // 丢弃重力

    [Header("射击")]
    public float bulletLife = 1.6f;         // 子弹最长存活，秒
    public float bulletUpSpeed = 240f;      // 子弹向上初速度
    public float bulletSideSpeed = 90f;     // 子弹左右散开速度
    public float bulletGravity = -700f;     // 子弹重力
    public float bulletSpin = 360f;         // 子弹自转速度，方向随机
    public float bulletPressTime = 0.08f;   // 弹出前上顶时长，秒
    public float bulletPressDistance = 24f; // 弹出前上顶距离，像素
    public float bulletWidth = 40f;         // 子弹显示宽度，像素
    public float bulletOffscreenMargin = 24f;   // 掉出屏幕后的额外余量，像素
    public Color bulletColor = new Color(0.98f, 0.82f, 0.25f, 1f);  // 子弹剪影色

    // 一个弹夹的显示状态
    class Magazine_View
    {
        public Image baseImage;         // 半透明底图
        public RectTransform rect;      // 弹夹容器
        public RectTransform stackWindow;   // 内胆窗口
        public Vector2 stackBasePos;    // 内胆初始位置
        public Image[] stackBullets;    // 内胆子弹
        public Image[] stackOutlines;   // 内胆子弹轮廓
        public CanvasGroup group;       // 丢弃整体淡出
        public float stackKick;         // 上顶强度
        public Vector2 targetPos;       // 目标位置
        public float targetScale;       // 目标缩放
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
        public Image outline;           // 子弹轮廓
        public RectTransform rect;      // 子弹位置
        public Vector2 pos;             // 当前位置
        public Vector2 vel;             // 当前速度
        public Vector2 pressStart;      // 上顶起点
        public float pressTimer;        // 上顶剩余时长
        public float startAngle;        // 起始角度
        public float angle;             // 当前角度
        public float spin;              // 当前自转速度
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
    float reserveScale;             // 备用弹夹缩放
    float stackInset;               // 内胆距弹夹底部
    float innerHeight;              // 内胆子弹高度
    float innerSpacing;             // 内胆子弹间距
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
        reserveScale = Mathf.Clamp01(reserveHeight / Mathf.Max(1f, magazineHeight));

        root = New_Rect("弹夹", transform);
        root.anchorMin = new Vector2(0f, 0f);
        root.anchorMax = new Vector2(0f, 0f);
        root.pivot = new Vector2(0f, 0f);
        root.anchoredPosition = panelOffset;
        root.sizeDelta = new Vector2(magazineWidth, magazineHeight);

        // 子弹单独一层，避免下坠时被弹药与血量条挡住
        bulletRoot = New_Rect("子弹层", transform);
        bulletRoot.anchorMin = new Vector2(0f, 0f);
        bulletRoot.anchorMax = new Vector2(0f, 0f);
        bulletRoot.pivot = new Vector2(0f, 0f);
        bulletRoot.anchoredPosition = panelOffset;
        bulletRoot.sizeDelta = root.sizeDelta;

        // 弹夹外壳与内胆共用同一张子弹图，内胆用矩形遮罩裁切
        Material bulletMaterial = new Material(shader);
        bulletMaterial.SetColor("_Color", bulletColor);
        Material outlineMaterial = new Material(shader);
        outlineMaterial.SetColor("_Color", outlineColor);

        float stackWidth = magazineWidth * 0.78f;
        float stackHeight = magazineHeight * 0.86f;
        stackInset = magazineHeight * 0.07f;
        float innerWidth = stackWidth * 0.86f;
        innerHeight = innerWidth * bulletTex.height / bulletTex.width;
        float stackPad = bulletOutline + 0.5f;
        innerSpacing = (stackHeight - innerHeight - stackPad * 2f) / (Stack_Max - 1);

        // 弹夹图，每个弹夹由半透明外壳和内胆子弹组成
        for (int i = 0; i < Magazine_Max; i++)
        {
            RectTransform slot = New_Rect($"弹夹{i + 1}", root);
            slot.anchorMin = new Vector2(0f, 0f);
            slot.anchorMax = new Vector2(0f, 0f);
            slot.pivot = new Vector2(0.5f, 0f);
            slot.sizeDelta = new Vector2(magazineWidth, magazineHeight);
            slot.anchoredPosition = Layout_Pos(i);
            slot.localScale = Vector3.one * (i == 0 ? 1f : reserveScale);

            Image baseImage = New_Image("底图", magazineSprite,
                new Vector2(magazineWidth, magazineHeight), new Vector2(0.5f, 0f), slot);
            baseImage.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            baseImage.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            baseImage.rectTransform.anchoredPosition = Vector2.zero;
            baseImage.material = new Material(shader);
            baseImage.enabled = false;

            CanvasGroup group = slot.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = false;
            group.interactable = false;

            RectTransform stackWindow = New_Rect("内胆", slot);
            stackWindow.anchorMin = new Vector2(0.5f, 0f);
            stackWindow.anchorMax = new Vector2(0.5f, 0f);
            stackWindow.pivot = new Vector2(0.5f, 0f);
            stackWindow.sizeDelta = new Vector2(stackWidth, stackHeight);
            float stackOffset = stackOffsetX * magazineHeight / 300f;
            stackWindow.anchoredPosition = new Vector2(stackOffset, stackInset);
            stackWindow.localRotation = Quaternion.Euler(0f, 0f, stackTilt);
            stackWindow.gameObject.AddComponent<RectMask2D>();

            Image[] stackBullets = new Image[Stack_Max];
            Image[] stackOutlines = new Image[Stack_Max];
            for (int j = 0; j < Stack_Max; j++)
            {
                Vector2 stackPos = new Vector2(
                    0f, stackPad + innerHeight * 0.5f + j * innerSpacing);

                Image stackOutline = New_Image($"内弹轮廓{j + 1}", bulletSprite,
                    new Vector2(innerWidth + bulletOutline * 2f, innerHeight + bulletOutline * 2f),
                    new Vector2(0.5f, 0.5f), stackWindow);
                stackOutline.rectTransform.anchorMin = new Vector2(0.5f, 0f);
                stackOutline.rectTransform.anchorMax = new Vector2(0.5f, 0f);
                stackOutline.rectTransform.anchoredPosition = stackPos;
                stackOutline.material = outlineMaterial;
                stackOutline.enabled = false;
                stackOutlines[j] = stackOutline;

                Image stackBullet = New_Image($"内弹{j + 1}", bulletSprite,
                    new Vector2(innerWidth, innerHeight), new Vector2(0.5f, 0.5f), stackWindow);
                stackBullet.rectTransform.anchorMin = new Vector2(0.5f, 0f);
                stackBullet.rectTransform.anchorMax = new Vector2(0.5f, 0f);
                stackBullet.rectTransform.anchoredPosition = stackPos;
                stackBullet.material = bulletMaterial;
                stackBullet.enabled = false;
                stackBullets[j] = stackBullet;
            }

            stackWindow.gameObject.SetActive(false);

            magazineViews[i] = new Magazine_View
            {
                baseImage = baseImage,
                rect = slot,
                stackWindow = stackWindow,
                stackBasePos = new Vector2(stackOffset, stackInset),
                stackBullets = stackBullets,
                stackOutlines = stackOutlines,
                group = group,
                alpha = idleAlpha
            };
            Set_Color(baseImage, activeColor, idleAlpha);
        }

        // 子弹图池
        float bulletHeight = bulletWidth * bulletTex.height / bulletTex.width;
        Image[] bulletOutlines = new Image[Bullet_Max];

        // 先建所有轮廓，保证轮廓都在弹头下层
        for (int i = 0; i < Bullet_Max; i++)
        {
            Image outline = New_Image($"子弹轮廓{i + 1}", bulletSprite,
                new Vector2(bulletWidth + bulletOutline * 2f, bulletHeight + bulletOutline * 2f),
                new Vector2(0.5f, 0.5f), bulletRoot);
            outline.material = outlineMaterial;
            outline.enabled = false;
            bulletOutlines[i] = outline;
        }

        for (int i = 0; i < Bullet_Max; i++)
        {
            Image image = New_Image($"子弹{i + 1}", bulletSprite,
                new Vector2(bulletWidth, bulletHeight), new Vector2(0.5f, 0.5f), bulletRoot);
            image.material = bulletMaterial;
            image.enabled = false;

            bulletViews[i] = new Bullet_View
            {
                image = image,
                outline = bulletOutlines[i],
                rect = image.rectTransform
            };
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

        Layout_Root_Update();

        // 刷新目标位置与透明度
        for (int i = 0; i < activeCount; i++)
        {
            activeViews[i].targetPos = Layout_Pos(i);
            activeViews[i].targetScale = Layout_Scale(i);
            activeViews[i].targetAlpha = i == 0 ? activeAlpha : idleAlpha;

            bool showStack = true;
            if (activeViews[i].stackWindow.gameObject.activeSelf != showStack)
            {
                activeViews[i].stackWindow.gameObject.SetActive(showStack);
                if (showStack)
                    activeViews[i].stackWindow.anchoredPosition = activeViews[i].stackBasePos;
            }
        }

        // 弹药下降时弹出子弹，一帧最多补两发
        if (lastAmmo >= 0 && ammo < lastAmmo)
        {
            int shotCount = Mathf.Min(lastAmmo - ammo, 2);
            for (int i = 0; i < shotCount; i++) Bullet_Spawn();
            if (activeCount > 0) activeViews[0].stackKick = 1f;
        }

        // 当前弹夹内胆按剩余子弹数量从底部往上显示
        Magazine_Stack_Update(ammo, maxAmmo, isReload);

        lastAmmo = ammo;
        lastMagazine = magazineCount;

        float dt = Time.deltaTime;
        Magazine_Update(dt);
        Bullet_Update(dt);
    }

    // 更新当前弹夹内胆的子弹数量
    void Magazine_Stack_Update(int ammo, int maxAmmo, bool isReload)
    {
        if (activeCount <= 0) return;

        // 换弹期间新弹夹按满弹显示，换弹取消后回到原弹药数量
        float ratio = isReload
            ? 1f
            : (maxAmmo > 0 ? Mathf.Clamp01((float)ammo / maxAmmo) : 0f);

        int currentCount = Mathf.Clamp(Mathf.CeilToInt(ratio * Stack_Max), 0, Stack_Max);

        for (int viewIndex = 0; viewIndex < activeCount; viewIndex++)
        {
            // 当前弹夹按剩余弹药，备用弹夹显示满弹堆
            int count = viewIndex == 0 ? currentCount : Stack_Max;
            int start = Stack_Max - count;
            Magazine_View view = activeViews[viewIndex];

            for (int i = 0; i < Stack_Max; i++)
            {
                bool show = i >= start;
                view.stackBullets[i].enabled = show;
                view.stackOutlines[i].enabled = show;
            }
        }
    }

    // 按数量重建弹夹显示，用于首次显示与数量回升
    void Rebuild_Active(int count)
    {
        activeCount = count;
        Layout_Root_Update();

        for (int i = 0; i < Magazine_Max; i++)
        {
            Magazine_View view = magazineViews[i];
            view.discarding = false;
            view.targetPos = Layout_Pos(i);
            view.targetScale = Layout_Scale(i);
            view.targetAlpha = i < count
                ? (i == 0 ? activeAlpha : idleAlpha)
                : idleAlpha;
            view.alpha = view.targetAlpha;
            view.rect.anchoredPosition = view.targetPos;
            view.rect.localScale = Vector3.one * view.targetScale;
            view.rect.localRotation = Quaternion.identity;
            view.baseImage.enabled = i < count;
            view.group.alpha = 1f;
            view.stackKick = 0f;
            view.stackWindow.anchoredPosition = view.stackBasePos;
            view.stackWindow.gameObject.SetActive(i < count);
            for (int j = 0; j < Stack_Max; j++)
            {
                view.stackBullets[j].enabled = i < count;
                view.stackOutlines[j].enabled = i < count;
            }
            Set_Color(view.baseImage, activeColor, view.alpha);

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
            view.rect.localScale = Vector3.Lerp(
                view.rect.localScale, Vector3.one * view.targetScale, move);
            view.alpha = Mathf.Lerp(view.alpha, view.targetAlpha, fade);
            Set_Color(view.baseImage, activeColor, view.alpha);

            // 弹堆先向上顶，再回弹
            if (i == 0)
            {
                if (view.stackKick > 0f)
                {
                    view.stackKick = Mathf.MoveTowards(
                        view.stackKick, 0f, dt / Mathf.Max(0.01f, stackKickTime));
                    view.stackWindow.anchoredPosition = view.stackBasePos
                        + Vector2.up * (stackKickDistance * view.stackKick);
                }
                else view.stackWindow.anchoredPosition = view.stackBasePos;
            }
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
        view.stackWindow.gameObject.SetActive(true);
        view.stackWindow.anchoredPosition = view.stackBasePos;
        view.stackKick = 0f;
        view.group.alpha = 1f;
        view.rect.localRotation = Quaternion.identity;
        Set_Color(view.baseImage, activeColor, view.alpha);
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
        view.group.alpha = 1f - progress;

        if (view.discardTimer <= 0f)
        {
            view.discarding = false;
            view.baseImage.enabled = false;
            view.stackWindow.gameObject.SetActive(false);
            view.group.alpha = 1f;
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

        // 从当前最上方那发内胆子弹的位置向上顶出
        Magazine_View active = activeViews[0];
        int topIndex = 0;
        for (int i = Stack_Max - 1; i >= 0; i--)
        {
            if (!active.stackBullets[i].enabled) continue;
            topIndex = i;
            break;
        }

        Vector3 topWorld = active.stackBullets[topIndex].rectTransform.TransformPoint(Vector3.zero);
        Vector3 topLocal = bulletRoot.InverseTransformPoint(topWorld);
        Vector2 start = new Vector2(topLocal.x, topLocal.y);

        view.timer = bulletLife;
        view.pressTimer = bulletPressTime;
        view.pressStart = start;
        view.pos = start;
        view.startAngle = Mathf.DeltaAngle(0f, active.stackWindow.localEulerAngles.z);

        float side = Random.Range(bulletSideSpeed * 0.35f, bulletSideSpeed);
        if (Random.value < 0.5f) side = -side;
        view.vel = new Vector2(side, bulletUpSpeed);
        view.angle = Mathf.Atan2(view.vel.y, view.vel.x) * Mathf.Rad2Deg;
        view.spin = bulletSpin * Random.Range(0.75f, 1.25f)
            * (Random.value < 0.5f ? -1f : 1f);
        view.rect.anchoredPosition = view.pos;
        view.rect.localRotation = Quaternion.Euler(0f, 0f, view.startAngle);
        view.outline.rectTransform.anchoredPosition = view.pos;
        view.outline.rectTransform.localRotation = Quaternion.Euler(0f, 0f, view.startAngle);
        view.image.enabled = true;
        view.outline.enabled = true;
    }

    // 子弹上抛、旋转，持续下坠到屏幕外
    void Bullet_Update(float dt)
    {
        for (int i = 0; i < Bullet_Max; i++)
        {
            Bullet_View view = bulletViews[i];
            if (view.timer <= 0f) continue;

            // 先向上顶起，再随机向左或向右弹出
            if (view.pressTimer > 0f)
            {
                view.pressTimer -= dt;
                float t = 1f - Mathf.Clamp01(
                    view.pressTimer / Mathf.Max(0.001f, bulletPressTime));
                float press = 1f - (1f - t) * (1f - t);
                view.pos = view.pressStart + Vector2.up * (bulletPressDistance * press);
                view.rect.anchoredPosition = view.pos;
                view.rect.localRotation = Quaternion.Euler(
                    0f, 0f, Mathf.Lerp(view.startAngle, view.angle, press));
                view.outline.rectTransform.anchoredPosition = view.pos;
                view.outline.rectTransform.localRotation = view.rect.localRotation;

                continue;
            }

            view.timer -= dt;
            view.vel.y += bulletGravity * dt;
            view.pos += view.vel * dt;
            view.angle += view.spin * dt;

            view.rect.anchoredPosition = view.pos;
            view.rect.localRotation = Quaternion.Euler(0f, 0f, view.angle);
            view.outline.rectTransform.anchoredPosition = view.pos;
            view.outline.rectTransform.localRotation = view.rect.localRotation;

            // 子弹一路保持实色，掉出屏幕底部后再回收
            float offscreenY = -panelOffset.y - bulletOffscreenMargin;

            if (view.pos.y < offscreenY || view.timer <= 0f)
            {
                view.timer = 0f;
                view.image.enabled = false;
                view.outline.enabled = false;
            }
        }
    }

    // 当前弹夹保持展开，备用弹夹缩成短图标
    Vector2 Layout_Pos(int index)
    {
        if (index <= 0) return new Vector2(magazineWidth * 0.5f, 0f);

        float reserveWidth = magazineWidth * reserveScale;
        return new Vector2(
            magazineWidth + magazineGap
            + (index - 1) * (reserveWidth + magazineGap)
            + reserveWidth * 0.5f,
            0f);
    }

    // 第几个弹夹的缩放
    float Layout_Scale(int index)
        => index == 0 ? 1f : reserveScale;

    // 当前弹夹与其后备弹夹的总宽度
    float Layout_Width(int count)
    {
        if (count <= 0) return magazineWidth;

        float reserveWidth = magazineWidth * reserveScale;
        return magazineWidth + (count - 1) * (reserveWidth + magazineGap);
    }

    // 同步根节点宽度，子弹层使用同一坐标系
    void Layout_Root_Update()
    {
        root.sizeDelta = new Vector2(Layout_Width(activeCount), magazineHeight);
        bulletRoot.sizeDelta = root.sizeDelta;
    }

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
