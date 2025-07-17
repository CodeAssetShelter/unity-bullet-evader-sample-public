#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.IO;

[CustomEditor(typeof(BgmAsset))]
public class BgmAssetEditor : Editor
{
    private ReorderableList _list;

    void OnEnable()
    {
        // clipPaths 프로퍼티 가져오기
        var prop = serializedObject.FindProperty("clipPaths");

        _list = new ReorderableList(serializedObject, prop, true, true, true, true);
        _list.drawHeaderCallback = rect => GUI.Label(rect, "Clip Paths (Resources)");
        _list.drawElementCallback = (rect, idx, _, __) =>
        {
            var elem = prop.GetArrayElementAtIndex(idx);
            elem.stringValue = EditorGUI.TextField(rect, elem.stringValue);
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();       // displayName, composer, etc.
        GUILayout.Space(8);

        // ── 드래그 영역 ──────────────────────────────
        Rect drop = GUILayoutUtility.GetRect(0f, 40f, GUILayout.ExpandWidth(true));
        GUI.Box(drop, "Drag AudioClips here", EditorStyles.helpBox);

        HandleDrag(drop);

        _list.DoLayoutList();
        serializedObject.ApplyModifiedProperties();
    }

    private void HandleDrag(Rect dropArea)
    {
        Event evt = Event.current;
        if (!dropArea.Contains(evt.mousePosition)) return;

        if (evt.type == EventType.DragUpdated)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            evt.Use();
        }
        else if (evt.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            foreach (Object obj in DragAndDrop.objectReferences)
            {
                if (obj is AudioClip clip)
                {
                    string full = AssetDatabase.GetAssetPath(clip);              // "Assets/Resources/Audio/BGM/Stage1_Loop.ogg"
                    string resPath = ToResourcesPath(full);                      // "Audio/BGM/Stage1_Loop"
                    AppendIfNotExists(resPath);
                }
            }
            evt.Use();
        }
    }

    private static string ToResourcesPath(string assetPath)
    {
        const string prefix = "Assets/Resources/";
        if (!assetPath.StartsWith(prefix))
        {
            Debug.LogWarning($"[{assetPath}] 은 Resources 폴더 밖에 있습니다!");
            return null;
        }
        string noExt = Path.ChangeExtension(assetPath, null);          // 확장자 제거
        return noExt.Substring(prefix.Length);                           // "Audio/..."
    }

    private void AppendIfNotExists(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        var listProp = serializedObject.FindProperty("clipPaths");
        for (int i = 0; i < listProp.arraySize; ++i)
        {
            if (listProp.GetArrayElementAtIndex(i).stringValue == path)  // 중복 방지
                return;
        }
        int idx = listProp.arraySize;
        listProp.InsertArrayElementAtIndex(idx);
        listProp.GetArrayElementAtIndex(idx).stringValue = path;
    }
}
#endif