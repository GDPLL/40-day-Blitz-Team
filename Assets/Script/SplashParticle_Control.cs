using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SplashParticle_Control : MonoBehaviour
{
    public ParticleSystem particle;   // 粒子系统
    public float time = 2f;           // 存活时间
    void Start()
    {
        if (particle == null) particle = GetComponent<ParticleSystem>();
        if (particle != null) particle.Play();

        Destroy(gameObject, time);      // 到时间销毁
    }
}
