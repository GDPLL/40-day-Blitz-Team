using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SplashParticle_Control : MonoBehaviour, IPooled_Effect
{
    public ParticleSystem[] particles;   // 粒子系统
    public AudioSource audioSource;   // 音源
    public AudioSource ricochetSource;    // 跳弹音源，不填用主音源
    public AudioClip hitClip;         // 水泥撞击音
    public AudioClip ricochetClip;    // 跳弹音

    public float time = 2f;           // 存活时间
    public float ricochetRate = 0.1f;    // 跳弹概率
    public float volumeMin = 0.8f;    // 随机音量下限
    public float volumeMax = 1.2f;    // 随机音量上限

    public bool IsAudio;

    public float Pool_Lifetime => time > 0f ? time : 2f;

    // 非池化实例保留自销毁，池化实例由管理器统一回收
    void Start()
    {
        if (Object_Pool_Manager.Is_Pooled(gameObject)) return;

        Play_Performance();
        Destroy(gameObject, Pool_Lifetime);
    }

    // 每次从池里取出时重播粒子和音效
    public void On_Pool_Spawn()
    {
        Play_Performance();
    }

    // 回收时停止残留粒子和声音
    public void On_Pool_Despawn()
    {
        foreach (ParticleSystem particle in particles)
        {
            if (particle == null) continue;
            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (audioSource != null) audioSource.Stop();
        if (ricochetSource != null) ricochetSource.Stop();
    }

    // 播放全部粒子和命中音效
    void Play_Performance()
    {
        foreach (ParticleSystem particle in particles)
        {
            if (particle == null) continue;
            particle.Clear(true);
            particle.Play(true);
        }

        Play_Sound();
    }

    // 生成时播撞击音，按概率叠跳弹音
    void Play_Sound()
    {
        if (!IsAudio) return;
        // 无音源的对象只走粒子，不出声
        if (audioSource == null) return;

        if (hitClip != null) audioSource.PlayOneShot(hitClip, Random_Volume());

        // 按概率出跳弹
        if (ricochetClip == null || UnityEngine.Random.value > ricochetRate) return;

        // 跳弹可单独配衰减，不填就用主音源
        AudioSource source = ricochetSource != null ? ricochetSource : audioSource;
        source.PlayOneShot(ricochetClip, Random_Volume());
    }

    // 随机音量系数
    float Random_Volume()
    {
        return UnityEngine.Random.Range(volumeMin, volumeMax);
    }
}
