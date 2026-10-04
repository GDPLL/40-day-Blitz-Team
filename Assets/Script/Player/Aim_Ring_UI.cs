using UnityEngine;
using UnityEngine.UI;

// 瞄准圈显示，只有本地玩家运行
public class Aim_Ring_UI : MonoBehaviour
{
    Image outerRing;      // 外圈
    Image currentRing;    // 当前精度圈
    Image innerRing;      // 内圈

    Gun_Control gun;      // 本机枪械
    RectTransform focus;  // 准星
    Camera cam;           // 本机相机
    Canvas canvas;        // 圈所在画布
    Sprite ringSprite;    // 圆环贴图

    // 初始化，绑定本机枪械与准星
    public void Aim_Ring_UI_Init(Gun_Control gunControl, RectTransform uiFocus, Camera camera)
    {
        gun = gunControl;
        focus = uiFocus;
        cam = camera;

        if (gun == null)
        {
            Debug.LogError("Aim_Ring_UI|Aim_Ring_UI_Init|Gun_Control 为空");
            return;
        }
        if (focus == null)
        {
            Debug.LogError("Aim_Ring_UI|Aim_Ring_UI_Init|准星 RectTransform 为空");
            return;
        }
        if (cam == null)
        {
            Debug.LogError("Aim_Ring_UI|Aim_Ring_UI_Init|相机为空");
            return;
        }

        canvas = focus.GetComponentInParent<Canvas>();   //圈与准星同画布
        if (canvas == null)
        {
            Debug.LogError("Aim_Ring_UI|Aim_Ring_UI_Init|准星不在画布下");
            return;
        }

        outerRing = MakeRing("OuterRing", new Color(1f, 1f, 1f, 0.25f), 300f);
        currentRing = MakeRing("CurrentRing", new Color(1f, 1f, 1f, 0.8f), 220f);
        innerRing = MakeRing("InnerRing", new Color(1f, 0.8f, 0.2f, 0.9f), 120f);

        Debug.Log("Aim_Ring_UI|Aim_Ring_UI_Init|完成初始化");
    }

    // 本机每帧绘制瞄准圈
    public void Aim_Ring_UI_Show()
    {
        if (gun == null || focus == null || cam == null || canvas == null) return;

        // 外圈画在准星上
        outerRing.rectTransform.position = focus.position;
        SetDiameter(outerRing, gun.OuterAngle);

        // 内圈与当前圈画在锁定点上
        Vector3 p = gun.HasTarget ? cam.WorldToScreenPoint(gun.TargetPos) : Vector3.zero;
        bool show = gun.HasTarget;

        if (p.z <= 0f) p = focus.position;   //锁定点在相机后方时画准星上
        else p = ClampToRing(p);             //锁定点超出外圈时收到圈边上

        innerRing.gameObject.SetActive(show);
        currentRing.gameObject.SetActive(show);

        if (!show) return;

        innerRing.rectTransform.position = p;
        currentRing.rectTransform.position = p;

        SetDiameter(innerRing, gun.InnerAngle);
        SetDiameter(currentRing, gun.CurrentAngle);
    }

    // 把锁定点收在外圈以内
    Vector3 ClampToRing(Vector3 p)
    {
        float radius = Mathf.Tan(gun.OuterAngle * Mathf.Deg2Rad * 0.5f)
                     / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * Screen.height * 0.5f;

        Vector2 offset = p - focus.position;
        if (offset.magnitude <= radius) return p;

        return focus.position + (Vector3)(offset.normalized * radius);
    }

    // 按角度设置圈的直径
    void SetDiameter(Image ring, float angleDeg)
    {
        float pixel = Mathf.Tan(angleDeg * Mathf.Deg2Rad * 0.5f)
                    / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad)
                    * Screen.height;

        ring.rectTransform.sizeDelta = Vector2.one * (pixel / canvas.scaleFactor);
    }

    // 生成一个圈
    Image MakeRing(string ringName, Color color, float diameter)
    {
        GameObject go = new GameObject(ringName, typeof(Image));
        go.transform.SetParent(canvas.transform, false);

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
