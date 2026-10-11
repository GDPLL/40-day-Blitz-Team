using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SplashParticle_Control : MonoBehaviour
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

    void Start()
    {
        foreach (var particle in particles)
        {
            if (particle == null) continue;
            if (particle != null) particle.Play();
        }

        Play_Sound();       //生成时播撞击音

        Destroy(gameObject, time);      // 到时间销毁
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
