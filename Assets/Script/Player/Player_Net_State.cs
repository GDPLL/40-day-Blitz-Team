using UnityEngine;
using Unity.Netcode;

// 玩家网络数据仓库，只存同步值与打包
public class Player_Net_State : NetworkBehaviour
{
    Player_Control Own_Control; //父级
    public void Owner_Init(Player_Control control) { Own_Control = control; }

    //同步表现状态
    NetworkVariable<ushort> netShowState = new NetworkVariable<ushort>();    //表现状态位
    NetworkVariable<Vector3> netAimPoint = new NetworkVariable<Vector3>();  //瞄准落点
    NetworkVariable<Vector3> netTargetPos = new NetworkVariable<Vector3>(); //锁定点
    NetworkVariable<bool> netHasTarget = new NetworkVariable<bool>();       //是否锁定
    NetworkVariable<float> netAimAngle = new NetworkVariable<float>();      //精度圈角度
    NetworkVariable<float> netVerticalSpeed = new NetworkVariable<float>(); //垂直速度
    NetworkVariable<int> netHealth = new NetworkVariable<int>();            //玩家血量
    NetworkVariable<int> netAmmo = new NetworkVariable<int>();              //剩余弹药
    NetworkVariable<int> netMagazine = new NetworkVariable<int>(6);         //剩余弹夹
    NetworkVariable<int> netRespawnIndex = new NetworkVariable<int>();      //复活点编号
    NetworkVariable<int> netScore = new NetworkVariable<int>();             //玩家积分
    NetworkVariable<float> netRespawnWait = new NetworkVariable<float>();   //等待剩余
    NetworkVariable<bool> netOut = new NetworkVariable<bool>();             //已淘汰，不再复活

    //对外读取
    public ushort Show_State => netShowState.Value;         //表现状态位
    public Vector3 AimPoint => netAimPoint.Value;           //瞄准落点
    public Vector3 TargetPos => netTargetPos.Value;         //锁定点
    public bool HasTarget => netHasTarget.Value;            //是否锁定
    public float AimAngle => netAimAngle.Value;             //精度圈角度
    public float VerticalSpeed => netVerticalSpeed.Value;   //垂直速度
    public int Health => netHealth.Value;                   //玩家血量
    public int Ammo => netAmmo.Value;                       //剩余弹药
    public int Magazine => netMagazine.Value;               //剩余弹夹
    public int RespawnIndex => netRespawnIndex.Value;       //复活点编号
    public int Score => netScore.Value;                     //玩家积分
    public float RespawnWait => netRespawnWait.Value;       //等待剩余
    public bool Is_Out => netOut.Value;                     //已淘汰

    //本机表现状态位，按键本地预测
    public ushort Show_State_Local()
    {
        Player_Control c = Own_Control;
        if (c == null || c.Con_input_Manage == null || c.Con_move == null) return netShowState.Value;

        Input_Manage im = c.Con_input_Manage;
        Player_Move_Date mv = c.Con_move;

        ushort state = 0;
        if (im.WASDHeld) state |= Player_animation.BitWalk;
        if (im.RunHeld) state |= Player_animation.BitRun;
        if (im.SquatHeld) state |= Player_animation.BitSquat;
        if (im.MouseHeld || im.ShoulderHeld || im.AdsHeld) state |= Player_animation.BitGun;
        if (im.JumpDownHeld) state |= Player_animation.BitJump;
        if (netRespawnWait.Value > 0f || netOut.Value) state |= Player_animation.BitDeath;
        if (mv.rollTimer > 0f) state |= Player_animation.BitRoll;
        if (c.Con_gun_Control != null && c.Con_gun_Control.IsReloading) state |= Player_animation.BitReload;            //换弹
        if (mv.isWeightLess) state |= Player_animation.BitWeightLess;    //失重

        // 落地与方向取本机数据
        if (mv.isOnGround) state |= Player_animation.BitGround;
        state |= Player_animation.Pack_Move(im.LastPacket.move);
        return state;
    }

    //主机打包表现状态位
    ushort PackShowState()
    {
        Player_Control c = Own_Control;
        Player_Move_Date mv = c.Con_move;

        InputPacket packet = c.Con_player_HostNetworkEvent.Packet;
        bool aimDown = packet.mouse1 || packet.mouse2 || packet.shoulder || packet.ads;

        ushort state = 0;
        if (mv.isWASDDowm) state |= Player_animation.BitWalk;
        if (mv.isRuning) state |= Player_animation.BitRun;
        if (mv.isSquat) state |= Player_animation.BitSquat;
        if (aimDown) state |= Player_animation.BitGun;
        if (mv.isJumpDown) state |= Player_animation.BitJump;
        if (mv.isOnGround) state |= Player_animation.BitGround;
        if (c.Con_ObjectSystem.isDead || netOut.Value) state |= Player_animation.BitDeath;
        if (mv.rollTimer > 0f) state |= Player_animation.BitRoll;
        if (c.Con_gun_Control != null && c.Con_gun_Control.IsReloading) state |= Player_animation.BitReload;            //换弹
        if (mv.isWeightLess) state |= Player_animation.BitWeightLess;    //失重

        // 移动方向取输入包，各端一致
        state |= Player_animation.Pack_Move(c.Con_player_HostNetworkEvent.Packet.move);
        return state;
    }

    //主机每帧写表现状态位，死亡期间也要同步
    public void Show_State_Push()
    {
        if (!IsServer) return;
        if (Own_Control == null || Own_Control.Con_move == null) return;

        netShowState.Value = PackShowState();
    }

    //主机每帧写同步数据，数据更新末尾调用
    public void Host_State_Push()
    {
        if (!IsServer) return;

        Player_Control c = Own_Control;
        if (c == null || c.Con_gun_Control == null || c.Con_ObjectSystem == null) return;

        // 生命与弹药
        netHealth.Value = c.Con_ObjectSystem.HP;
        netAmmo.Value = c.Con_gun_Control.ammo;
        netMagazine.Value = c.Con_gun_Control.magazineCount;

        // 垂直速度
        netVerticalSpeed.Value = c.rigidbody != null ? c.rigidbody.velocity.y : 0f;

        // 同步瞄准表现数据
        InputPacket packet = c.Con_player_HostNetworkEvent != null ? c.Con_player_HostNetworkEvent.Packet : default;
        bool aimDown = packet.mouse1 || packet.mouse2 || packet.shoulder || packet.ads;

        netHasTarget.Value = aimDown && c.Con_gun_Control.HasTarget;   //举枪才显示锁圈
        netAimAngle.Value = c.Con_gun_Control.CurrentAngle;
        if (aimDown)
        {
            netAimPoint.Value = packet.aimPoint;
            netTargetPos.Value = c.Con_gun_Control.TargetPos;
        }
    }

    //初始化同步
    public void Init_Push(int hp, int ammo, int magazine, int respawnIdx)
    {
        if (!IsServer) return;

        netHealth.Value = hp;
        netAmmo.Value = ammo;
        netMagazine.Value = magazine;
        netRespawnIndex.Value = respawnIdx;
    }

    //复活等待时间，主机写入
    public void Respawn_Wait_Push(float wait)
    {
        if (!IsServer) return;

        netRespawnWait.Value = wait;
    }

    //复活点编号只增不减
    public void Respawn_Index_Push(int index)
    {
        if (!IsServer) return;
        if (netRespawnIndex.Value >= index) return;

        netRespawnIndex.Value = index;
    }

    //淘汰标记，主机写入
    public void Out_Push()
    {
        if (!IsServer) return;

        netOut.Value = true;
    }

    //累加积分，主机处理
    public void Score_Add(int add)
    {
        if (!IsServer) return;

        netScore.Value += add;
    }
}
