using UnityEngine;
using System.Collections;

[RequireComponent(typeof(AudioListener))]
public class SeamlessLoop : MonoBehaviour
{
    [Header("설정")]
    [SerializeField] private AudioClip bgmClip;       // Suno AI가 만든 MP3를 넣어도 됨
    [SerializeField] private float crossfade = 1.5f;  // 페이드 구간(초)

    private AudioSource srcA, srcB;
    private double nextStart;     // DSP 타임
    private bool flip;            // 어느 소스를 다음에 쓸지

    void Awake()
    {
        // AudioSource 두 개 준비
        srcA = gameObject.AddComponent<AudioSource>();
        srcB = gameObject.AddComponent<AudioSource>();

        InitSource(srcA);
        InitSource(srcB);
    }

    void Start()
    {
        // 첫 시작
        srcA.Play();
        nextStart = AudioSettings.dspTime + bgmClip.length - crossfade;
        StartCoroutine(ScheduleLoop());
    }

    private void InitSource(AudioSource s)
    {
        s.clip = bgmClip;
        s.loop = false;          // 직접 스케줄링 하므로 Loop 체크 해제
        s.playOnAwake = false;
        s.volume = 1f;
    }

    IEnumerator ScheduleLoop()
    {
        while (true)
        {
            double dsp = AudioSettings.dspTime;

            if (dsp + 0.1 > nextStart) // 약 0.1초 여유를 두고 예약
            {
                AudioSource next = flip ? srcA : srcB;
                AudioSource curr = flip ? srcB : srcA;

                next.volume = 0f;
                next.PlayScheduled(nextStart);              // 샘플 정확도 예약
                StartCoroutine(Fade(curr, 1f, 0f, crossfade));
                StartCoroutine(Fade(next, 0f, 1f, crossfade));

                nextStart += bgmClip.length - crossfade;    // 다음 이벤트 계산
                flip = !flip;
            }
            yield return null;
        }
    }

    IEnumerator Fade(AudioSource s, float from, float to, float dur)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            s.volume = Mathf.Lerp(from, to, t / dur);
            yield return null;
        }
        s.volume = to;
    }
}
