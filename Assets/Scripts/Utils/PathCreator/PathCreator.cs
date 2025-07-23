/************************************************
 *  PathCreator.cs  (3D 대응)
 ************************************************/
using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[RequireComponent(typeof(LineRenderer))]
public class PathCreator : MonoBehaviour
{
    [Tooltip("로컬 좌표 기준 경로 포인트들 (최소 2개)")]
    public PathPoint[] points = new PathPoint[2]
    {
        new PathPoint { position = Vector3.left },
        new PathPoint { position = Vector3.right }
    };

    [Header("Gizmo / Sampling")]
    [Range(4, 40)] public int samplesPerSegment = 12;
    public bool autoRebuild = true;

    // Arc-length table
    private float[] arcTable;   // 누적 길이를 0~1로 정규화
    private float totalLen;

    private LineRenderer lr;

    private void Awake()
    {
        lr = GetComponent<LineRenderer>();
        BuildArcTable();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // null 항목 생성
        for (int i = 0; i < points.Length; i++)
            if (points[i] == null) points[i] = new PathPoint();

        if (autoRebuild)
            BuildArcTable();
    }

    private void OnDrawGizmos() => DrawGizmos();
#endif

    // ───────────── API ─────────────
    /// <summary>세그먼트 균등 t (0~1)</summary>
    public Vector3 Evaluate(float t) => EvaluateInternal(t, false);

    /// <summary>호길이 균등 u (0~1)</summary>
    public Vector3 EvaluateUniform(float u) => EvaluateInternal(u, true);

    public float Length => totalLen;

    // ─────────── 내부 구현 ───────────
    private Vector3 EvaluateInternal(float t, bool useArc)
    {
        if (points == null || points.Length < 2) return transform.position;
        t = Mathf.Clamp01(t);

        if (useArc && arcTable != null && arcTable.Length > 1)
            t = RemapArcToSegmentT(t);

        int segCnt = points.Length - 1;
        float segF = t * segCnt;

        // 마지막 포인트 보호
        if (segF >= segCnt - Mathf.Epsilon)
            return TransformPoint(points[segCnt].position);

        int segIdx = Mathf.FloorToInt(segF);
        float lt = segF - segIdx;

        PathPoint a = points[segIdx];
        PathPoint b = points[segIdx + 1];

        if (a.toNext == SegmentType.Line)
        {
            return Vector3.Lerp(
                TransformPoint(a.position),
                TransformPoint(b.position), lt);
        }

        // Cubic Bezier
        Vector3 p0 = a.position;
        Vector3 p1 = a.position + a.handleOut;
        Vector3 p2 = b.position + b.handleIn;
        Vector3 p3 = b.position;
        return TransformPoint(Bezier(p0, p1, p2, p3, lt));
    }

    public void BuildArcTable()
    {
        if (points == null || points.Length < 2) return;

        int segCnt = points.Length - 1;
        int sampleCnt = segCnt * samplesPerSegment + 1;
        arcTable = new float[sampleCnt];
        totalLen = 0f;

        Vector3 prev = Evaluate(0f); // arcTable null 상태라 non-uniform로 호출되어도 OK
        int k = 1;

        for (int s = 0; s < segCnt; s++)
        {
            for (int i = 1; i <= samplesPerSegment; i++)
            {
                float t = (s + i / (float)samplesPerSegment) / segCnt;
                Vector3 cur = Evaluate(t);
                totalLen += Vector3.Distance(prev, cur);
                arcTable[k++] = totalLen;
                prev = cur;
            }
        }

        // 정규화
        if (totalLen > 0f)
        {
            for (int i = 1; i < sampleCnt; i++)
                arcTable[i] /= totalLen;
        }
    }

    private float RemapArcToSegmentT(float u)
    {
        u = Mathf.Clamp01(u);
        int hi = Array.BinarySearch(arcTable, u);
        if (hi < 0) hi = ~hi;
        hi = Mathf.Clamp(hi, 1, arcTable.Length - 1);
        int lo = hi - 1;

        float segU = Mathf.InverseLerp(arcTable[lo], arcTable[hi], u);
        return (lo + segU) / (arcTable.Length - 1);
    }

    // ───────── Gizmo (Editor 전용) ─────────
#if UNITY_EDITOR
    private static GUIStyle s_PivotStyle;
    private static GUIStyle PivotStyle
    {
        get
        {
            if (s_PivotStyle == null)
            {
                var baseStyle = EditorStyles.boldLabel ?? new GUIStyle(EditorStyles.label);
                s_PivotStyle = new GUIStyle(baseStyle)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 12,
                    richText = true,
                    normal = { textColor = Color.white }
                };
            }
            return s_PivotStyle;
        }
    }

    private void DrawGizmos()
    {
        if (points == null || points.Length < 2) return;

        Handles.color = Color.cyan;

        // 1) 라인/베지어 먼저
        for (int i = 0; i < points.Length - 1; i++)
        {
            var a = points[i];
            var b = points[i + 1];

            Vector3 p0 = TransformPoint(a.position);
            Vector3 p1 = TransformPoint(b.position);

            if (a.toNext == SegmentType.Line)
            {
                Handles.DrawLine(p0, p1);
            }
            else
            {
                Vector3 h0 = TransformPoint(a.position + a.handleOut);
                Vector3 h1 = TransformPoint(b.position + b.handleIn);
                Handles.DrawBezier(p0, p1, h0, h1, Color.cyan, null, 2);
            }
        }

        // 2) GUI 라벨 (픽셀 고정, 원 크기 위)
        Handles.BeginGUI();
        {
            const float HANDLE_WORLD_RADIUS = 0.1f;
            const float MARGIN_PX = 4f;

            Camera cam = SceneView.currentDrawingSceneView?.camera;
            Vector3 camUp = cam ? cam.transform.up : Vector3.up;

            for (int i = 0; i < points.Length; i++)
            {
                Vector3 world = TransformPoint(points[i].position);

                Vector2 guiCenter = HandleUtility.WorldToGUIPoint(world);
                Vector2 guiTop = HandleUtility.WorldToGUIPoint(world + camUp * HANDLE_WORLD_RADIUS);

                float pixelRadius = Vector2.Distance(guiCenter, guiTop);
                Vector2 guiPos = guiCenter + new Vector2(0, -(pixelRadius + MARGIN_PX));

                string txt = $"<b>{i}</b>";
                Vector2 size = PivotStyle.CalcSize(new GUIContent(txt));
                Rect rect = new Rect(guiPos.x - size.x * 0.5f, guiPos.y - size.y * 0.5f, size.x, size.y);

                GUI.Label(rect, txt, PivotStyle);
            }
        }
        Handles.EndGUI();
    }
#endif

    // ───────── 유틸 ─────────
    private Vector3 TransformPoint(Vector3 local) => transform.TransformPoint(local);

    private static Vector3 Bezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float u = 1f - t;
        return u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
    }
}

/************************************************
 *  서브 데이터 타입
 ************************************************/
[Serializable]
public class PathPoint
{
    public Vector3 position;
    public Vector3 handleIn;
    public Vector3 handleOut;
    public SegmentType toNext = SegmentType.Line;
}

public enum SegmentType { Line, Bezier }
