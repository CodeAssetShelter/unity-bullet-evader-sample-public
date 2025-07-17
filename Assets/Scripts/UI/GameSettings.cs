using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using Unity.VisualScripting;

public class GameSettings : MonoBehaviour
{
    [Space(5)]
    [SerializeField] protected RectTransform m_Arrow;
    [SerializeField] protected List<GameObject> m_SettingsObjs;
    [SerializeField] protected int m_BtnIdx = 0;    // 현재 선택된 버튼 인덱스 (0 = 맨 위)
    [SerializeField] protected float m_Offset = 45f;   // TMP 왼쪽 – 화살표 오른쪽 간격(px)


    protected virtual void OnEnable()
    {
        Init();
    }

    private void Init()
    {
        if (m_CoDetectOnInput != null)
            StopCoroutine(m_CoDetectOnInput);
        m_CoDetectOnInput = StartCoroutine(CorDetectOnInput());
    }

    Coroutine m_CoDetectOnInput;
    IEnumerator CorDetectOnInput()
    {
        m_BtnIdx = 0;
        UpdateArrowPos();

        while (true)
        {
            foreach (var key in Keyboard.current.allKeys)
            {
                if (!key.wasPressedThisFrame) continue;

                switch (key.keyCode)   // keyCode 반환형은 Key
                {
                    case Key.Space:
                    case Key.F:
                        OnSettingInput(key.keyCode);
                        break;
                    case Key.LeftArrow:
                    case Key.RightArrow:
                    case Key.A:
                    case Key.D:
                        OnSettingInput(key.keyCode);
                        m_SettingsObjs[m_BtnIdx].GetComponent<OptionSetterPlaySound>().Play();
                        break;
                    case Key.DownArrow: case Key.S:
                        Move(+1); break;
                    case Key.UpArrow: case Key.W: 
                        Move(-1); break;
                }
            }
            yield return null;
        }
    }

    void Move(int dir) // dir = ±1
    {
        m_BtnIdx = (m_BtnIdx + dir + m_SettingsObjs.Count) % m_SettingsObjs.Count;
        SoundManager.Instance.StopAllSound();
        UpdateArrowPos();
    }

    void OnSettingInput(Key _key)
    {
        m_SettingsObjs[m_BtnIdx].GetComponent<ISettingInput>().OnInput(_key);
    }

    void UpdateArrowPos()
    {
        var targetRT = m_SettingsObjs[m_BtnIdx].GetComponent<ISettingInput>().GetTMPRect();
        CommonUtil.PositionArrowLeftOfTMP(targetRT, m_Arrow, m_Offset);
    }

    public void InitSettings()
    {
        m_SettingsObjs.ForEach(x => x.GetComponent<OptionSetter>().LoadSettings());
    }
}
