#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// IK 설정 전용 창.  Tools > IK > IK 설정 창
///
/// 캐릭터를 선택하면 체인 목록이 뜨고, 여기서 전부 제어한다.
/// 인스펙터를 왔다 갔다 할 필요가 없다.
///
/// Assets/Editor/ 안에 두어야 한다.
/// </summary>
public class IKWindow : EditorWindow
{
    [MenuItem("Tools/IK/IK 설정 창 %#i")]
    public static void Open()
    {
        var w = GetWindow<IKWindow>("IK Setup");
        w.minSize = new Vector2(320, 400);
        w.Show();
    }

    GameObject owner;
    bool lockSelection;
    Vector2 scroll;

    // 씬 핸들
    int activeIndex = 0;
    bool showHandles = true;
    bool showRotationHandle = true;
    bool showHintHandle = true;

    readonly List<SimpleTwoBoneIK> chains = new List<SimpleTwoBoneIK>();
    List<Transform> leftovers = new List<Transform>();


    void OnEnable()
    {
        Refresh();
        Selection.selectionChanged += OnSel;
        EditorApplication.hierarchyChanged += Refresh;
        SceneView.duringSceneGui += DuringScene;
    }

    void OnDisable()
    {
        Selection.selectionChanged -= OnSel;
        EditorApplication.hierarchyChanged -= Refresh;
        SceneView.duringSceneGui -= DuringScene;
        SceneView.RepaintAll();
    }

    void OnSel() { if (!lockSelection) { Refresh(); Repaint(); } }

    void Refresh()
    {
        if (!lockSelection)
        {
            var sel = Selection.activeGameObject;
            if (sel != null)
            {
                var anim = sel.GetComponent<Animator>() ?? sel.GetComponentInParent<Animator>();
                owner = anim != null ? anim.gameObject : sel;
            }
        }

        chains.Clear();
        leftovers.Clear();

        if (owner == null) return;

        chains.AddRange(owner.GetComponentsInChildren<SimpleTwoBoneIK>(true));
        leftovers = SimpleTwoBoneIK.FindRigLeftovers(owner.transform);
    }


    void OnGUI()
    {
        DrawTarget();
        if (owner == null)
        {
            EditorGUILayout.HelpBox("캐릭터(Animator가 붙은 오브젝트)를 선택하세요.", MessageType.Info);
            return;
        }

        EditorGUILayout.Space(4);
        DrawLeftoverWarning();
        DrawCreateButtons();

        EditorGUILayout.Space(6);
        DrawGlobalPreview();

        EditorGUILayout.Space(6);
        DrawHandleOptions();

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField($"체인 {chains.Count}개", EditorStyles.boldLabel);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (int i = 0; i < chains.Count; i++) DrawChain(chains[i]);
        EditorGUILayout.EndScrollView();
    }


    // ────────────────────────── 대상 ──────────────────────────

    void DrawTarget()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            EditorGUILayout.LabelField("대상", GUILayout.Width(32));

            using (new EditorGUI.DisabledScope(lockSelection))
            {
                var newOwner = (GameObject)EditorGUILayout.ObjectField(
                    owner, typeof(GameObject), true);
                if (newOwner != owner) { owner = newOwner; Refresh(); }
            }

            bool newLock = GUILayout.Toggle(lockSelection, "고정", EditorStyles.toolbarButton,
                                            GUILayout.Width(40));
            if (newLock != lockSelection) { lockSelection = newLock; Refresh(); }

            if (GUILayout.Button("새로고침", EditorStyles.toolbarButton, GUILayout.Width(60)))
                Refresh();
        }
    }


    // ────────────────────────── 잔재 경고 ──────────────────────────

    void DrawLeftoverWarning()
    {
        if (leftovers.Count == 0) return;

        EditorGUILayout.HelpBox(
            $"IK 리그 잔재로 보이는 오브젝트 {leftovers.Count}개가 있습니다.\n" +
            "뼈 자동 찾기가 이걸 잘못 집을 수 있으니 지우는 게 좋습니다.",
            MessageType.Warning);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("잔재 선택해서 보기"))
            {
                var objs = new List<Object>();
                foreach (var t in leftovers) objs.Add(t.gameObject);
                Selection.objects = objs.ToArray();
                lockSelection = true;
            }
            GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
            if (GUILayout.Button("전부 삭제"))
            {
                if (EditorUtility.DisplayDialog("잔재 삭제",
                    $"{leftovers.Count}개 오브젝트를 지웁니다.\n되돌리기(Ctrl+Z) 가능합니다.", "삭제", "취소"))
                {
                    foreach (var t in leftovers)
                        if (t != null) Undo.DestroyObjectImmediate(t.gameObject);
                    Refresh();
                }
            }
            GUI.backgroundColor = Color.white;
        }

        foreach (var t in leftovers)
            if (t != null) EditorGUILayout.LabelField("   • " + t.name, EditorStyles.miniLabel);
    }


    // ────────────────────────── 만들기 ──────────────────────────

    void DrawCreateButtons()
    {
        EditorGUILayout.LabelField("만들기", EditorStyles.boldLabel);

        GUI.backgroundColor = new Color(0.6f, 0.9f, 1f);
        if (GUILayout.Button("전신 IK 만들기 (양팔 + 양다리)", GUILayout.Height(30)))
            Build(new[]
            {
                SimpleTwoBoneIK.ChainPreset.RightArm,
                SimpleTwoBoneIK.ChainPreset.LeftArm,
                SimpleTwoBoneIK.ChainPreset.RightLeg,
                SimpleTwoBoneIK.ChainPreset.LeftLeg
            });
        GUI.backgroundColor = Color.white;

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("양팔만"))
                Build(new[] { SimpleTwoBoneIK.ChainPreset.RightArm, SimpleTwoBoneIK.ChainPreset.LeftArm });
            if (GUILayout.Button("양다리만"))
                Build(new[] { SimpleTwoBoneIK.ChainPreset.RightLeg, SimpleTwoBoneIK.ChainPreset.LeftLeg });
            if (GUILayout.Button("빈 체인 추가"))
                Build(new[] { SimpleTwoBoneIK.ChainPreset.Custom });
        }

        using (new EditorGUI.DisabledScope(chains.Count == 0))
        {
            GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
            if (GUILayout.Button($"이 캐릭터의 IK 전부 삭제 ({chains.Count}개)"))
                DeleteAll();
            GUI.backgroundColor = Color.white;
        }
    }


    // ────────────────────────── 삭제 ──────────────────────────

    void DeleteAll()
    {
        if (!EditorUtility.DisplayDialog("IK 전부 삭제",
            $"체인 {chains.Count}개와 Target / Hint를 지웁니다.\n" +
            "뼈는 원래 포즈로 되돌립니다.\n\n되돌리기(Ctrl+Z) 가능합니다.",
            "삭제", "취소")) return;

        var copy = new List<SimpleTwoBoneIK>(chains);
        foreach (var ik in copy) DeleteChain(ik);

        // 채널이 다 비었으면 IKDriver도 치운다
        var driver = owner != null ? owner.GetComponent<IKDriver>() : null;
        if (driver != null && (driver.channels == null || driver.channels.Length == 0))
        {
            if (EditorUtility.DisplayDialog("IKDriver",
                "IKDriver에 남은 체인이 없습니다. 같이 지울까요?", "지우기", "남겨두기"))
                Undo.DestroyObjectImmediate(driver);
        }

        activeIndex = 0;
        Refresh();
    }

    /// <summary>체인 하나를 지운다. 뼈는 원래 포즈로 되돌린다</summary>
    void DeleteChain(SimpleTwoBoneIK ik)
    {
        if (ik == null) return;

        // 1) 미리보기로 바뀐 포즈를 먼저 되돌린다
        if (ik.IsValid)
        {
            Undo.RegisterFullObjectHierarchyUndo(ik.gameObject, "Delete IK");
            ik.DisablePreview();
        }

        // 2) IKDriver에서 참조 제거
        var driver = owner != null ? owner.GetComponent<IKDriver>() : null;
        if (driver != null && driver.channels != null)
        {
            var keep = new List<IKDriver.Channel>();
            foreach (var c in driver.channels)
                if (c != null && c.ik != ik) keep.Add(c);

            if (keep.Count != driver.channels.Length)
            {
                Undo.RecordObject(driver, "Remove IK Channel");
                driver.channels = keep.ToArray();
                EditorUtility.SetDirty(driver);
            }
        }

        GameObject go = ik.gameObject;

        // 3) 이 도구가 만든 오브젝트면 통째로, 아니면 컴포넌트만 지운다
        bool toolMade = go.name.StartsWith("IK_") &&
                        go.GetComponents<Component>().Length <= 2;   // Transform + SimpleTwoBoneIK

        if (toolMade)
        {
            Undo.DestroyObjectImmediate(go);
            return;
        }

        // Target / Hint가 이 오브젝트의 자식이면 같이 지운다
        if (ik.target != null && ik.target.parent == go.transform)
            Undo.DestroyObjectImmediate(ik.target.gameObject);
        if (ik.hint != null && ik.hint.parent == go.transform)
            Undo.DestroyObjectImmediate(ik.hint.gameObject);

        Undo.DestroyObjectImmediate(ik);
    }

    void Build(SimpleTwoBoneIK.ChainPreset[] presets)
    {
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

            var ik = go.GetComponent<SimpleTwoBoneIK>() ?? Undo.AddComponent<SimpleTwoBoneIK>(go);

            Undo.RecordObject(ik, "Setup IK");
            ik.preset = preset;
            ik.searchRoot = owner.transform;

            if (preset != SimpleTwoBoneIK.ChainPreset.Custom && !ik.AutoFindBones())
            {
                failed.Add(preset.ToString());
                EditorUtility.SetDirty(ik);
                continue;
            }

            if (ik.root != null && ik.tip != null) ik.AutoCreateTargetAndHint();
            ik.weight = 0f;
            EditorUtility.SetDirty(ik);
            made.Add(ik);

            if (ik.root != null)
                Debug.Log($"[IK] {preset} — {ik.root.name} → {ik.mid.name} → {ik.tip.name}", ik);
        }

        if (made.Count > 0) WireDriver(made);

        Refresh();

        if (failed.Count > 0)
            EditorUtility.DisplayDialog("일부 실패",
                $"찾지 못한 체인: {string.Join(", ", failed)}\n\n" +
                "뼈 이름을 확인하거나 Custom으로 직접 지정하세요.", "확인");
    }

    void WireDriver(List<SimpleTwoBoneIK> made)
    {
        var driver = owner.GetComponent<IKDriver>() ?? Undo.AddComponent<IKDriver>(owner);
        Undo.RecordObject(driver, "Setup IKDriver");

        var list = new List<IKDriver.Channel>(driver.channels ?? new IKDriver.Channel[0]);
        foreach (var ik in made)
        {
            string id = ToId(ik);
            var found = list.Find(c => c != null && c.id == id);
            if (found != null) found.ik = ik;
            else list.Add(new IKDriver.Channel { id = id, ik = ik, blendTime = 0.15f });
        }
        driver.channels = list.ToArray();
        EditorUtility.SetDirty(driver);
    }

    static string ToId(SimpleTwoBoneIK ik)
    {
        switch (ik.preset)
        {
            case SimpleTwoBoneIK.ChainPreset.RightArm: return "rightArm";
            case SimpleTwoBoneIK.ChainPreset.LeftArm: return "leftArm";
            case SimpleTwoBoneIK.ChainPreset.RightLeg: return "rightLeg";
            case SimpleTwoBoneIK.ChainPreset.LeftLeg: return "leftLeg";
            default: return ik.gameObject.name;
        }
    }


    // ────────────────────────── 전체 미리보기 ──────────────────────────

    void DrawGlobalPreview()
    {
        int on = 0;
        foreach (var c in chains) if (c != null && c.editorPreview) on++;

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(chains.Count == 0))
            {
                if (GUILayout.Button("전체 미리보기 켜기", GUILayout.Height(26)))
                    foreach (var c in chains) SetPreview(c, true);

                GUI.backgroundColor = on > 0 ? new Color(1f, 0.85f, 0.4f) : Color.white;
                if (GUILayout.Button("전체 끄기 (원래 포즈로)", GUILayout.Height(26)))
                    foreach (var c in chains) SetPreview(c, false);
                GUI.backgroundColor = Color.white;
            }
        }

        if (on > 0)
            EditorGUILayout.HelpBox($"미리보기 {on}개 켜짐 — 지금 뼈 포즈가 씬에 저장됩니다. 끝나면 끄세요.",
                                    MessageType.Warning);
    }

    static void SetPreview(SimpleTwoBoneIK ik, bool on)
    {
        if (ik == null || !ik.IsValid) return;

        Undo.RegisterFullObjectHierarchyUndo(ik.gameObject, on ? "IK Preview On" : "IK Preview Off");
        if (on)
        {
            ik.EnablePreview();
            if (ik.weight <= 0f) ik.weight = 1f;
        }
        else ik.DisablePreview();

        EditorUtility.SetDirty(ik);
    }


    // ────────────────────────── 체인 하나 ──────────────────────────

    void DrawChain(SimpleTwoBoneIK ik)
    {
        if (ik == null) return;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            int myIndex = chains.IndexOf(ik);
            bool isActive = (myIndex == activeIndex);

            // 제목줄
            using (new EditorGUILayout.HorizontalScope())
            {
                bool prev = ik.editorPreview;
                bool now = GUILayout.Toggle(prev, prev ? "■" : "▶", EditorStyles.miniButton,
                                            GUILayout.Width(26));
                if (now != prev) SetPreview(ik, now);

                // 핸들을 띄울 체인 고르기
                bool newActive = GUILayout.Toggle(isActive, "◉", EditorStyles.miniButton,
                                                  GUILayout.Width(26));
                if (newActive && !isActive) { activeIndex = myIndex; SceneView.RepaintAll(); }

                var style = isActive ? EditorStyles.boldLabel : EditorStyles.label;
                EditorGUILayout.LabelField(ToId(ik), style);

                if (GUILayout.Button("선택", EditorStyles.miniButton, GUILayout.Width(40)))
                    Selection.activeGameObject = ik.gameObject;

                GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
                if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(24)))
                {
                    DeleteChain(ik);
                    Refresh();
                    GUIUtility.ExitGUI();          // 목록이 바뀌었으니 이번 GUI는 중단
                }
                GUI.backgroundColor = Color.white;
            }

            // 체인 표시
            if (ik.IsValid || (ik.root != null && ik.tip != null))
                EditorGUILayout.LabelField($"{ik.root.name} → {ik.mid.name} → {ik.tip.name}",
                                           EditorStyles.miniLabel);
            else
            {
                EditorGUILayout.HelpBox("뼈가 비었습니다.", MessageType.Warning);
                if (GUILayout.Button("뼈 자동 찾기"))
                {
                    Undo.RecordObject(ik, "Auto Find");
                    if (ik.AutoFindBones()) { ik.AutoCreateTargetAndHint(); EditorUtility.SetDirty(ik); }
                    else EditorUtility.DisplayDialog("실패", "뼈를 찾지 못했습니다. 직접 지정하세요.", "확인");
                }
            }

            // Weight
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Weight", GUILayout.Width(50));
                float w = EditorGUILayout.Slider(ik.weight, 0f, 1f);
                if (!Mathf.Approximately(w, ik.weight))
                {
                    Undo.RecordObject(ik, "IK Weight");
                    ik.weight = w;
                    EditorUtility.SetDirty(ik);
                }
            }

            // 목표 버튼들
            using (new EditorGUI.DisabledScope(ik.root == null || ik.tip == null))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Target 선택", EditorStyles.miniButton))
                {
                    if (ik.target != null) Selection.activeGameObject = ik.target.gameObject;
                    lockSelection = true;
                }
                if (GUILayout.Button("현재 위치로", EditorStyles.miniButton))
                {
                    if (ik.target != null) Undo.RecordObject(ik.target, "Snap");
                    ik.SnapTargetToTip();
                }
                if (GUILayout.Button("Hint 재계산", EditorStyles.miniButton))
                {
                    if (ik.hint != null)
                    {
                        Undo.RecordObject(ik.hint, "Recalc Hint");
                        ik.hint.position = ik.CalcHintPosition();
                    }
                }
            }
        }
    }


    // ────────────────────────── 핸들 설정 UI ──────────────────────────

    void DrawHandleOptions()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            bool h = EditorGUILayout.ToggleLeft("씬에 뼈 선 / 점 표시", showHandles);
            if (h != showHandles) { showHandles = h; SceneView.RepaintAll(); }

            using (new EditorGUI.DisabledScope(!showHandles))
            using (new EditorGUILayout.HorizontalScope())
            {
                bool r = EditorGUILayout.ToggleLeft("Target 회전", showRotationHandle);
                if (r != showRotationHandle) { showRotationHandle = r; SceneView.RepaintAll(); }

                bool hi = EditorGUILayout.ToggleLeft("Hint", showHintHandle);
                if (hi != showHintHandle) { showHintHandle = hi; SceneView.RepaintAll(); }
            }

            EditorGUILayout.LabelField("◉ 표시한 체인에만 이동/회전 핸들이 뜹니다.",
                                       EditorStyles.miniLabel);
        }
    }


    // ────────────────────────── 씬 뷰 ──────────────────────────

    void DuringScene(SceneView sv)
    {
        if (!showHandles || owner == null || chains.Count == 0) return;

        for (int i = 0; i < chains.Count; i++)
        {
            var ik = chains[i];
            if (ik == null || ik.root == null || ik.mid == null || ik.tip == null) continue;

            bool active = (i == activeIndex);
            DrawChainHandles(ik, active, i);
        }

        // 핸들을 쓰는 동안 씬이 계속 갱신되게
        if (Event.current.type == EventType.MouseDrag) SceneView.RepaintAll();
    }

    void DrawChainHandles(SimpleTwoBoneIK ik, bool active, int index)
    {
        Color boneCol = active ? new Color(0.4f, 1f, 1f) : new Color(0.3f, 0.6f, 0.7f, 0.5f);

        // ── 뼈 선 ──────────────────────────────────
        Handles.color = boneCol;
        Handles.DrawAAPolyLine(active ? 4f : 2f,
                               ik.root.position, ik.mid.position, ik.tip.position);

        // ── 관절 점 (누르면 그 뼈가 선택된다) ─────────
        DrawJoint(ik.root, boneCol, active, "Root");
        DrawJoint(ik.mid, boneCol, active, ik.JointName);
        DrawJoint(ik.tip, boneCol, active, "Tip");

        // ── Target ────────────────────────────────
        if (ik.target != null)
        {
            Handles.color = new Color(1f, 0.8f, 0.2f);
            Handles.DrawDottedLine(ik.tip.position, ik.target.position, 3f);

            if (active)
            {
                EditorGUI.BeginChangeCheck();
                Vector3 p = Handles.PositionHandle(ik.target.position, ik.target.rotation);
                Quaternion q = ik.target.rotation;
                if (showRotationHandle)
                    q = Handles.RotationHandle(ik.target.rotation, ik.target.position);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(ik.target, "Move IK Target");
                    ik.target.position = p;
                    ik.target.rotation = q;
                }
                Handles.Label(ik.target.position, "  Target");
            }
            else
            {
                float s = HandleUtility.GetHandleSize(ik.target.position) * 0.06f;
                if (Handles.Button(ik.target.position, Quaternion.identity, s, s,
                                   Handles.SphereHandleCap))
                {
                    activeIndex = index;
                    Repaint();
                }
            }
        }

        // ── Hint ──────────────────────────────────
        if (ik.hint != null && showHintHandle)
        {
            Handles.color = new Color(1f, 0.4f, 0.8f);
            Handles.DrawDottedLine(ik.mid.position, ik.hint.position, 2f);

            if (active)
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
            else
            {
                float s = HandleUtility.GetHandleSize(ik.hint.position) * 0.05f;
                Handles.SphereHandleCap(0, ik.hint.position, Quaternion.identity, s,
                                        EventType.Repaint);
            }
        }
    }

    /// <summary>관절 점. 누르면 그 뼈 오브젝트가 선택돼서 유니티 기본 툴로 돌릴 수 있다</summary>
    void DrawJoint(Transform bone, Color col, bool active, string label)
    {
        if (bone == null) return;

        float size = HandleUtility.GetHandleSize(bone.position) * (active ? 0.05f : 0.035f);

        Handles.color = col;
        if (Handles.Button(bone.position, Quaternion.identity, size, size, Handles.SphereHandleCap))
        {
            Selection.activeGameObject = bone.gameObject;
            lockSelection = true;        // 창의 대상이 바뀌지 않게
            Repaint();
        }

        if (active)
        {
            Handles.color = Color.white;
            Handles.Label(bone.position, "  " + label);
        }
    }
}


/// <summary>
/// 에디터에서 미리보기가 켜진 IK를 계속 풀어준다.
/// 인스펙터나 창이 닫혀 있어도 동작한다.
/// </summary>
[InitializeOnLoad]
static class IKPreviewDriver
{
    static double next;
    static SimpleTwoBoneIK[] cache;
    static double cacheTime;

    static IKPreviewDriver()
    {
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if (Application.isPlaying) return;

        double now = EditorApplication.timeSinceStartup;
        if (now < next) return;
        next = now + 0.02;                      // 초당 50회면 충분하다

        // 목록은 가끔만 다시 모은다 (매 프레임 Find는 느리다)
        if (cache == null || now - cacheTime > 1.0)
        {
            cache = Object.FindObjectsByType<SimpleTwoBoneIK>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            cacheTime = now;
        }

        bool any = false;
        foreach (var ik in cache)
        {
            if (ik == null || !ik.editorPreview || !ik.IsValid || ik.weight <= 0f) continue;
            ik.SolveNow();
            any = true;
        }

        if (any) SceneView.RepaintAll();
    }
}
#endif