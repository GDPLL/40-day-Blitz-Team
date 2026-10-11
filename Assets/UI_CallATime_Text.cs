using System;
using System.Collections;
using TMPro;
using UnityEngine;

// 通讯设备UI，时间显示与打字机台词
public class UI_CallATime_Text : MonoBehaviour
{
    [Serializable]
    public class Line
    {
        public string text;     // 台词
        public float bars = 1f; // 占用拍数
    }

    [Serializable]
    public class Section
    {
        public float time;      // 触发时刻
        public Line[] lines;    // 台词行
    }

    [Serializable]
    class CallData
    {
        public Section[] sections;  // 定时段落
        public Section[] events;    // 突发段落
    }

    public TextMeshProUGUI timeText;    // 时间文本，格式00:00
    public TextMeshProUGUI textPro;     // 台词文本
    public AudioSource audioSource;     // 语音音源
    public AudioSource tipSource;       // 提示音源
    public AudioClip[] sectionClips;    // 每段语音，与配置顺序一致
    public AudioClip[] eventClips;      // 每个突发语音
    public AudioClip startClip;         // 启动音效
    public AudioClip endClip;           // 完成音效
    public TextAsset jsonFile;          // 台词配置

    public float bpm = 120f;            // 音乐速度
    public float typeInterval = 0.03f;  // 每字打印秒数
    public float lineGap = 1f;          // 行间分割，拍
    public float lastGap = 4f;          // 最后一行前的分割，拍

    [Header("测试")]
    public bool testMode;               // 测试模式
    public float testTime;              // 测试时间，代替关卡时间

    [Header("解析结果")]
    public Section[] sections;          // 定时台词，初始化时解析
    public Section[] events;            // 突发台词，初始化时解析
    int nextIndex;                      // 待触发段序号
    Coroutine playing;                  // 播放协程
    float lastTestTime = -1f;           // 上次测试时间

    // 一拍秒数
    float BeatTime => 60f / bpm;

    // 初始化，解析台词配置
    public void UI_CallATime_Init()
    {
        if (timeText == null || textPro == null)
        {
            Debug.LogError("UI_CallATime_Text|UI_CallATime_Init|文本未绑定");
            return;
        }

        timeText.text = string.Empty;
        textPro.text = string.Empty;

        if (jsonFile == null)
        {
            Debug.LogError("UI_CallATime_Text|UI_CallATime_Init|jsonFile 为空");
            return;
        }

        CallData data = JsonUtility.FromJson<CallData>(jsonFile.text);
        sections = data.sections;
        events = data.events;
        nextIndex = 0;
    }

    // 驱动，gameTime为关卡时间
    public void UI_CallATime_Update(float gameTime)
    {
        // 测试模式用手动时间
        if (testMode) gameTime = testTime;

        // 分:秒
        if (timeText != null)
            timeText.text = $"{(int)gameTime / 60:00}:{(int)gameTime % 60:00}";

        if (sections == null) return;

        // 测试模式时间一变直接播对应段
        if (testMode)
        {
            Test_Show();
            return;
        }

        // 到点自动播报，播放中不打断
        if (playing != null) return;
        if (nextIndex >= sections.Length) return;
        if (gameTime < sections[nextIndex].time) return;

        UI_CallATime_Show(nextIndex);
    }

    // 测试模式，时间改动直接播对应段
    void Test_Show()
    {
        if (Mathf.Approximately(testTime, lastTestTime)) return;

        lastTestTime = testTime;

        // 取时间覆盖的最新一段
        int pick = -1;
        for (int i = 0; i < sections.Length; i++)
            if (sections[i].time <= testTime) pick = i;

        if (pick >= 0) UI_CallATime_Show(pick);
    }

    // 播放一段通讯，index为段序号
    public void UI_CallATime_Show(int index)
    {
        if (sections == null || index < 0 || index >= sections.Length)
        {
            Debug.LogError("UI_CallATime_Text|UI_CallATime_Show|段序号越界");
            return;
        }

        // 打断上一段
        if (playing != null) StopCoroutine(playing);
        playing = StartCoroutine(Play_Section(sections[index], Clip(sectionClips, index)));

        // 跳过已触发过的段
        if (nextIndex <= index) nextIndex = index + 1;
    }

    // 播放一个突发交流，index为突发序号
    public void UI_CallATime_Event_Show(int index)
    {
        if (events == null || index < 0 || index >= events.Length)
        {
            Debug.LogError("UI_CallATime_Text|UI_CallATime_Event_Show|突发序号越界");
            return;
        }

        // 打断上一段
        if (playing != null) StopCoroutine(playing);
        playing = StartCoroutine(Play_Section(events[index], Clip(eventClips, index)));
    }

    // 取指定数组的片段
    AudioClip Clip(AudioClip[] clips, int index)
    {
        if (clips == null || index < 0 || index >= clips.Length) return null;
        return clips[index];
    }

    // 播放一段，逐行打字并按拍等待
    IEnumerator Play_Section(Section section, AudioClip voice)
    {
        // 启动音效
        if (tipSource != null && startClip != null) tipSource.PlayOneShot(startClip);

        // 语音
        if (audioSource != null && voice != null)
        {
            audioSource.clip = voice;
            audioSource.Play();
        }

        Line[] lines = section.lines;

        for (int i = 0; i < lines.Length; i++)
        {
            Line line = lines[i];

            // 逐字打印，按绝对时间对齐避免累计误差
            textPro.text = string.Empty;
            float startTime = Time.time;
            for (int c = 0; c < line.text.Length; c++)
            {
                float target = startTime + (c + 1) * typeInterval;
                while (Time.time < target) yield return null;

                textPro.text = line.text.Substring(0, c + 1);
            }

            // 等到该行拍数走完再清除
            float lineEnd = startTime + line.bars * BeatTime;
            while (Time.time < lineEnd) yield return null;

            textPro.text = string.Empty;

            // 行间分割，最后一行前留长分割
            float gap = i == lines.Length - 2 ? lastGap : lineGap;
            float gapEnd = Time.time + gap * BeatTime;
            while (Time.time < gapEnd) yield return null;
        }

        // 完成音效
        if (tipSource != null && endClip != null) tipSource.PlayOneShot(endClip);

        playing = null;
    }
}
