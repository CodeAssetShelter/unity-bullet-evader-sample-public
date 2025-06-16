using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class SpriteBackgroundLooper : MonoBehaviour
{
    [Tooltip("UV 스크롤 속도 (텍스처 단위/sec)")]
    public Vector2 scrollSpeed = new Vector2(0f, 0.1f);

    private Material _mat;
    private Vector2 _offset;

    void Awake()
    {
        // material 프로퍼티를 사용해 인스턴스화
        _mat = GetComponent<SpriteRenderer>().material;
    }

    void Update()
    {
        // 누적 오프셋
        _offset += scrollSpeed * Time.deltaTime;
        // Unlit/Transparent 에서는 "_MainTex" 프로퍼티를 사용
        _mat.mainTextureOffset = _offset;
    }
}
