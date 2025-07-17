// Assets/_Scripts/Audio/BgmAsset.cs
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Audio/BGM Asset", fileName = "Bgm_")]
public class BgmAsset : ScriptableObject
{
    [Header("메타데이터")]
    public string displayName;        // In‑game 노출 이름
    public string composer;           // 작곡/저작권자
    public bool hideInfo = false;     // 씬에 정보 노출 여부

    [TextArea(1, 4)]
    public string copyrightNotice;    // CC‑BY‑SA 문구 등

    [Header("클립 구성 (0...n‑2 = Intro or Stem, 마지막 = Loop)")]
    public List<string> clipPaths = new();

    [Header("재생 설정")]
    [Tooltip("Intro→Loop 전환시 DSP 정확도 PlayScheduled 사용")]
    public bool useIntro = true;
    [Tooltip("Loop 시 cross‑fade 길이(초), 0 = 스냅 전환")]
    public float crossfade = 0.0f;

    // 편의 프로퍼티
    public string Intro => useIntro && clipPaths.Count > 1 ? clipPaths[0] : null;
    public string Loop => clipPaths.Count > 0 ? clipPaths[^1] : null;
}
