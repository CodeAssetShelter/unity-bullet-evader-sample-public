using UnityEngine;

/// <summary>
/// 노치디자인 적용 UI크기,위치 조정.
/// </summary>
[ExecuteInEditMode]
public class SafeArea : MonoBehaviour
{
    public UnityEngine.UI.CanvasScaler m_CanvasScaler;
    private Vector2 m_OrignSize;
    private Vector2 m_ReSize;

    RectTransform Panel;
    Rect LastSafeArea = new Rect (0, 0, 0, 0);

    private void Awake ()
    {
        if(m_CanvasScaler != null)
        {
            //m_OrignSize = m_CanvasScaler.GetComponent<RectTransform>().rect.size;
            //m_OrignSize = m_CanvasScaler.GetComponent<RectTransform>().rect.size;
            m_OrignSize = m_CanvasScaler.referenceResolution;
            m_ReSize = m_OrignSize;
            Debug.Log(m_OrignSize);
        }
        Panel = GetComponent<RectTransform> ();
        Refresh ();
    }

    private void Update ()
    {
        Refresh ();
    }

    private void Refresh ()
    {
        Rect safeArea = GetSafeArea ();

        if (safeArea != LastSafeArea)
            ApplySafeArea (safeArea);
    }

    private Rect GetSafeArea ()
    {
        return Screen.safeArea;
    }

    private void ApplySafeArea (Rect r)
    {
        LastSafeArea = r;

        // Convert safe area rectangle from absolute pixels to normalised anchor coordinates
        Vector2 anchorMin = r.position;
        Vector2 anchorMax = r.position + r.size;
        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;
        Panel.anchorMin = anchorMin;
        Panel.anchorMax = anchorMax;

        //NBDebug.Log ("New safe area applied to {0}: x={1}, y={2}, w={3}, h={4} on full extents w={5}, h={6}",
        //    name, r.x, r.y, r.width, r.height, Screen.width, Screen.height);



        if (m_CanvasScaler != null)
        {
            m_ReSize = m_OrignSize;
            //m_OrignSize = m_CanvasScaler.referenceResolution;

            if (m_CanvasScaler.screenMatchMode == UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight &&
                m_CanvasScaler.matchWidthOrHeight  == 1)
            {
                m_ReSize.x = (r.width / r.height) * m_OrignSize.y;
                m_CanvasScaler.referenceResolution = m_ReSize;
            }
            else if(r.width / r.height < 1.777778f)
            //if(m_CanvasScaler.referenceResolution.x == m_OrignSize.x)
            {
                m_ReSize.y = (r.height / r.width) * m_OrignSize.x;
                m_CanvasScaler.referenceResolution = m_ReSize;
            }
            else
            {
                m_ReSize.x = (r.width / r.height) * m_OrignSize.y;
                m_CanvasScaler.referenceResolution = m_ReSize;
            }
        }

    }
}