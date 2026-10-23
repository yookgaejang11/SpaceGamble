#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 캐릭터 하나에 IK 체인을 한 번에 깔아주는 메뉴.
/// Assets/Editor/ 안에 두어야 한다.
///
/// 쓰는 법
///   씬에서 캐릭터(또는 Animator가 붙은 오브젝트) 선택
///   → Tools > IK > 양팔 IK 만들기   (또는 팔다리 전부)
///
/// 만들어지는 구조
///   Character
///    ├ IK_RightArm   (SimpleTwoBoneIK)
///    │   ├ RightArm_Target
///    │   └ RightArm_Hint
///    ├ IK_LeftArm ...
///    └ IKDriver      (Animator가 붙은 오브젝트에)
/// </summary>
public static class IKSetupMenu
{
    const string Root = "Tools/IK/";

    [MenuItem(Root + "양팔 IK 만들기", false, 10)]
    static void Arms() => Build(new[]
    {
        SimpleTwoBoneIK.ChainPreset.RightArm,
        SimpleTwoBoneIK.ChainPreset.LeftArm
    });

    [MenuItem(Root + "양다리 IK 만들기", false, 11)]
    static void Legs() => Build(new[]
    {
        SimpleTwoBoneIK.ChainPreset.RightLeg,
        SimpleTwoBoneIK.ChainPreset.LeftLeg
    });

    [MenuItem(Root + "팔다리 전부 만들기", false, 12)]
    static void All() => Build(new[]
    {
        SimpleTwoBoneIK.ChainPreset.RightArm,
        SimpleTwoBoneIK.ChainPreset.LeftArm,
        SimpleTwoBoneIK.ChainPreset.RightLeg,
        SimpleTwoBoneIK.ChainPreset.LeftLeg
    });

    [MenuItem(Root + "이 캐릭터의 IK 전부 끄기", false, 30)]
    static void PreviewOff()
    {
        var go = Selection.activeGameObject;
        if (go == null) return;

        foreach (var ik in go.GetComponentsInChildren<SimpleTwoBoneIK>(true))
        {
            Undo.RegisterFullObjectHierarchyUndo(ik.gameObject, "IK Preview Off");
            ik.DisablePreview();
            EditorUtility.SetDirty(ik);
        }
        Debug.Log("[IK] 미리보기를 전부 껐습니다.");
    }

    [MenuItem(Root + "양팔 IK 만들기", true)]
    [MenuItem(Root + "양다리 IK 만들기", true)]
    [MenuItem(Root + "팔다리 전부 만들기", true)]
    [MenuItem(Root + "이 캐릭터의 IK 전부 끄기", true)]
    static bool Validate() => Selection.activeGameObject != null;


    static void Build(SimpleTwoBoneIK.ChainPreset[] presets)
    {
        var selected = Selection.activeGameObject;
        if (selected == null) return;

        // Animator가 있는 오브젝트를 기준으로 삼는다
        var anim = selected.GetComponent<Animator>() ?? selected.GetComponentInParent<Animator>();
        GameObject owner = anim != null ? anim.gameObject : selected;

        var made = new List<SimpleTwoBoneIK>();
        var failed = new List<string>();

        foreach (var preset in presets)
        {
            string objName = "IK_" + preset;

            Transform t = owner.transform.Find(objName);
            GameObject go;
            if (t == null)
            {
                go = new GameObject(objName);
                Undo.RegisterCreatedObjectUndo(go, "Create IK");
                go.transform.SetParent(owner.transform, false);
            }
            else go = t.gameObject;

            var ik = go.GetComponent<SimpleTwoBoneIK>();
            if (ik == null) ik = Undo.AddComponent<SimpleTwoBoneIK>(go);

            Undo.RecordObject(ik, "Setup IK");
            ik.preset = preset;
            ik.searchRoot = owner.transform;

            if (!ik.AutoFindBones())
            {
                failed.Add(preset.ToString());
                EditorUtility.SetDirty(ik);
                continue;
            }

            ik.AutoCreateTargetAndHint();
            ik.weight = 0f;
            EditorUtility.SetDirty(ik);

            made.Add(ik);
            Debug.Log($"[IK] {preset} — {ik.root.name} → {ik.mid.name} → {ik.tip.name}", ik);
        }

        // ── IKDriver 연결 ────────────────────────────────
        if (made.Count > 0)
        {
            var driver = owner.GetComponent<IKDriver>();
            if (driver == null) driver = Undo.AddComponent<IKDriver>(owner);

            Undo.RecordObject(driver, "Setup IKDriver");

            var list = new List<IKDriver.Channel>(driver.channels ?? new IKDriver.Channel[0]);
            foreach (var ik in made)
            {
                string id = ToId(ik.preset);
                var existing = list.Find(c => c != null && c.id == id);
                if (existing != null) existing.ik = ik;
                else list.Add(new IKDriver.Channel { id = id, ik = ik, blendTime = 0.15f });
            }
            driver.channels = list.ToArray();
            EditorUtility.SetDirty(driver);
        }

        // ── 결과 알림 ────────────────────────────────────
        string msg = made.Count > 0
            ? $"{made.Count}개 체인을 만들었습니다.\n\n다음 할 일:\n" +
              "1. 각 Target을 닿아야 할 지점으로 옮기기\n" +
              "2. 애니메이션 이벤트에서 IKOn / IKOff 호출"
            : "만들어진 체인이 없습니다.";

        if (failed.Count > 0)
            msg += $"\n\n찾지 못한 체인: {string.Join(", ", failed)}\n" +
                   "뼈 이름을 확인하고 직접 지정하세요.";

        EditorUtility.DisplayDialog("IK 설정", msg, "확인");

        if (made.Count > 0) Selection.activeGameObject = made[0].gameObject;
    }

    /// <summary>IKDriver에서 쓸 이름. 애니메이션 이벤트의 String 칸에 이걸 적는다</summary>
    static string ToId(SimpleTwoBoneIK.ChainPreset p)
    {
        switch (p)
        {
            case SimpleTwoBoneIK.ChainPreset.RightArm: return "rightArm";
            case SimpleTwoBoneIK.ChainPreset.LeftArm: return "leftArm";
            case SimpleTwoBoneIK.ChainPreset.RightLeg: return "rightLeg";
            case SimpleTwoBoneIK.ChainPreset.LeftLeg: return "leftLeg";
            default: return "custom";
        }
    }
}
#endif
