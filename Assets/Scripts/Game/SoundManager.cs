using System.Collections.Generic;
using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public enum SoundType
    {
        ExplosionSmall_000, ExplosionSmall_001,
        StateCount
    }

    [System.Serializable]
    public class SoundData
    {
        public SoundType type;
        public AudioClip clip;
    }

    public static SoundManager Instance;

    [SerializeField] private List<SoundData> m_Data;
    [SerializeField] private AudioSource m_Source;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }

    public void PlayMusic(SoundType _type)
    {
        var data = FindSoundData(_type);
        if (data == null) return;

        m_Source.clip = data.clip;
        m_Source.Play();
    }

    public void PlayEfxSound(SoundType _type)
    {
        var data = FindSoundData(_type);
        if (data == null) return;

        m_Source.PlayOneShot(data.clip);
    }

    private SoundData FindSoundData(SoundType _type)
    {
        return m_Data.Find(x => x.type == _type);
    }
}
