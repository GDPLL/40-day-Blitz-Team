using UnityEngine;
using UnityEngine.UI;

// 本机人形状态图，由 Player_Main 驱动
public class UI_Body : MonoBehaviour
{
    //姿态编号，与姿态图数组顺序一致
    const int Pose_Stand = 0;       //站立
    const int Pose_Walk = 1;        //行走
    const int Pose_Run = 2;         //冲刺
    const int Pose_Squat = 3;       //蹲下静止
    const int Pose_SquatWalk = 4;   //蹲下移动
    const int Pose_Death = 5;       //死亡
    const int Pose_GunDown = 6;     //放枪
    const int Pose_GunUp = 7;       //举枪
    const int Pose_Air = 8;         //空中，待补图

    public Image bodyImage;             //人形图
    public Sprite[] poseSprites;        //姿态图，按姿态编号
    public Vector2[] poseSize;          //各姿态显示尺寸
    public Vector2[] poseOffset;        //各姿态位置微调
    public float heavyHp = 30f;         //低血量线
    public float flashSpeed = 4f;       //红闪频率，次每秒
    public float damageLife = 0.25f;    //受伤闪时长，秒
    public float lowerLife = 0.35f;     //放枪过渡时长，秒

    Material mat;                       //人形材质
    int lastPose = -1;                  //上次姿态编号
    int lastHp = -1;                    //上次血量
    float damageTimer;                  //受伤闪剩余
    float lowerTimer;                   //放枪过渡剩余
    bool lastGunUp;                     //上次是否举枪
    bool isReady;                       //引用就绪

    // 初始化，建人形材质
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

        Shader shader = Resources.Load<Shader>("UI_Body");
        if (shader == null) shader = Shader.Find("UI/Body");
        if (shader == null)
        {
            Debug.LogError("UI_Body|UI_Body_Init|未找到 UI_Body 着色器");
            return;
        }

        mat = new Material(shader);
        bodyImage.material = mat;
        isReady = true;

        Debug.Log("UI_Body|UI_Body_Init|完成初始化");
    }

    // 按血量与状态位刷新，state 为表现状态位
    public void UI_Body_Show(int hp, int maxHp, ushort state)
    {
        if (!isReady) return;

        bool dead = (state & Player_animation.BitDeath) != 0 || hp <= 0;

        //松掉举枪键起一段放枪过渡
        bool gunUp = (state & Player_animation.BitGun) != 0;
        if (lastGunUp && !gunUp) lowerTimer = lowerLife;
        lastGunUp = gunUp;
        if (lowerTimer > 0f) lowerTimer -= Time.deltaTime;

        //姿态切换
        int pose = Pose_Index(state, dead, gunUp);
        if (pose != lastPose) Set_Pose(pose);

        //血量下降起一次受伤闪
        if (lastHp >= 0 && hp < lastHp) damageTimer = damageLife;
        lastHp = hp;

        float damage = 0f;
        if (damageTimer > 0f)
        {
            damageTimer -= Time.deltaTime;
            damage = Mathf.Clamp01(damageTimer / damageLife);
        }

        //喂材质参数
        mat.SetColor("_Color", Hp_Color(hp, maxHp, dead, damage));
        mat.SetFloat("_Damage", damage);
    }

    // 按状态位取姿态编号，死亡优先
    int Pose_Index(ushort state, bool dead, bool gunUp)
    {
        if (dead) return Fallback_Index(Pose_Death);

        //举枪覆盖身体姿态
        if (gunUp) return Fallback_Index(Pose_GunUp);

        //放枪过渡
        if (lowerTimer > 0f) return Fallback_Index(Pose_GunDown);

        //蹲下分静止与移动
        if ((state & Player_animation.BitSquat) != 0)
            return Fallback_Index((state & Player_animation.BitWalk) != 0 ? Pose_SquatWalk : Pose_Squat);

        if ((state & Player_animation.BitRun) != 0) return Fallback_Index(Pose_Run);
        if ((state & Player_animation.BitWalk) != 0) return Fallback_Index(Pose_Walk);

        return Pose_Stand;
    }

    // 缺图回退，取最近可用姿态
    int Fallback_Index(int index)
    {
        //缺图时退到相近姿态
        if (!Has_Sprite(index))
        {
            Debug.LogError($"UI_Body|Fallback_Index|姿态图缺失 下标{index}");

            //举枪退放枪，蹲下移动退蹲下静止
            if (index == Pose_GunUp) index = Pose_GunDown;
            else if (index == Pose_SquatWalk) index = Pose_Squat;
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

        if (poseSize != null && index < poseSize.Length)
        {
            //尺寸为零视为漏配
            if (poseSize[index].x <= 0f || poseSize[index].y <= 0f)
                Debug.LogError($"UI_Body|Set_Pose|姿态尺寸为零 下标{index}");
            else bodyImage.rectTransform.sizeDelta = poseSize[index];
        }

        if (poseOffset != null && index < poseOffset.Length)
            bodyImage.rectTransform.anchoredPosition = poseOffset[index];
    }

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
