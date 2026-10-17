using UnityEngine;

// 玩家音效表现，各端本地播放
public class Player_Audio : MonoBehaviour
{
    // 音效编号，与 audioClips 顺序一致
    enum AudioNumKV
    {
        FireStart,      //射击-开火
        FireSingle,     //射击-单发
        FireTail,       //射击-开火尾音
        CasingDrop,     //射击-子弹落地
        FireLoop,       //射击-开火背景音
        Empty,          //射击-空仓
        RaiseGun,       //举枪
        LowerGun,       //放枪
        Reload,         //换弹
        Shoulder,       //据枪
        Ads,            //瞄准
        WalkStep,       //走路脚步
        RunStep,        //跑步脚步
        GearShake,      //装备晃动
        HitMark,        //命中目标
        Kill,           //击杀目标
        Hurt1,          //受伤1
        Hurt2,          //受伤2
        Hurt3,          //受伤3
        Hurt4,          //受伤4
        Breath,         //常态呼吸
        BreathHeavy,    //重伤呼吸
        BreathHeal,     //回血呼吸
        RespawnBreath,  //复活喘气
        Cough,          //毒气咳嗽
        Wind,           //高速风声
        Jump,           //起跳
        Land,           //坠地
        Roll,           //翻滚
    }

    public AudioClip[] audioClips;      // 音效片段，按编号取
    public AudioSource shotSource;      // 单发音源
    public AudioSource loopSource;      // 背景循环音源
    public AudioSource dropSource;      // 子弹落地音源
    public AudioSource gunSource;       // 枪械动作音源
    public AudioSource stepSource;      // 脚步音源
    public AudioSource gearSource;      // 装备晃动音源，循环
    public AudioSource hitSource;       // 命中反馈音源，一次性
    public AudioSource breathSource;    // 呼吸音源，循环
    public AudioSource coughSource;     // 咳嗽音源，循环
    public AudioSource windSource;      // 风声音源，循环

    public float tailDelay = 0.2f;      // 停火判定秒数
    public float gunMinInterval = 0.1f;     // 动作音最小间隔
    public float volumeMin = 0.9f;      // 随机音量下限
    public float volumeMax = 1.1f;      // 随机音量上限
    public int heavyHp = 30;            // 重伤呼吸血量线
    public float windMinSpeed = 8f;     // 起风速度
    public float windMaxSpeed = 20f;    // 满音量速度
    public float landSpeed = 6f;        // 坠地判定速度

    Gun_Control gun;                    // 所属枪械
    bool isFiring;                      // 连发中
    float lastShotTime;                 // 上次开火时刻
    float lastGunTime;                  // 上次动作音时刻
    int lastHp;                         // 上帧血量
    bool hasBody;                       // 生命音效已初始化
    float fallSpeed;                    // 空中最大下落速度

    // 初始化，gunControl为所属枪械
    public void Player_Audio_Init(Gun_Control gunControl)
    {
        if (gunControl == null)
        {
            Debug.LogError("Player_Audio|Player_Audio_Init|Gun_Control 为空");
            return;
        }

        gun = gunControl;
        gun.Shoot_event += OnShoot;      //开火
        gun.Empty_event += OnEmpty;      //空仓
        gun.Raise_event += OnRaise;      //举枪
        gun.Lower_event += OnLower;      //放枪
        gun.Shoulder_event += OnShoulder;    //据枪
        gun.Ads_event += OnAds;          //瞄准
        gun.Reload_event += OnReload;    //换弹

        // 背景循环音
        if (loopSource != null)
        {
            loopSource.clip = Clip(AudioNumKV.FireLoop);
            loopSource.loop = true;
        }

        // 装备晃动循环音
        if (gearSource != null)
        {
            gearSource.clip = Clip(AudioNumKV.GearShake);
            gearSource.loop = true;
        }

        // 毒气咳嗽循环音
        if (coughSource != null)
        {
            coughSource.clip = Clip(AudioNumKV.Cough);
            coughSource.loop = true;
        }

        // 高速风声循环音
        if (windSource != null)
        {
            windSource.clip = Clip(AudioNumKV.Wind);
            windSource.loop = true;
            windSource.volume = 0f;
        }
    }

    // 反注册
    void OnDestroy()
    {
        if (gun == null) return;

        gun.Shoot_event -= OnShoot;
        gun.Empty_event -= OnEmpty;
        gun.Raise_event -= OnRaise;
        gun.Lower_event -= OnLower;
        gun.Shoulder_event -= OnShoulder;
        gun.Ads_event -= OnAds;
        gun.Reload_event -= OnReload;
    }

    // 每次开火，第一发与后续分开
    void OnShoot()
    {
        if (isFiring) Play_Clip(AudioNumKV.FireSingle, Random_Volume());      //后续单发
        else
        {
            isFiring = true;
            Play_Clip(AudioNumKV.FireStart);                 //第一发
            if (loopSource != null) loopSource.Play();       //背景循环
        }

        Play_Drop();      //子弹落地
        lastShotTime = Time.time;
    }

    // 空仓，只播一次
    void OnEmpty()
    {
        Play_Clip(AudioNumKV.Empty);
    }

    // 举枪
    void OnRaise()
    {
        Play_Gun(AudioNumKV.RaiseGun);
    }

    // 放枪
    void OnLower()
    {
        Play_Gun(AudioNumKV.LowerGun);
    }

    // 据枪
    void OnShoulder()
    {
        Play_Gun(AudioNumKV.Shoulder);
    }

    // 瞄准
    void OnAds()
    {
        Play_Gun(AudioNumKV.Ads);
    }

    // 换弹
    void OnReload()
    {
        Play_Gun(AudioNumKV.Reload);
    }

    // 停火播尾音
    void Update()
    {
        if (!isFiring) return;
        if (Time.time - lastShotTime <= tailDelay) return;

        isFiring = false;
        if (loopSource != null) loopSource.Stop();
        Play_Clip(AudioNumKV.FireTail);
    }

    // 移动表现，state为表现状态位
    public void Player_Audio_Show(ushort state)
    {
        // 只在地面移动才有脚步与装备晃动
        bool move = (state & Player_animation.BitWalk) != 0 && (state & Player_animation.BitGround) != 0;
        bool run = (state & Player_animation.BitRun) != 0;

        Step_Performance(move, run);
        Gear_Performance(move, run);
    }

    // 脚步循环，走路与跑步各一段
    void Step_Performance(bool move, bool run)
    {
        if (stepSource == null) return;

        if (!move)      //停下不响
        {
            if (stepSource.isPlaying) stepSource.Stop();
            return;
        }

        AudioClip clip = Clip(run ? AudioNumKV.RunStep : AudioNumKV.WalkStep);
        if (clip == null) return;

        // 换片段才重播，避免每帧重启
        if (stepSource.clip == clip)
        {
            if (!stepSource.isPlaying) stepSource.Play();
            return;
        }

        stepSource.clip = clip;
        stepSource.loop = true;
        stepSource.Play();
    }

    // 装备晃动，跑步两倍速
    void Gear_Performance(bool move, bool run)
    {
        if (gearSource == null) return;

        if (!move)      //停下不响
        {
            if (gearSource.isPlaying) gearSource.Stop();
            return;
        }

        if (!gearSource.isPlaying) gearSource.Play();
    }

    // 风声，速度越快越响
    public void Wind_Performance(float speed)
    {
        if (windSource == null) return;

        // 低于起风速度不响
        if (speed <= windMinSpeed)
        {
            if (windSource.isPlaying) windSource.Stop();
            return;
        }

        windSource.volume = Mathf.InverseLerp(windMinSpeed, windMaxSpeed, speed);
        if (!windSource.isPlaying) windSource.Play();
    }

    // 起跳
    public void Jump_Show()
    {
        Play_Body(AudioNumKV.Jump);
    }

    // 翻滚
    public void Roll_Show()
    {
        Play_Body(AudioNumKV.Roll);
    }

    // 落地检测，vy为垂直速度，onGround为是否在地面
    public void Land_Check(float vy, bool onGround)
    {
        // 空中记录下落速度
        if (!onGround)
        {
            fallSpeed = Mathf.Max(fallSpeed, -vy);
            return;
        }

        // 触地那一下按落速判定
        if (fallSpeed > landSpeed) Play_Body(AudioNumKV.Land);
        fallSpeed = 0f;
    }

    // 命中目标
    public void Hit_Show()
    {
        Play_Body(AudioNumKV.HitMark);
    }

    // 击杀目标
    public void Kill_Show()
    {
        Play_Body(AudioNumKV.Kill);
    }

    // 受伤，四种随机
    public void Hurt_Show()
    {
        int pick = UnityEngine.Random.Range(0, 4);
        Play_Body((AudioNumKV)((int)AudioNumKV.Hurt1 + pick));
    }

    // 生命音效，hp为当前血量，healCap为回血上限，inGas为是否在毒气内
    public void Player_Audio_Body_Show(int hp, int healCap, bool inGas)
    {
        // 首次记录基准，避免开局误判复活
        if (!hasBody)
        {
            hasBody = true;
            lastHp = hp;
        }

        // 血量从零回升视为复活，喘一口气
        if (lastHp <= 0 && hp > 0) Play_Body(AudioNumKV.RespawnBreath);

        lastHp = hp;

        Breath_Performance(hp, healCap);
        Cough_Performance(inGas && hp > 0);
    }

    // 呼吸循环，血量越低越重，未满上限为恢复期
    void Breath_Performance(int hp, int healCap)
    {
        if (breathSource == null) return;

        // 死亡不呼吸
        if (hp <= 0)
        {
            if (breathSource.isPlaying) breathSource.Stop();
            return;
        }

        AudioNumKV num = hp < heavyHp ? AudioNumKV.BreathHeavy
            : hp < healCap ? AudioNumKV.BreathHeal
            : AudioNumKV.Breath;

        AudioClip clip = Clip(num);
        if (clip == null) return;

        // 换片段才重播，避免每帧重启
        if (breathSource.clip == clip)
        {
            if (!breathSource.isPlaying) breathSource.Play();
            return;
        }

        breathSource.clip = clip;
        breathSource.loop = true;
        breathSource.Play();
    }

    // 毒气里一直咳，离开即停
    void Cough_Performance(bool inGas)
    {
        if (coughSource == null) return;

        if (!inGas)
        {
            if (coughSource.isPlaying) coughSource.Stop();
            return;
        }

        if (!coughSource.isPlaying) coughSource.Play();
    }

    // 身体音播一次
    void Play_Body(AudioNumKV num)
    {
        AudioClip clip = Clip(num);
        if (clip == null || hitSource == null) return;

        hitSource.PlayOneShot(clip);
    }

    // 按编号播一次，volumeScale为音量系数
    void Play_Clip(AudioNumKV num, float volumeScale = 1f)
    {
        AudioClip clip = Clip(num);
        if (clip == null || shotSource == null) return;

        shotSource.PlayOneShot(clip, volumeScale);
    }

    // 随机音量系数
    float Random_Volume()
    {
        return UnityEngine.Random.Range(volumeMin, volumeMax);
    }

    // 子弹落地，走独立音源
    void Play_Drop()
    {
        AudioClip clip = Clip(AudioNumKV.CasingDrop);
        if (clip == null || dropSource == null) return;

        dropSource.PlayOneShot(clip, Random_Volume());
    }

    // 枪械动作，共用一个音源，新动作顶掉旧动作
    void Play_Gun(AudioNumKV num)
    {
        if (Time.time - lastGunTime < gunMinInterval) return;   //抖动防抖

        AudioClip clip = Clip(num);
        if (clip == null || gunSource == null) return;

        lastGunTime = Time.time;
        gunSource.clip = clip;
        gunSource.Play();      //顶掉上一个动作音，不叠加
    }

    // 取片段
    AudioClip Clip(AudioNumKV num)
    {
        int index = (int)num;
        if (audioClips == null || index < 0 || index >= audioClips.Length) return null;
        return audioClips[index];
    }
}
