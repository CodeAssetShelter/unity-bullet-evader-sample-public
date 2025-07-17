// TMP_CharacterAnimator.cs (UTF‑8)
// ≤5 short comments

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

[RequireComponent(typeof(TextMeshProUGUI))]
public sealed class TMP_CharacterAnimatorFlags : MonoBehaviour
{
    [System.Flags]
    public enum EffectType
    {
        None = 0,
        Wave = 1 << 0,
        Bounce = 1 << 1,
        Floating = 1 << 2,
        Drift = 1 << 3,
        Fall = 1 << 4,
        Shuffle = 1 << 5,
        Spin = 1 << 6,
        Flip = 1 << 7,
        Tilt = 1 << 8,
        Pulse = 1 << 9,
        Pop = 1 << 10,
        WobbleScale = 1 << 11,
        Rainbow = 1 << 12,
        Flash = 1 << 13,
        GradientScroll = 1 << 14,
        Typewriter = 1 << 15,
        Fade = 1 << 16,
        DragWave = 1 << 17
    }

    [SerializeField, Tooltip("여러 효과 중 하나를 랜덤 재생합니다.")]
    private EffectType effects = EffectType.Wave;   // 기존 effect → effects
    [SerializeField] private float amplitude = 5;
    [SerializeField] private float frequency = 3;
    [SerializeField] private float speed = 1;
    [SerializeField] private float revealFPS = 30;
    [SerializeField] private TextMeshProUGUI _tmp;

    TMP_TextInfo _info;
    Vector3[] _base;
    Vector2[] _rand;
    Color32[] _origCol;
    Mesh _mesh;
    string _txtCache;
    int _visible;
    float _nextReveal;
    EffectType _active;

    delegate void EffectFn(int i, Vector3[] v, Color32[] c, int idx, float t, float dist);
    EffectFn _apply;

    void Awake()
    {
        if (!_tmp) _tmp = GetComponent<TextMeshProUGUI>();
    }

    void OnEnable()
    {
        _active = PickRandom(effects); // ① 선택
        SetEffect();                   // ② 매핑
        CacheSeeds();
    }

    void OnValidate()
    {
        if (!_tmp) _tmp = GetComponent<TextMeshProUGUI>();
        if (Application.isPlaying)
        {
            _active = PickRandom(effects); // 재선택
            SetEffect();
        }
    }

    void Update()
    {
        if (!_tmp || _tmp.textInfo.characterCount == 0) return;
        if (_tmp.text != _txtCache) CacheSeeds();

        float t = Time.unscaledTime * speed;
        float dist = Input.GetMouseButton(0)
            ? Vector2.Distance(Input.mousePosition, _tmp.transform.position)
            : 0;

        var verts = _info.meshInfo[0].vertices;
        var cols = _info.meshInfo[0].colors32;

        for (int i = 0; i < _info.characterCount; i++)
        {
            if (!_info.characterInfo[i].isVisible) continue;
            int idx = _info.characterInfo[i].vertexIndex;

            for (int k = 0; k < 4; k++)
            {
                verts[idx + k] = _base[idx + k];
                cols[idx + k] = _origCol[idx + k];
            }

            if (_active == EffectType.Typewriter && i > _visible)
                SetAlpha(cols, idx, 0);

            _apply(i, verts, cols, idx, t, dist);
        }

        _mesh.vertices = verts;
        _mesh.colors32 = cols;
        _tmp.UpdateGeometry(_mesh, 0);

        if (_active == EffectType.Typewriter && Time.time >= _nextReveal)
        {
            _visible = Mathf.Min(_visible + 1, _info.characterCount - 1);
            _nextReveal = Time.time + 1f / revealFPS;
        }
    }

    void CacheSeeds()
    {
        _tmp.ForceMeshUpdate();
        _info = _tmp.textInfo;
        _mesh = _info.meshInfo[0].mesh;
        _txtCache = _tmp.text;

        _base = (Vector3[])_info.meshInfo[0].vertices.Clone();
        _origCol = (Color32[])_info.meshInfo[0].colors32.Clone();

        _rand = new Vector2[_info.characterCount];
        for (int i = 0; i < _rand.Length; i++)
            _rand[i] = new(Random.value * 10, Random.value * 10);

        if (_active == EffectType.Typewriter)
        {
            _visible = -1;
            _nextReveal = Time.time;
        }
    }

    void SetEffect()
    {
        _apply = _active switch
        {
            EffectType.Wave => Wave,
            EffectType.Bounce => Bounce,
            EffectType.Floating => Floating,
            EffectType.Drift => Drift,
            EffectType.Fall => Fall,
            EffectType.Shuffle => Shuffle,
            EffectType.Spin => Spin,
            EffectType.Flip => Flip,
            EffectType.Tilt => Tilt,
            EffectType.Pulse => Pulse,
            EffectType.Pop => Pop,
            EffectType.WobbleScale => Wobble,
            EffectType.Rainbow => Rainbow,
            EffectType.Flash => Flash,
            EffectType.GradientScroll => Grad,
            EffectType.Fade => Fade,
            EffectType.DragWave => Drag,
            _ => NoOp
        };
    }
    EffectType PickRandom(EffectType mask)
    {
        // 선택된 비트들만 리스트화
        var list = new List<EffectType>();
        foreach (EffectType e in Enum.GetValues(typeof(EffectType)))
            if (e != EffectType.None && mask.HasFlag(e))
                list.Add(e);

        return list.Count == 0
            ? EffectType.None
            : list[Random.Range(0, list.Count)];
    }

    #region effects
    void Wave(int i, Vector3[] v, Color32[] c, int idx, float t, float d) =>
        OffY(v, idx, Mathf.Sin(t * frequency + i) * amplitude);

    void Bounce(int i, Vector3[] v, Color32[] c, int idx, float t, float d) =>
        OffY(v, idx, Mathf.Abs(Mathf.Sin(t * frequency + i)) * amplitude);

    void Floating(int i, Vector3[] v, Color32[] c, int idx, float t, float d)
    {
        var s = _rand[i];
        OffXY(v, idx,
              (Mathf.PerlinNoise(s.x, t) - .5f) * amplitude,
              (Mathf.PerlinNoise(s.y, t) - .5f) * amplitude);
    }

    void Drift(int i, Vector3[] v, Color32[] c, int idx, float t, float d) =>
        OffXY(v, idx, t * amplitude * .1f, 0);

    void Fall(int i, Vector3[] v, Color32[] c, int idx, float t, float d) =>
        OffY(v, idx, Mathf.Max(0, amplitude - (t + i * .05f) * amplitude));

    void Shuffle(int i, Vector3[] v, Color32[] c, int idx, float t, float d)
    {
        float p = Mathf.Clamp01(t * .5f);
        Vector2 r = _rand[i] * 100;
        OffXY(v, idx,
              Mathf.Lerp((r.x % 60) - 30, 0, p),
              Mathf.Lerp((r.y % 60) - 30, 0, p));
    }

    void Spin(int i, Vector3[] v, Color32[] c, int idx, float t, float d) =>
        Rot(v, idx, t * frequency * 360);

    void Flip(int i, Vector3[] v, Color32[] c, int idx, float t, float d) =>
        RotY(v, idx, Mathf.Sin(t * frequency) * 180);

    void Tilt(int i, Vector3[] v, Color32[] c, int idx, float t, float d) =>
        Rot(v, idx, Mathf.Sin(t * frequency + i) * 10);

    void Pulse(int i, Vector3[] v, Color32[] c, int idx, float t, float d) =>
        Scale(v, idx, 1 + Mathf.Sin(t * frequency + i) * .2f);

    void Pop(int i, Vector3[] v, Color32[] c, int idx, float t, float d)
    {
        float q = Mathf.PingPong(t * frequency + i * .1f, 1);
        Scale(v, idx, Mathf.LerpUnclamped(0, 1, Ease(q)));
    }

    void Wobble(int i, Vector3[] v, Color32[] c, int idx, float t, float d) =>
        ScaleXY(v, idx,
                1 + Mathf.Sin(t * frequency + i) * .05f,
                1 + Mathf.Sin(t * frequency + i + 1) * .05f);

    void Rainbow(int i, Vector3[] v, Color32[] c, int idx, float t, float d) =>
        SetCol(c, idx, Color.HSVToRGB(Mathf.Repeat(t * .1f + i * .02f, 1), 1, 1));

    void Flash(int i, Vector3[] v, Color32[] c, int idx, float t, float d) =>
        SetCol(c, idx, new Color(1, 1, 1, Mathf.PingPong(t * frequency, 1)));

    void Grad(int i, Vector3[] v, Color32[] c, int idx, float t, float d) =>
        SetCol(c, idx, Color.HSVToRGB(Mathf.Repeat(t * .1f + i * .05f, 1), .7f, 1));

    void Fade(int i, Vector3[] v, Color32[] c, int idx, float t, float d) =>
        SetAlpha(c, idx, Mathf.Clamp01((t * .5f + i * .05f) % 1));

    void Drag(int i, Vector3[] v, Color32[] c, int idx, float t, float dist)
    {
        if (Input.GetMouseButton(0))
            OffY(v, idx, Mathf.Sin(dist * .05f - t * 10) * amplitude);
    }

    void NoOp(int i, Vector3[] v, Color32[] c, int idx, float t, float d) { }
    #endregion

    #region math
    static void OffY(Vector3[] v, int i, float y)
    {
        for (int k = 0; k < 4; k++) v[i + k].y += y;
    }

    static void OffXY(Vector3[] v, int i, float x, float y)
    {
        for (int k = 0; k < 4; k++) v[i + k] += new Vector3(x, y);
    }

    static void Rot(Vector3[] v, int i, float a)
    {
        Vector3 m = (v[i] + v[i + 2]) * .5f;
        float r = a * Mathf.Deg2Rad;
        float cs = Mathf.Cos(r), sn = Mathf.Sin(r);

        for (int k = 0; k < 4; k++)
        {
            Vector3 p = v[i + k] - m;
            v[i + k] = m + new Vector3(p.x * cs - p.y * sn,
                                        p.x * sn + p.y * cs);
        }
    }

    static void RotY(Vector3[] v, int i, float a)
    {
        float r = a * Mathf.Deg2Rad;
        float cs = Mathf.Cos(r), sn = Mathf.Sin(r);
        Vector3 m = (v[i] + v[i + 2]) * .5f;

        for (int k = 0; k < 4; k++)
        {
            Vector3 p = v[i + k] - m;
            v[i + k] = m + new Vector3(p.x * cs + p.z * sn,
                                        p.y,
                                       -p.x * sn + p.z * cs);
        }
    }

    static void Scale(Vector3[] v, int i, float s) => ScaleXY(v, i, s, s);

    static void ScaleXY(Vector3[] v, int i, float sx, float sy)
    {
        Vector3 m = (v[i] + v[i + 2]) * .5f;
        for (int k = 0; k < 4; k++)
        {
            Vector3 p = v[i + k] - m;
            v[i + k] = m + Vector3.Scale(p, new Vector3(sx, sy, 1));
        }
    }

    static void SetCol(Color32[] c, int i, Color col)
    {
        Color32 cc = new((byte)(col.r * 255),
                         (byte)(col.g * 255),
                         (byte)(col.b * 255),
                         (byte)(col.a * 255));

        for (int k = 0; k < 4; k++) c[i + k] = cc;
    }

    static void SetAlpha(Color32[] c, int i, float a)
    {
        byte b = (byte)(a * 255);
        for (int k = 0; k < 4; k++)
        {
            var col = c[i + k];
            col.a = b;
            c[i + k] = col;
        }
    }

    static float Ease(float x)
    {
        x -= 1;
        return 1 + x * x * (2.7f * x + 1.7f);
    }
    #endregion
}
