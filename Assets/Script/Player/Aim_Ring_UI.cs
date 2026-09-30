using UnityEngine;
using UnityEngine.UI;

// 瞄准圈显示
public class Aim_Ring_UI : MonoBehaviour
{
    public static Aim_Ring_UI Instance;

    public Image outerRing;      // 外圈
    public Image currentRing;    // 当前精度圈
    public Image innerRing;      // 内圈

    Gun_Control gun;
    RectTransform focus;
    Camera cam;
    Canvas canvas;
    Sprite ringSprite;

    void Awake()
    {
        Instance = this;
        if (canvas == null) canvas = GetComponentInParent<Canvas>();

        // 没拖引用就自己建
        if (outerRing == null) outerRing = MakeRing("OuterRing", new Color(1f, 1f, 1f, 0.25f), 300f);
        if (currentRing == null) currentRing = MakeRing("CurrentRing", new Color(1f, 1f, 1f, 0.8f), 220f);
        if (innerRing == null) innerRing = MakeRing("InnerRing", new Color(1f, 0.8f, 0.2f, 0.9f), 120f);
    }

    // 绑定本地玩家的枪与准星
    public void Bind(Gun_Control gunControl, RectTransform uiFocus, Camera camera)
    {
        gun = gunControl;
        focus = uiFocus;
        cam = camera;
    }

    void Update()
    {
        Camera view = cam != null ? cam : Camera.main;
        if (gun == null || view == null || canvas == null) return;

        // 外圈画在准星上
        if (focus != null) outerRing.rectTransform.position = focus.position;
        SetDiameter(outerRing, gun.OuterAngle, view);

        // 内圈和当前圈画在锁定点上
        Vector3 p = gun.HasTarget ? view.WorldToScreenPoint(gun.TargetPos) : Vector3.zero;
        bool show = gun.HasTarget && p.z > 0f;

        innerRing.gameObject.SetActive(show);
        currentRing.gameObject.SetActive(show);

        if (show)
        {
            innerRing.rectTransform.position = p;
            currentRing.rectTransform.position = p;

            SetDiameter(innerRing, gun.InnerAngle, view);
            SetDiameter(currentRing, gun.CurrentAngle, view);
        }
    }

    // 按角度设置直径
    void SetDiameter(Image ring, float angleDeg, Camera view)
    {
        float pixel = Mathf.Tan(angleDeg * Mathf.Deg2Rad * 0.5f)
                    / Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad)
                    * Screen.height;

        ring.rectTransform.sizeDelta = Vector2.one * (pixel / canvas.scaleFactor);
    }

    // 生成一个圈
    Image MakeRing(string ringName, Color color, float diameter)
    {
        GameObject go = new GameObject(ringName, typeof(Image));
        go.transform.SetParent(transform, false);

        Image img = go.GetComponent<Image>();
        img.sprite = GetRingSprite();
        img.color = color;
        img.raycastTarget = false;
        img.rectTransform.sizeDelta = Vector2.one * diameter;

        return img;
    }

    // 生成圆环贴图
    Sprite GetRingSprite()
    {
        if (ringSprite != null) return ringSprite;

        int size = 256;
        float half = size * 0.5f;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half));
                tex.SetPixel(x, y, (d < half && d > half - 3f) ? Color.white : Color.clear);
            }
        }

        tex.Apply();
        ringSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        return ringSprite;
    }
}
