using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 可受伤对象接口
public interface Idamage
{
    // 扣血并返回实际伤害与击杀
    int Takedamage(int hit, ulong killerId, Vector3 fromPos, out bool killed);
}

