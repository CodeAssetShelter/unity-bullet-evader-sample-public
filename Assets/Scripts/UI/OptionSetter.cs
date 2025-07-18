using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public interface ISettingInput
{
    public RectTransform GetTMPRect();
    public void OnInput(Key _keyCode);
}

public class OptionSetter : MonoBehaviour, ISettingInput
{
    [Header("- BackBtn")]
    [SerializeField] private bool m_IsBack = false;
    [SerializeField] private Button m_Button;

    [Header("- Settings")]
    [SerializeField] private string m_SettingKey = "None";
    [SerializeField] private int m_CurrentUnits;
    [SerializeField] private TextMeshProUGUI m_SettingName;

    [Header("- Design")]
    public Image m_SegmentPrefab;      // 1칸짜리 프리팹
    public int m_Count = 10;
    public float m_Gap = 2f;

    [Header("Colors")]
    public Color startColor = Color.red; // 0번 인덱스
    public Color endColor = Color.green; // 마지막 인덱스
    [Range(0f, 1f)]
    public float emptyAlpha = 0.25f;      // 비어 있는 조각 투명도

    [Header("- Hierarchy")]
    [Tooltip("Segment들을 넣어 둘 컨테이너(비워 두면 자기 자신)")]
    public Transform m_Container;        // ← 추가

    readonly List<Image> m_Segments = new();
    readonly List<Color> _baseColors = new(); // 그라디언트 본색 저장

    void OnEnable() => BuildSegments();

    /*──────────────────────── public API ────────────────────────*/

    public void SetUnits(int n)
    {
        m_CurrentUnits = Mathf.Clamp(n, 0, m_Count);
        for (int i = 0; i < m_Segments.Count; i++)
        {
            // 활성/비활성에 따라 알파만 조절
            Color c = _baseColors[i];
            c.a = (i < m_CurrentUnits) ? 1f : emptyAlpha;
            m_Segments[i].color = c;
        }
        ApplySettings();
    }

    [ContextMenu("AddOne")]
    public void AddOne() => SetUnits(m_CurrentUnits + 1);
    [ContextMenu("RemoveOne")]
    public void RemoveOne() => SetUnits(m_CurrentUnits - 1);

    public void ApplySettings()
    {
        PlayerPrefs.SetInt(m_SettingKey, m_CurrentUnits);
        SoundManager.Instance.SetVolume(m_SettingKey, (float)m_CurrentUnits / m_Count);
    }
    public void LoadSettings() 
    { 
        if (m_IsBack) return;
        if (PlayerPrefs.HasKey(m_SettingKey))
            m_CurrentUnits = PlayerPrefs.GetInt(m_SettingKey);
        else
        {
            float newVol = (float)m_Count * 0.3f;
            m_CurrentUnits = (int)newVol/m_Count;
            PlayerPrefs.SetInt(m_SettingKey, m_CurrentUnits);
        }

        ApplySettings();
        Debug.Log($"Apply {m_SettingKey} : {PlayerPrefs.GetInt(m_SettingKey)}");
    }
    /*──────────────────────── internal ──────────────────────────*/

    void BuildSegments()
    {
        if (m_IsBack) return;
        if (m_Container == null) m_Container = transform;

        m_SettingName.text = m_SettingKey;

        foreach (Transform c in m_Container) Destroy(c.gameObject);
        m_Segments.Clear();
        _baseColors.Clear();

        float w = m_SegmentPrefab.rectTransform.rect.width; // 세그먼트 폭

        for (int i = 0; i < m_Count; i++)
        {
            var seg = Instantiate(m_SegmentPrefab, m_Container);
            seg.transform.localScale = Vector3.one;

            // 0번째 = (0,0)  /  이후 = (폭 + gap)씩 오른쪽으로 이동
            float x = i * (w + m_Gap);
            seg.rectTransform.anchoredPosition = new Vector2(x, 0);

            // 3) 그라디언트 색 계산
            float t = (m_Count == 1) ? 0f : i / (float)(m_Count - 1);
            Color baseCol = Color.Lerp(startColor, endColor, t);
            seg.color = baseCol;

            m_Segments.Add(seg);
            _baseColors.Add(baseCol);
        }

        LoadSettings();
        SetUnits(m_CurrentUnits);  // 초기 표시
    }

    public RectTransform GetTMPRect()
    {
        return m_SettingName.rectTransform;
    }

    public void OnInput(Key _keyCode)
    {
        switch (_keyCode)
        {
            case Key.A:
            case Key.LeftArrow:
                RemoveOne();
                break;
            case Key.D:
            case Key.RightArrow:
                AddOne();
                break;
            case Key.Space:
            case Key.F:
                if (m_Button != null)
                    m_Button.onClick.Invoke();
                break;
        }
    }
}
