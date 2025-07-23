/************************************************
 *  PathFollower.cs  (3D + speedCurve + Loop/Reverse + Switch A)
 ************************************************/
using UnityEditor;
using UnityEngine;

public class PathFollower : MonoBehaviour
{
    [Header("Path & Timing")]
    public PathCreator path;
    [Min(0.01f)] public float duration = 5f;
    [Range(0f, 1f)] public float offset;
    public bool useUniform = true;
    public bool loop = true;
    public bool reverse = false;

    [Header("Speed Profile")]
    [Tooltip("X: path ratio(0~1), Y: speed multiplier")]
    public AnimationCurve speedCurve = AnimationCurve.Linear(0, 1, 1, 1);

    [Header("Auto Start")]
    public bool autoStart = true;   // ✔️ 플레이/활성화 시 자동 시작

    private float elapsed;
    private bool isMoving;

    private void OnEnable()
    {
        if (autoStart)
            StartFollow();          // startOffset 기본값 -1 → 기존 offset 사용
    }

    // ───── External Control ─────
    [ContextMenu("▶ StartFollow")]
    public void StartFollow() => StartFollow(0);
    public void StartFollow(float startOffset = -1f)
    {
        if (startOffset >= 0f)
            offset = Mathf.Repeat(startOffset, 1f);

        elapsed = reverse ? duration : 0f;
        isMoving = true;
        UpdatePosition();  // 첫 프레임 정렬
    }

    [ContextMenu("■ StopFollow")]
    public void StopFollow() => isMoving = false;

    /// <summary>
    /// 진행률 유지 방식(A) - 현재 u를 계산해 새 경로에서도 같은 u로 이어가기
    /// </summary>
    [ContextMenu("⤴ SwitchPath (keep progress)")]
    public void SwitchPath(PathCreator newPath)
    {
        if (newPath == null || newPath == path) return;

        float uNow = Mathf.Repeat(offset + elapsed / duration, 1f);

        path = newPath;
        offset = 0f;
        elapsed = uNow * duration;

        isMoving = true;
        UpdatePosition();
    }

    private void Update()
    {
        if (!isMoving || path == null || duration <= 0f) return;

        // 현재 진행률로 speedCurve 평가
        float uRaw = Mathf.Repeat(offset + elapsed / duration, 1f);
        float speedMul = speedCurve.Evaluate(uRaw);

        float dir = reverse ? -1f : 1f;
        elapsed += dir * Time.deltaTime * speedMul;

        if (loop)
        {
            elapsed = Mathf.Repeat(elapsed, duration);
        }
        else
        {
            elapsed = Mathf.Clamp(elapsed, 0f, duration);

            bool reachedEnd = (!reverse && Mathf.Approximately(elapsed, duration)) ||
                              (reverse && Mathf.Approximately(elapsed, 0f));

            if (reachedEnd) isMoving = false;
        }

        UpdatePosition();
    }

    private void UpdatePosition()
    {
        float u = Mathf.Repeat(offset + elapsed / duration, 1f);

        transform.position = useUniform
            ? path.EvaluateUniform(u)
            : path.Evaluate(u);
    }

    [ContextMenu("SpeedCurve ▸ Sync Keys With Points")]
    private void SyncSpeedCurveKeys()
    {
        if (path == null || path.points == null || path.points.Length < 2) return;

        int segCnt = path.points.Length - 1;
        var ks = new Keyframe[path.points.Length];
        for (int i = 0; i < path.points.Length; i++)
        {
            float u = i / (float)segCnt;      // 0~1
            ks[i] = new Keyframe(u, 1f);      // 기본 1배속
        }
        speedCurve = new AnimationCurve(ks);

        // (선택) 탄젠트 정리 - 에디터 전용
#if UNITY_EDITOR
        for (int i = 0; i < speedCurve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(speedCurve, i, AnimationUtility.TangentMode.Auto);
            AnimationUtility.SetKeyRightTangentMode(speedCurve, i, AnimationUtility.TangentMode.Auto);
        }
#endif
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (duration < 0.01f) duration = 0.01f;
    }
#endif
}
