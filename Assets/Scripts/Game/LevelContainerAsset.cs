using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [Categories(List<BulletPattern>) + Value] 한 덩어리
/// </summary>
[Serializable]
public class LevelData
{
    public List<BulletPattern> categories = new();   // 여러 개 선택 가능
    [Range(0, 4)] public int value;                  // 슬라이더(0~N-1)로 편집; 실제 범위는 에디터에서 강제
}

/// <summary>
/// ScriptableObject 컨테이너
/// </summary>
[CreateAssetMenu(fileName = "data_so_level_container",
                 menuName = "Custom/Level Container")]
public class LevelContainerAsset : ScriptableObject
{
    public List<LevelData> levels = new();
    public LevelData GetLevel(float level)
    {
        int idx = Mathf.Clamp((int)level, 0, levels.Count - 1);  // (int) → 버림
        return levels[idx];
    }
}