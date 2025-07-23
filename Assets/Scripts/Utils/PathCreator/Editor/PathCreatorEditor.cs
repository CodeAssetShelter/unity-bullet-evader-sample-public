/************************************************
 *  PathCreatorEditor.cs  (Editor 전용)
 ************************************************/
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PathCreator))]
public class PathCreatorEditor : Editor
{
    private PathCreator pc;

    private void OnEnable() => pc = target as PathCreator;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("samplesPerSegment"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("autoRebuild"));

        SerializedProperty pointsProp = serializedObject.FindProperty("points");

        // 항상 펼쳐서 보여주기
        pointsProp.isExpanded = EditorGUILayout.Foldout(pointsProp.isExpanded,
            $"Points ({pointsProp.arraySize})", true);

        if (pointsProp.isExpanded)
        {
            EditorGUI.indentLevel++;
            for (int i = 0; i < pointsProp.arraySize; i++)
            {
                SerializedProperty elem = pointsProp.GetArrayElementAtIndex(i);
                elem.isExpanded = true;
                EditorGUILayout.PropertyField(elem, true);
            }
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add Point"))
        {
            int len = pointsProp.arraySize;
            pointsProp.InsertArrayElementAtIndex(len);
            serializedObject.ApplyModifiedProperties(); // 먼저 적용

            // 새 포인트 기본값
            if (pc.points[len] == null) pc.points[len] = new PathPoint();
            pc.points[len].position =
                pc.points[len - 1].position + Vector3.right;

            EditorUtility.SetDirty(pc);
            pc.BuildArcTable();
        }
        if (GUILayout.Button("Remove Last") && pointsProp.arraySize > 2)
        {
            pointsProp.DeleteArrayElementAtIndex(pointsProp.arraySize - 1);
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(pc);
            pc.BuildArcTable();
        }
        EditorGUILayout.EndHorizontal();

        serializedObject.ApplyModifiedProperties();
    }

    // 씬 핸들
    private void OnSceneGUI()
    {
        if (pc.points == null) return;

        for (int i = 0; i < pc.points.Length; i++)
        {
            var p = pc.points[i];
            EditorGUI.BeginChangeCheck();
            Vector3 world = pc.transform.TransformPoint(p.position);

#if UNITY_2022_1_OR_NEWER
            Vector3 newWorld = Handles.FreeMoveHandle(world, 0.1f, Vector3.zero, Handles.SphereHandleCap);
#else
            Vector3 newWorld = Handles.FreeMoveHandle(world, Quaternion.identity, 0.1f, Vector3.zero, Handles.SphereHandleCap);
#endif

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(pc, "Move Path Point");
                p.position = pc.transform.InverseTransformPoint(newWorld);
                pc.BuildArcTable();
            }

            // 베지어 핸들
            if (i < pc.points.Length - 1 && p.toNext == SegmentType.Bezier)
            {
                Handles.color = Color.magenta;

                Vector3 hOutW = pc.transform.TransformPoint(p.position + p.handleOut);
                Vector3 newHOutW = Handles.PositionHandle(hOutW, Quaternion.identity);
                Handles.DrawLine(world, newHOutW);
                if (hOutW != newHOutW)
                {
                    Undo.RecordObject(pc, "Move Handle Out");
                    p.handleOut = pc.transform.InverseTransformPoint(newHOutW) - p.position;
                    pc.BuildArcTable();
                }

                var next = pc.points[i + 1];
                Vector3 hInW = pc.transform.TransformPoint(next.position + next.handleIn);
                Vector3 newHInW = Handles.PositionHandle(hInW, Quaternion.identity);
                Handles.DrawLine(pc.transform.TransformPoint(next.position), newHInW);
                if (hInW != newHInW)
                {
                    Undo.RecordObject(pc, "Move Handle In");
                    next.handleIn = pc.transform.InverseTransformPoint(newHInW) - next.position;
                    pc.BuildArcTable();
                }
            }
        }
    }
}
#endif
