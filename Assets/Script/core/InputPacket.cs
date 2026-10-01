using UnityEngine;
using Unity.Netcode;

// 一帧输入数据，用于上传主机
public struct InputPacket : INetworkSerializable
{
    // 玩家身份
    public ulong clientId;          // 发送者网络ID

    // 输入状态
    public Vector3 viewDir;         // 本机视角朝向
    public Vector3 aimPoint;        // 举枪瞄准落点
    public Vector2 move;            // 移动轴
    public bool jump;               // 跳跃
    public bool mouse1;             // 左键
    public bool mouse2;             // 右键
    public bool mouseHeld;          // 鼠标按住
    public bool shoulder;           // 肩射
    public bool ads;                // 开镜
    public bool run;                // 奔跑
    public bool reload;             // 换弹
    public bool squat;              // 蹲下

    // 网络序列化
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref clientId);
        serializer.SerializeValue(ref viewDir);
        serializer.SerializeValue(ref aimPoint);
        serializer.SerializeValue(ref move);
        serializer.SerializeValue(ref jump);
        serializer.SerializeValue(ref mouse1);
        serializer.SerializeValue(ref mouse2);
        serializer.SerializeValue(ref mouseHeld);
        serializer.SerializeValue(ref shoulder);
        serializer.SerializeValue(ref ads);
        serializer.SerializeValue(ref run);
        serializer.SerializeValue(ref reload);
        serializer.SerializeValue(ref squat);
    }
}
