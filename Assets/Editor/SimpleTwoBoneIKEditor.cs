#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// SimpleTwoBoneIK의 인스펙터. 버튼 몇 개로 설정이 끝나게 한다.
/// Assets/Editor/ 안에 두어야 한다.
/// </summary>
[CustomEditor(typeof(SimpleTwoBoneIK))]
public class SimpleTwoBoneIKEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var ik = (SimpleTwoBoneIK)target;
        serializedObject.Update();

        if (GUILayout.Button("IK 설정 창 열기", GUILayout.Height(22)))
            IKWindow.Open();
        EditorGUILayout.Space(4);

        // ── 1. 체인 ─────────────────────────────────────
        EditorGUILayout.LabelField("1. 뼈 체인", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("preset"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("searchRoot"));

        using (new EditorGUI.DisabledScope(ik.preset == SimpleTwoBoneIK.ChainPreset.Custom))
        {
            if (GUILayout.Button("뼈 자동 찾기", GUILayout.Height(26)))
                AutoFind(ik);
        }

        if (ik.preset == SimpleTwoBoneIK.ChainPreset.Custom)
            EditorGUILayout.HelpBox("Custom은 자동 찾기를 쓰지 않습니다. 아래 세 칸을 직접 채우세요.",
                                    MessageType.None);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("root"), new GUIContent("Root (윗마디)"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("mid"), new GUIContent($"Mid ({ik.JointName})"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("tip"), new GUIContent("Tip (끝)"));

        if (ik.root != null && ik.mid != null && ik.tip != null)
            EditorGUILayout.HelpBox($"{ik.root.name}  →  {ik.mid.name}  →  {ik.tip.name}", MessageType.None);
        else
            EditorGUILayout.HelpBox("뼈가 비어 있습니다. 자동 찾기를 누르거나 직접 끌어다 넣으세요.",
                                    MessageType.Warning);

        EditorGUILayout.Space(8);

        // ── 2. 목표 ─────────────────────────────────────
        EditorGUILayout.LabelField("2. 목표", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(ik.root == null || ik.tip == null))
        {
            if (GUILayout.Button("Target / Hint 자동 생성", GUILayout.Height(26)))
            {
                Undo.RegisterFullObjectHierarchyUndo(ik.gameObject, "Create IK Targets");
                ik.AutoCreateTargetAndHint();
                EditorUtility.SetDirty(ik);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Target을 현재 위치로"))
                {
                    if (ik.target != null) Undo.RecordObject(ik.target, "Snap Target");
                    ik.SnapTargetToTip();
                }
                if (GUILayout.Button("Hint 다시 계산"))
                {
                    if (ik.hint != null)
                    {
                        Undo.RecordObject(ik.hint, "Recalc Hint");
                        ik.hint.position = ik.CalcHintPosition();
                    }
                }
            }
        }

        EditorGUILayout.PropertyField(serializedObject.FindProperty("target"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("hint"),
                                      new GUIContent($"Hint ({ik.JointName} 방향)"));

        EditorGUILayout.Space(8);

        // ── 3. 적용량 ────────────────────────────────────
        EditorGUILayout.LabelField("3. 적용량", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("weight"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("rotationWeight"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("hintWeight"));

        if (!Application.isPlaying && ik.editorPreview && ik.weight <= 0f)
            EditorGUILayout.HelpBox("Weight가 0이라 변화가 없습니다. 1로 올리세요.", MessageType.Info);

        EditorGUILayout.Space(8);

        // ── 미리보기 ─────────────────────────────────────
        EditorGUILayout.LabelField("미리보기", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(!ik.IsValid))
        {
            if (!ik.editorPreview)
            {
                if (GUILayout.Button("미리보기 켜기", GUILayout.Height(28)))
                {
                    Undo.RegisterFullObjectHierarchyUndo(ik.gameObject, "IK Preview On");
                    ik.EnablePreview();
                    if (ik.weight <= 0f) ik.weight = 1f;
                    EditorUtility.SetDirty(ik);
                }
                EditorGUILayout.HelpBox(
                    "켜면 Play 없이도 Target을 따라옵니다. 끄면 원래 포즈로 돌아갑니다.",
                    MessageType.None);
            }
            else
            {
                GUI.backgroundColor = new Color(1f, 0.85f, 0.4f);
                if (GUILayout.Button("미리보기 끄기 (원래 포즈로)", GUILayout.Height(28)))
                {
                    Undo.RegisterFullObjectHierarchyUndo(ik.gameObject, "IK Preview Off");
                    ik.DisablePreview();
                    EditorUtility.SetDirty(ik);
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.HelpBox(
                    "미리보기 중 — 지금 뼈 포즈가 씬에 저장됩니다. 작업이 끝나면 끄세요.",
                    MessageType.Warning);
            }
        }

        serializedObject.ApplyModifiedProperties();

        if (ik.editorPreview && !Application.isPlaying) Repaint();
    }

    static void AutoFind(SimpleTwoBoneIK ik)
    {
        Undo.RecordObject(ik, "Auto Find Bones");

        if (ik.AutoFindBones())
        {
            Debug.Log($"[IK] {ik.preset} — {ik.root.name} → {ik.mid.name} → {ik.tip.name}", ik);
            EditorUtility.SetDirty(ik);
        }
        else
        {
            bool leg = ik.IsLeg;
            EditorUtility.DisplayDialog("뼈를 못 찾음",
                $"{ik.preset} 체인을 찾지 못했습니다.\n\n" +
                $"뼈 이름에 {(leg ? "foot 또는 ankle" : "hand 또는 wrist")}이(가) 들어 있고, " +
                "좌우 표기(.R / _R / Right 등)가 있어야 자동으로 찾습니다.\n\n" +
                "Search Root를 캐릭터 루트로 지정하거나, 직접 끌어다 넣으세요.", "확인");
        }
    }

    // 씬 뷰에서 Target / Hint를 바로 끌 수 있게 핸들을 띄운다
    void OnSceneGUI()
    {
        var ik = (SimpleTwoBoneIK)target;

        if (ik.target != null)
        {
            EditorGUI.BeginChangeCheck();
            Vector3 p = Handles.PositionHandle(ik.target.position, ik.target.rotation);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(ik.target, "Move IK Target");
                ik.target.position = p;
            }
            Handles.Label(ik.target.position, "  Target");
        }

        if (ik.hint != null)
        {
            EditorGUI.BeginChangeCheck();
            Vector3 p = Handles.PositionHandle(ik.hint.position, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(ik.hint, "Move IK Hint");
                ik.hint.position = p;
            }
            Handles.Label(ik.hint.position, "  Hint");
        }
    }
}
#endif
