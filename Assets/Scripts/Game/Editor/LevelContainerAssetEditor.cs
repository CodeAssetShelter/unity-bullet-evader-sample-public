#if UNITY_EDITOR
using System.Collections.Generic;
using System;
using Unity.Android.Gradle.Manifest;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LevelContainerAsset))]
public class LevelContainerAssetEditor : Editor
{
    SerializedProperty _itemsProp;

    // ‘유효한’ 패턴 배열 (None, State_Count 제외)
    BulletPattern[] _validPatterns;
    int _maxIndex;

    void OnEnable()
    {
        _itemsProp = serializedObject.FindProperty("levels");
        CacheValidPatterns();
    }

    void CacheValidPatterns()
    {
        // 필터링: None 과 State_Count 제외
        List<BulletPattern> list = new();
        foreach (BulletPattern p in Enum.GetValues(typeof(BulletPattern)))
        {
            if (p == BulletPattern.None || p == BulletPattern.State_Count)
                continue;
            list.Add(p);
        }
        _validPatterns = list.ToArray();
        _maxIndex = Mathf.Max(0, _validPatterns.Length - 1);   // 0~4
    }

    public override void OnInspectorGUI()
    {
        // Enum 수정 가능성 대비
        CacheValidPatterns();
        serializedObject.Update();

        // ── 각 Level 표시 ───────────────────────────────
        for (int i = 0; i < _itemsProp.arraySize; i++)
        {
            SerializedProperty item = _itemsProp.GetArrayElementAtIndex(i);
            SerializedProperty cats = item.FindPropertyRelative("categories");
            SerializedProperty val = item.FindPropertyRelative("value");

            EditorGUILayout.BeginVertical("box");

            // 1. 카테고리 리스트
            EditorGUILayout.PropertyField(cats,
                new GUIContent($"Level {i + 1} — Fixed Patterns"), true);

            // 2. ActiveRandomPatternIndex 슬라이더
            val.intValue = Mathf.Clamp(val.intValue, 0, _maxIndex);
            string patName = _validPatterns[val.intValue].ToString();

            val.intValue = EditorGUILayout.IntSlider(
                new GUIContent($"Active Random Pattern Count"),
                val.intValue, 0, _maxIndex);

            // 3. Level 삭제
            if (GUILayout.Button("Delete This Level"))
            {
                _itemsProp.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndVertical();
                break;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        // ── 새 Level 추가 ───────────────────────────────
        if (GUILayout.Button("Add New Level"))
        {
            _itemsProp.InsertArrayElementAtIndex(_itemsProp.arraySize);
            SerializedProperty newItem = _itemsProp.GetArrayElementAtIndex(_itemsProp.arraySize - 1);

            // 기본 카테고리: Normal
            SerializedProperty cats = newItem.FindPropertyRelative("categories");
            cats.ClearArray();
            cats.InsertArrayElementAtIndex(0);
            cats.GetArrayElementAtIndex(0).enumValueIndex = (int)BulletPattern.Normal;  // 필요 시 변경

            // 기본 ActiveRandomPatternIndex = 0 (Normal)
            newItem.FindPropertyRelative("value").intValue = 0;
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif