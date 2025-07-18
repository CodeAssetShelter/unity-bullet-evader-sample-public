using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;

    public enum SoundType
    {
        EFX_ExplosionSmall_000, EFX_ExplosionSmall_001,
        BGM_001, BGM_002,
        EFX_GameOver,
        StateCount
    }

    public enum AudioGroup { Master, Efx, Music }

    [System.Serializable]
    public class SoundData
    {
        public SoundType type;
        public AudioClip clip;      // ★ 과거 SFX용 등 유지
        public BgmAsset bgmAsset;   // ★ BGM 전용
    }


    [Header("- AudioMixer")]
    [SerializeField] private AudioMixer m_AudioMixer;

    [Header("- Exposed Parameter Names")]
    [SerializeField] private string masterParam = "Master";
    [SerializeField] private string efxParam = "Efx";
    [SerializeField] private string musicParam = "Music";

    [Header("- Sources")]
    [SerializeField] private List<SoundData> m_Data;
    [SerializeField] private AudioSource m_SourceMusicFrontBuffer;
    [SerializeField] private AudioSource m_SourceMusicBackBuffer;
    [SerializeField] private AudioSource m_SourceEfx;

    // *------------------ Crossfade Loop Values ----------------------
    [SerializeField] private float m_Crossfade = 2.5f;  // 페이드 구간(초)
    private double m_NextStart;     // DSP 타임
    private bool m_Flip;            // 어느 소스를 다음에 쓸지

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        InitSource(m_SourceMusicFrontBuffer);
        InitSource(m_SourceMusicBackBuffer);
    }

    private void InitSource(AudioSource s)
    {
        s.loop = false;          // 직접 스케줄링 하므로 Loop 체크 해제
        s.playOnAwake = false;
    }

    public void PreloadSound(SoundType _type)
    {
        if (_type.ToString().Contains("EFX_")) return;
        var data = FindSoundData(_type);
        if (data == null) return;

        data.bgmAsset.clipPaths.ForEach(x => Resources.Load(x));
    }

    private Coroutine m_CoPlayBgmAsset;      // Intro+Loop 관리용
    private void PlayBgmAsset(BgmAsset asset)
    {
        if (m_CoPlayBgmAsset != null) StopCoroutine(m_CoPlayBgmAsset);
        if (m_CoScheduleLoop != null) StopCoroutine(m_CoScheduleLoop);
        m_SourceMusicFrontBuffer.Stop();
        m_SourceMusicBackBuffer.Stop();

        m_CoPlayBgmAsset = StartCoroutine(CoPlayBgmAsset(asset));
    }

    IEnumerator CoPlayBgmAsset(BgmAsset asset)
    {
        // ── 1) Intro 로드 & 재생 ─────────────────────────────
        AudioClip introClip = null;
        if (asset.useIntro && asset.Intro != null)
        {
            introClip = Resources.Load<AudioClip>(asset.Intro);
            if (introClip == null) { Debug.LogError("Intro clip load fail"); yield break; }
        }

        AudioClip loopClip = Resources.Load<AudioClip>(asset.Loop);
        if (loopClip == null) { Debug.LogError("Loop clip load fail"); yield break; }

        double dspStart = AudioSettings.dspTime + 0.05;
        m_SourceMusicFrontBuffer.clip = introClip ?? loopClip;
        m_SourceMusicFrontBuffer.volume = 1f;
        m_SourceMusicFrontBuffer.PlayScheduled(dspStart);

        // Intro 없이 바로 Loop라면 코루틴 스킵
        if (introClip == null)
        {
            // Loop용 코루틴만 실행
            SetupAndStartLoop(loopClip, dspStart + loopClip.length);
            yield break;
        }

        // Intro → Loop 크로스페이드 예약
        m_SourceMusicBackBuffer.clip = loopClip;
        m_SourceMusicBackBuffer.volume = 0f;

        m_Flip = false; // Front▶Back
        m_NextStart = dspStart + introClip.length - asset.crossfade;
        m_Crossfade = asset.crossfade;

        // ── 2) Loop 무한 재생 코루틴 시작 ─────────────────────
        m_CoScheduleLoop = StartCoroutine(ScheduleLoop(loopClip));

        //// ── 3) Intro 끝날 때까지 대기 후 Intro 언로드 ─────────
        yield return new WaitForSecondsRealtime(introClip.length + 0.1f);
        //Resources.UnloadAsset(introClip);                        // 메모리 반환
    }
    private void SetupAndStartLoop(AudioClip loopClip, double nextStartTime)
    {
        m_Flip = false;
        m_NextStart = nextStartTime - m_Crossfade;
        m_CoScheduleLoop = StartCoroutine(ScheduleLoop(loopClip));
    }

    public void PlayMusic(SoundType _type)
    {
        var data = FindSoundData(_type);
        if (data == null) return;

        if (data.bgmAsset != null)
        {
            PlayBgmAsset(data.bgmAsset);   // ★ 새 메서드
        }
        else if (data.clip != null)
        {
            PlaySingleClip(data.clip);     // 기존 로직 분리
        }
    }
    private void PlaySingleClip(AudioClip clip)
    {
        // 모든 코루틴·소스 정리
        if (m_CoScheduleLoop != null) StopCoroutine(m_CoScheduleLoop);
        m_SourceMusicFrontBuffer.Stop();
        m_SourceMusicBackBuffer.Stop();

        // 두 버퍼에 동일 클립 장착
        m_SourceMusicFrontBuffer.clip = clip;
        m_SourceMusicBackBuffer.clip = clip;

        // ───── 최초 재생 예약 ─────
        double dspStart = AudioSettings.dspTime + 0.05;     // 50 ms 여유
        m_SourceMusicFrontBuffer.volume = 1f;               // 바로 들리게
        m_SourceMusicFrontBuffer.PlayScheduled(dspStart);   // 첫 트리거
        m_SourceMusicBackBuffer.volume = 0f;               // 교차용 준비

        // 타이밍 파라미터 초기화
        m_Flip = false;                                // Front → Back
        m_NextStart = dspStart + clip.length - m_Crossfade;

        // 코루틴 재시작
        m_CoScheduleLoop = StartCoroutine(ScheduleLoop(clip));
    }

    public void PlayEfxSound(SoundType _type)
    {
        var data = FindSoundData(_type);
        if (data == null) return;

        m_SourceEfx.PlayOneShot(data.clip);
    }

    public void StopAllSound()
    {
        if (m_CoScheduleLoop != null) StopCoroutine(m_CoScheduleLoop);
        if (m_CoPlayBgmAsset != null) StopCoroutine(m_CoPlayBgmAsset);
        m_SourceMusicFrontBuffer.Stop();
        m_SourceMusicBackBuffer.Stop();
        m_SourceEfx.Stop();
    }

    Coroutine m_CoScheduleLoop = null;
    IEnumerator ScheduleLoop(AudioClip _clip)
    {
        while (true)
        {
            double dsp = AudioSettings.dspTime;

            if (dsp + 0.1 > m_NextStart)   // 예약 시점 도달(±100 ms 여유)
            {
                AudioSource next = m_Flip ? m_SourceMusicFrontBuffer
                                           : m_SourceMusicBackBuffer;
                AudioSource curr = m_Flip ? m_SourceMusicBackBuffer
                                           : m_SourceMusicFrontBuffer;

                next.volume = 0f;
                next.PlayScheduled(m_NextStart);                // 샘플‑정확 예약

                float safeFade = Mathf.Max(m_Crossfade, 0.02f);   // 20 ms 이상 확보

                if (safeFade > 0.019f)          // 페이드 사용
                {
                    StartCoroutine(Fade(curr, 1f, 0f, safeFade)); // ↓
                    StartCoroutine(Fade(next, 0f, 1f, safeFade)); // ↑
                }
                else                            // 사실상 crossfade == 0
                {
                    curr.SetScheduledEndTime(m_NextStart);        // 샘플 정확 종료
                    curr.volume = 1f; next.volume = 1f;           // 즉시 전환
                }

                m_NextStart += _clip.length - m_Crossfade;       // 다음 교차 타임
                m_Flip = !m_Flip;
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

    private SoundData FindSoundData(SoundType _type)
    {
        return m_Data.Find(x => x.type == _type);
    }

    /// <summary>
    /// 0.0f ~ 1.0f 슬라이더 값을 dB 로 변환하여 Mixer 에 적용
    /// </summary>
    public void SetVolume(AudioGroup group, float slider01)
    {
        slider01 = Mathf.Clamp01(slider01);          // 안전 클램핑
        float dB = Mathf.Lerp(-80f, 20f, slider01);   // -80dB(무음) ↔ 0dB(풀볼륨)

        switch (group)
        {
            case AudioGroup.Master:
                m_AudioMixer.SetFloat(masterParam, dB);
                break;
            case AudioGroup.Efx:
                m_AudioMixer.SetFloat(efxParam, dB);
                break;
            case AudioGroup.Music:
                m_AudioMixer.SetFloat(musicParam, dB);
                break;
        }
    }

    /// <summary>
    /// 0.0f ~ 1.0f 슬라이더 값을 dB 로 변환하여 Mixer 에 적용
    /// </summary>
    public void SetVolume(string _key, float slider01)
    {
        slider01 = Mathf.Clamp01(slider01);          // 안전 클램핑
        float dB = Mathf.Lerp(-80f, 20f, slider01);   // -80dB(무음) ↔ 0dB(풀볼륨)

        m_AudioMixer.SetFloat(_key, dB);
    }

    /// <summary>
    /// dB 값을 직접 셋팅하고 싶을 때( -80 ~ 0 )
    /// </summary>
    public void SetVolume_dB(AudioGroup group, float dB)
    {
        dB = Mathf.Clamp(dB, -80f, 20f);
        switch (group)
        {
            case AudioGroup.Master:
                m_AudioMixer.SetFloat(masterParam, dB);
                break;
            case AudioGroup.Efx:
                m_AudioMixer.SetFloat(efxParam, dB);
                break;
            case AudioGroup.Music:
                m_AudioMixer.SetFloat(musicParam, dB);
                break;
        }
    }
}
