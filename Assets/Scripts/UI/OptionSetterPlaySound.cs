using UnityEngine;

public class OptionSetterPlaySound : MonoBehaviour
{
    public SoundManager.SoundType m_Type;

    public void Play()
    {
        switch (m_Type)
        {
            case SoundManager.SoundType.EFX_ExplosionSmall_000:
            case SoundManager.SoundType.EFX_ExplosionSmall_001:
                SoundManager.Instance.PlayEfxSound(m_Type);
                break;
            case SoundManager.SoundType.BGM_001:
            case SoundManager.SoundType.BGM_002:
                SoundManager.Instance.PlayMusic(m_Type);
                break;
        }
    }
}
