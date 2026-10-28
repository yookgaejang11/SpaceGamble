// Assets/Editor/AutoIKSetup.cs
// 필요 패키지: Animation Rigging (com.unity.animation.rigging)
// 사용법: Hierarchy에서 캐릭터 루트 선택 → 상단 메뉴 Tools > Auto IK > Setup Selected Character
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations.Rigging;

public static class AutoIKSetup
{
    const string RigName = "AutoIK_Rig";
    const string FingerRigName = "AutoIK_Fingers";

    enum Limb { LeftArm, RightArm, LeftLeg, RightLeg }

    static readonly string[] HandKeys = { "hand", "wrist" };
    static readonly string[] FootKeys = { "foot", "ankle" };
    // 컨트롤/보조 본은 무시 (블렌더에서 Deform Only로 안 뺐을 때 대비)
    static readonly string[] SkipKeys = { "ik", "ctrl", "pole", "target", "hint", "mch-", "org-", "end", "twist", "index", "thumb", "middle", "ring", "pinky", "toe" };

    [MenuItem("Tools/Auto IK/Setup Selected Character")]
    static void Setup()
    {
        var selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("Auto IK", "Hierarchy에서 캐릭터 루트를 먼저 선택하세요.", "확인");
            return;
        }

        var animator = selected.GetComponentInChildren<Animator>();
        Transform charRoot = animator ? animator.transform : selected.transform;

        limitsWarned = false;
        Undo.SetCurrentGroupName("Auto IK Setup");
        int group = Undo.GetCurrentGroup();

        // 기존에 만든 리그가 있으면 지우고 새로 생성
        var old = charRoot.Find(RigName);
        if (old) Undo.DestroyObjectImmediate(old.gameObject);

        // Rig Builder
        var rigBuilder = charRoot.GetComponent<RigBuilder>();
        if (!rigBuilder) rigBuilder = Undo.AddComponent<RigBuilder>(charRoot.gameObject);
        Undo.RecordObject(rigBuilder, "Setup RigBuilder");
        rigBuilder.layers.RemoveAll(l => l == null || l.rig == null);

        // Rig
        var rigGO = new GameObject(RigName);
        Undo.RegisterCreatedObjectUndo(rigGO, "Create Rig");
        rigGO.transform.SetParent(charRoot, false);
        var rig = Undo.AddComponent<Rig>(rigGO);
        rig.weight = 1f;
        rigBuilder.layers.Add(new RigLayer(rig, true));
        EnsureFingerLayerLast(rigBuilder); // 손가락 IK는 팔 IK 다음에 계산되어야 함
        PrefabUtility.RecordPrefabInstancePropertyModifications(rigBuilder);

        // 팔다리 IK
        var log = new List<string>();
        foreach (Limb limb in System.Enum.GetValues(typeof(Limb)))
        {
            if (TryGetChain(animator, charRoot, rigGO.transform, limb, out var r, out var m, out var t))
            {
                CreateIK(rigGO.transform, charRoot, limb, r, m, t);
                log.Add($"✔ {limb}: {r.name} → {m.name} → {t.name}");
            }
            else
            {
                log.Add($"✘ {limb}: 본을 못 찾아서 건너뜀");
            }
        }

        SetupBoneRenderer(charRoot);

        Undo.CollapseUndoOperations(group);
        Selection.activeGameObject = rigGO;

        string mode = animator && animator.isHuman ? "Humanoid 아바타 기준" : "본 이름 기준";
        EditorUtility.DisplayDialog("Auto IK 완료", $"({mode})\n\n" + string.Join("\n", log), "확인");
    }

    [MenuItem("Tools/Auto IK/Setup Selected Character", true)]
    static bool ValidateSetup() => Selection.activeGameObject != null;

    // ───────────────────── 손가락 IK ─────────────────────

    static readonly (HumanBodyBones root, HumanBodyBones tip)[] LeftFingerBones =
    {
        (HumanBodyBones.LeftThumbProximal,  HumanBodyBones.LeftThumbDistal),
        (HumanBodyBones.LeftIndexProximal,  HumanBodyBones.LeftIndexDistal),
        (HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleDistal),
        (HumanBodyBones.LeftRingProximal,   HumanBodyBones.LeftRingDistal),
        (HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleDistal),
    };
    static readonly (HumanBodyBones root, HumanBodyBones tip)[] RightFingerBones =
    {
        (HumanBodyBones.RightThumbProximal,  HumanBodyBones.RightThumbDistal),
        (HumanBodyBones.RightIndexProximal,  HumanBodyBones.RightIndexDistal),
        (HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleDistal),
        (HumanBodyBones.RightRingProximal,   HumanBodyBones.RightRingDistal),
        (HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleDistal),
    };
    static readonly string[] FingerSkipKeys = { "ik", "ctrl", "pole", "target", "hint", "mch-", "org-", "twist", "socket", "weapon", "prop", "attach" };

    [MenuItem("Tools/Auto IK/Add Finger IK")]
    static void SetupFingers()
    {
        var selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("Auto IK", "Hierarchy에서 캐릭터 루트를 먼저 선택하세요.", "확인");
            return;
        }

        var animator = selected.GetComponentInChildren<Animator>();
        Transform charRoot = animator ? animator.transform : selected.transform;

        limitsWarned = false;
        Undo.SetCurrentGroupName("Auto Finger IK Setup");
        int group = Undo.GetCurrentGroup();

        var old = charRoot.Find(FingerRigName);
        if (old) Undo.DestroyObjectImmediate(old.gameObject);

        var rigBuilder = charRoot.GetComponent<RigBuilder>();
        if (!rigBuilder) rigBuilder = Undo.AddComponent<RigBuilder>(charRoot.gameObject);
        Undo.RecordObject(rigBuilder, "Setup RigBuilder");
        rigBuilder.layers.RemoveAll(l => l == null || l.rig == null);

        // 팔다리 리그와 분리된 레이어 → Rig weight로 손가락만 따로 켜고 끌 수 있음
        var rigGO = new GameObject(FingerRigName);
        Undo.RegisterCreatedObjectUndo(rigGO, "Create Finger Rig");
        rigGO.transform.SetParent(charRoot, false);
        var rig = Undo.AddComponent<Rig>(rigGO);
        rig.weight = 1f;
        rigBuilder.layers.Add(new RigLayer(rig, true));
        PrefabUtility.RecordPrefabInstancePropertyModifications(rigBuilder);

        var log = new List<string>();
        foreach (var (side, sideName) in new[] { (-1, "Left"), (1, "Right") })
        {
            var chains = GetFingerChains(animator, charRoot, rigGO.transform, side);
            if (chains.Count == 0)
            {
                log.Add($"✘ {sideName}: 손가락 본을 못 찾음");
                continue;
            }

            var sideGO = new GameObject($"{sideName}Hand_Fingers");
            Undo.RegisterCreatedObjectUndo(sideGO, "Create Finger Group");
            sideGO.transform.SetParent(rigGO.transform, false);

            foreach (var (root, tip) in chains)
                CreateFingerIK(sideGO.transform, root, tip);

            log.Add($"✔ {sideName}: {chains.Count}개 ({string.Join(", ", chains.Select(c => c.root.name))})");
        }

        Undo.CollapseUndoOperations(group);
        Selection.activeGameObject = rigGO;
        EditorUtility.DisplayDialog("Finger IK 완료", string.Join("\n", log), "확인");
    }

    [MenuItem("Tools/Auto IK/Add Finger IK", true)]
    static bool ValidateFingers() => Selection.activeGameObject != null;

    static List<(Transform root, Transform tip)> GetFingerChains(Animator anim, Transform charRoot, Transform rigT, int side)
    {
        var result = new List<(Transform root, Transform tip)>();

        // Humanoid: 아바타 매핑 사용
        if (anim && anim.isHuman)
        {
            foreach (var (p, d) in side < 0 ? LeftFingerBones : RightFingerBones)
            {
                var root = anim.GetBoneTransform(p);
                var tip = anim.GetBoneTransform(d);
                if (!root || !tip) continue;
                // 손가락 끝 본(end/leaf)이 있으면 그걸 끝점으로 → 손끝 위치를 직접 잡을 수 있음
                if (tip.childCount > 0) tip = tip.GetChild(0);
                result.Add((root, tip));
            }
            if (result.Count > 0) return result;
        }

        // Generic (또는 아바타에 손가락 매핑이 없을 때): 손 본의 자식들을 손가락으로 간주
        Transform hand = anim && anim.isHuman
            ? anim.GetBoneTransform(side < 0 ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand)
            : FindTip(charRoot, rigT, side < 0 ? Limb.LeftArm : Limb.RightArm);
        if (!hand) return result;

        foreach (var fingerRoot in FingerRoots(hand))
        {
            var tip = FollowChain(fingerRoot);
            if (tip != fingerRoot) result.Add((fingerRoot, tip));
        }
        return result;
    }

    static bool IsFingerSkip(Transform t) => FingerSkipKeys.Any(t.name.ToLowerInvariant().Contains);

    // 손 바로 아래 자식 = 손가락 뿌리. Rigify처럼 palm 본이 끼어 있으면 그 아래로 내려감.
    static IEnumerable<Transform> FingerRoots(Transform hand)
    {
        foreach (Transform c in hand)
        {
            if (IsFingerSkip(c)) continue;
            string n = c.name.ToLowerInvariant();
            if (n.Contains("palm") || n.Contains("metacarpal"))
            {
                foreach (Transform g in c)
                    if (!IsFingerSkip(g)) yield return g;
            }
            else yield return c;
        }
    }

    // 첫 번째 자식을 계속 따라 내려가서 손가락 끝을 찾음
    static Transform FollowChain(Transform t)
    {
        while (true)
        {
            Transform next = null;
            foreach (Transform c in t)
                if (!IsFingerSkip(c)) { next = c; break; }
            if (next == null) return t;
            t = next;
        }
    }

    // 손가락은 마디가 3개라 Two Bone 대신 Chain IK 사용
    static void CreateFingerIK(Transform parent, Transform root, Transform tip)
    {
        var go = new GameObject($"{root.name}_IK");
        Undo.RegisterCreatedObjectUndo(go, "Create Finger IK");
        go.transform.SetParent(parent, false);

        var target = new GameObject($"{root.name}_Target").transform;
        Undo.RegisterCreatedObjectUndo(target.gameObject, "Create Finger Target");
        target.SetParent(go.transform, false);
        target.SetPositionAndRotation(tip.position, tip.rotation);

        var c = Undo.AddComponent<ChainIKConstraint>(go);
        c.weight = 1f;
        c.data.root = root;
        c.data.tip = tip;
        c.data.target = target;
        c.data.chainRotationWeight = 1f;
        c.data.tipRotationWeight = 1f;
        c.data.maxIterations = 15;
        c.data.tolerance = 0.0001f;
        EditorUtility.SetDirty(c);

        var lim = AddLimits(go);
        if (!lim) return;
        lim.kind = IKLimits.Kind.Chain;
        lim.root = root; lim.tip = tip; lim.target = target;
        lim.limitBend = false;
        lim.limitRootSwing = false;
        lim.maxJointAngle = 100f;
        lim.hintFollows = false;
        lim.targetFollows = true;             // 손가락 타겟은 손을 따라다녀야 함
        lim.targetFollowBone = root.parent;   // 손(또는 palm)
        lim.CaptureRestPose();
        EditorUtility.SetDirty(lim);
    }

    static bool limitsWarned;

    // IKLimits가 Editor 폴더에 있으면 컴포넌트로 못 붙임 → 원인을 알려주고 IK만 생성
    static IKLimits AddLimits(GameObject go)
    {
        var lim = Undo.AddComponent<IKLimits>(go);
        if (!lim && !limitsWarned)
        {
            limitsWarned = true;
            EditorUtility.DisplayDialog("Auto IK",
                "IKLimits 컴포넌트를 붙일 수 없어요.\n\n" +
                "IKLimits.cs가 Editor 폴더 안에 있으면 안 돼요. Assets/Scripts 같은 일반 폴더로 옮겨주세요.\n" +
                "(IK는 생성되고, 각도/거리 제한만 빠진 상태로 진행해요)", "확인");
        }
        return lim;
    }

    static void EnsureFingerLayerLast(RigBuilder rb)
    {
        int i = rb.layers.FindIndex(l => l.rig && l.rig.name == FingerRigName);
        if (i < 0 || i == rb.layers.Count - 1) return;
        var layer = rb.layers[i];
        rb.layers.RemoveAt(i);
        rb.layers.Add(layer);
    }

    // ───────────────────── 본 찾기 ─────────────────────

    static bool TryGetChain(Animator anim, Transform charRoot, Transform rigT, Limb limb,
                            out Transform root, out Transform mid, out Transform tip)
    {
        root = mid = tip = null;

        if (anim && anim.isHuman)
        {
            var (a, b, c) = limb switch
            {
                Limb.LeftArm => (HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand),
                Limb.RightArm => (HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand),
                Limb.LeftLeg => (HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot),
                _ => (HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot),
            };
            root = anim.GetBoneTransform(a);
            mid = anim.GetBoneTransform(b);
            tip = anim.GetBoneTransform(c);
        }
        else
        {
            // Generic 리그: 손/발 본을 이름으로 찾고 부모 2단계를 체인으로 사용
            tip = FindTip(charRoot, rigT, limb);
            if (tip)
            {
                mid = tip.parent;
                root = mid ? mid.parent : null;
            }
        }
        return root && mid && tip;
    }

    static Transform FindTip(Transform charRoot, Transform rigT, Limb limb)
    {
        bool isArm = limb == Limb.LeftArm || limb == Limb.RightArm;
        int side = (limb == Limb.LeftArm || limb == Limb.LeftLeg) ? -1 : 1;
        var keys = isArm ? HandKeys : FootKeys;

        Transform best = null;
        int bestDepth = int.MaxValue;

        foreach (var t in charRoot.GetComponentsInChildren<Transform>(true))
        {
            if (t.IsChildOf(rigT)) continue;
            string n = t.name.ToLowerInvariant();
            if (!keys.Any(n.Contains)) continue;
            if (SkipKeys.Any(n.Contains)) continue;
            if (Side(t.name) != side) continue;
            if (t.parent == null || t.parent.parent == null) continue;

            int d = Depth(t, charRoot);
            if (d < bestDepth) { best = t; bestDepth = d; } // 가장 상위(손가락 말고 손목)를 선택
        }
        return best;
    }

    // -1 = 왼쪽, 1 = 오른쪽, 0 = 모름
    // 대응: LeftHand, hand.L, hand_L, L_Hand, Bip01 L Hand, mixamorig:LeftHand, HandL ...
    static int Side(string raw)
    {
        string n = raw.ToLowerInvariant();
        if (n.Contains("left")) return -1;
        if (n.Contains("right")) return 1;
        if (Regex.IsMatch(n, @"(^|[^a-z])l([^a-z]|$)") || Regex.IsMatch(raw, @"[a-z]L$")) return -1;
        if (Regex.IsMatch(n, @"(^|[^a-z])r([^a-z]|$)") || Regex.IsMatch(raw, @"[a-z]R$")) return 1;
        return 0;
    }

    static int Depth(Transform t, Transform root)
    {
        int d = 0;
        while (t != null && t != root) { t = t.parent; d++; }
        return d;
    }

    // ───────────────────── IK 생성 ─────────────────────

    static void CreateIK(Transform rigT, Transform charRoot, Limb limb, Transform root, Transform mid, Transform tip)
    {
        var go = new GameObject($"{limb}_IK");
        Undo.RegisterCreatedObjectUndo(go, "Create IK");
        go.transform.SetParent(rigT, false);

        var target = new GameObject($"{limb}_Target").transform;
        Undo.RegisterCreatedObjectUndo(target.gameObject, "Create Target");
        target.SetParent(go.transform, false);
        target.SetPositionAndRotation(tip.position, tip.rotation);

        var hint = new GameObject($"{limb}_Hint").transform;
        Undo.RegisterCreatedObjectUndo(hint.gameObject, "Create Hint");
        hint.SetParent(go.transform, false);
        hint.position = HintPosition(charRoot, limb, root, mid, tip);

        var c = Undo.AddComponent<TwoBoneIKConstraint>(go);
        c.weight = 1f;
        c.data.root = root;
        c.data.mid = mid;
        c.data.tip = tip;
        c.data.target = target;
        c.data.hint = hint;
        c.data.targetPositionWeight = 1f;
        c.data.targetRotationWeight = 1f;
        c.data.hintWeight = 1f;
        EditorUtility.SetDirty(c);

        // 각도/거리 제한 + 따라오기
        bool isArm = limb == Limb.LeftArm || limb == Limb.RightArm;
        var lim = AddLimits(go);
        if (!lim) return;
        lim.kind = IKLimits.Kind.TwoBone;
        lim.root = root; lim.mid = mid; lim.tip = tip;
        lim.target = target; lim.hint = hint;
        lim.maxBend = isArm ? 150f : 145f;
        lim.maxRootSwing = isArm ? 120f : 100f;
        lim.hintFollows = true;
        lim.hintFollowBone = root.parent;     // 팔: 쇄골/어깨, 다리: 골반 → 몸이 돌아도 팔꿈치/무릎 방향 유지
        lim.targetFollows = false;            // 손발 타겟은 기본적으로 제자리 (발 고정용)
        lim.targetFollowBone = root.parent;
        lim.CaptureRestPose();
        EditorUtility.SetDirty(lim);
    }

    // 팔꿈치/무릎이 이미 굽혀진 방향으로 힌트를 배치. 일자로 펴져 있으면 기본 방향 사용.
    static Vector3 HintPosition(Transform charRoot, Limb limb, Transform root, Transform mid, Transform tip)
    {
        Vector3 a = root.position, b = mid.position, c = tip.position;
        Vector3 onLine = a + Vector3.Project(b - a, c - a);
        Vector3 dir = b - onLine;
        float length = (b - a).magnitude + (c - b).magnitude;

        if (dir.sqrMagnitude < 1e-6f)
        {
            bool isArm = limb == Limb.LeftArm || limb == Limb.RightArm;
            dir = isArm ? -charRoot.forward : charRoot.forward; // 팔꿈치는 뒤, 무릎은 앞
        }
        return b + dir.normalized * length * 0.5f;
    }

    // 씬 뷰에서 본이 보이도록 Bone Renderer 세팅
    static void SetupBoneRenderer(Transform charRoot)
    {
        var bones = charRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                            .SelectMany(s => s.bones)
                            .Where(b => b != null)
                            .Distinct()
                            .ToArray();
        if (bones.Length == 0) return;

        var br = charRoot.GetComponent<BoneRenderer>();
        if (!br) br = Undo.AddComponent<BoneRenderer>(charRoot.gameObject);
        Undo.RecordObject(br, "Setup BoneRenderer");
        br.transforms = bones;
        PrefabUtility.RecordPrefabInstancePropertyModifications(br);
    }
}
#endif