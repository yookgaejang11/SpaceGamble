#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 설정 창 UI를 통째로 만들어 주는 도구.  Tools > UI > 설정 창 만들기
///
/// 만들어지는 것
///   패널 + 제목 + 섹션 + 모든 컨트롤 + SettingsPanel 연결까지 전부
///
/// 폰트는 창에서 지정하면 만들 때 같이 적용된다.
/// 나중에 바꾸고 싶으면 "폰트만 다시 적용" 버튼을 쓰면 된다.
///
/// Assets/Editor/ 안에 두어야 한다.
///
/// TMP 드롭다운처럼 내부 구조가 복잡한 건 직접 조립하지 않고
/// 유니티의 GameObject > UI 메뉴를 그대로 실행해서 만든다.
/// 버전이 바뀌어도 깨지지 않는 방법이다.
/// </summary>
public class SettingsUIBuilder : EditorWindow
{
    [MenuItem("Tools/UI/설정 창 만들기")]
    public static void Open()
    {
        var w = GetWindow<SettingsUIBuilder>("설정 창 만들기");
        w.minSize = new Vector2(340, 430);
        w.Show();
    }

    // ────────────────────────── 설정값 ──────────────────────────

    TMP_FontAsset font;

    Color bgColor = new Color(0.043f, 0.059f, 0.075f, 0.97f);
    Color sectionColor = new Color(0.071f, 0.094f, 0.125f, 1f);
    Color accent = new Color(0f, 0.78f, 1f, 1f);
    Color textColor = new Color(0.81f, 0.90f, 0.95f, 1f);
    Color dimColor = new Color(0.49f, 0.58f, 0.64f, 1f);

    Vector2 panelSize = new Vector2(560f, 700f);
    bool includeCloseButton = true;
    bool startInactive = true;

    GameObject lastMade;


    void OnGUI()
    {
        EditorGUILayout.LabelField("폰트", EditorStyles.boldLabel);
        font = (TMP_FontAsset)EditorGUILayout.ObjectField(
            "TMP Font Asset", font, typeof(TMP_FontAsset), false);
        EditorGUILayout.HelpBox("비워두면 TMP 기본 폰트로 만들어집니다. 나중에 아래 버튼으로 바꿀 수 있습니다.",
                                MessageType.None);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("색", EditorStyles.boldLabel);
        bgColor = EditorGUILayout.ColorField("패널 배경", bgColor);
        sectionColor = EditorGUILayout.ColorField("섹션 배경", sectionColor);
        accent = EditorGUILayout.ColorField("강조색", accent);
        textColor = EditorGUILayout.ColorField("글자", textColor);
        dimColor = EditorGUILayout.ColorField("흐린 글자", dimColor);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("모양", EditorStyles.boldLabel);
        panelSize = EditorGUILayout.Vector2Field("패널 크기", panelSize);
        includeCloseButton = EditorGUILayout.Toggle("닫기 버튼", includeCloseButton);
        startInactive = EditorGUILayout.Toggle("만들고 바로 끄기", startInactive);

        EditorGUILayout.Space(12);

        GUI.backgroundColor = new Color(0.6f, 0.9f, 1f);
        if (GUILayout.Button("설정 창 만들기", GUILayout.Height(32)))
            Build();
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(4);

        using (new EditorGUI.DisabledScope(Selection.activeGameObject == null))
        {
            if (GUILayout.Button("선택한 UI에 폰트만 다시 적용"))
                ApplyFont(Selection.activeGameObject);
        }

        if (lastMade != null)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox($"만들어짐: {lastMade.name}\n" +
                                    "PauseMenu에서 켜고 끄면 됩니다.", MessageType.Info);
            if (GUILayout.Button("만든 패널 선택")) Selection.activeGameObject = lastMade;
        }
    }


    // ────────────────────────── 만들기 ──────────────────────────

    void Build()
    {
        Canvas canvas = FindOrMakeCanvas();

        // 패널 루트
        GameObject panel = MakeRect("SettingsPanel", canvas.transform);
        var panelRt = (RectTransform)panel.transform;
        panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.anchoredPosition = Vector2.zero;
        panelRt.sizeDelta = panelSize;

        var bg = panel.AddComponent<Image>();
        bg.color = bgColor;

        var vl = panel.AddComponent<VerticalLayoutGroup>();
        vl.padding = new RectOffset(24, 24, 20, 20);
        vl.spacing = 10;
        vl.childForceExpandHeight = false;
        vl.childForceExpandWidth = true;
        vl.childControlHeight = true;
        vl.childControlWidth = true;

        // 윗부분 강조선
        MakeAccentLine(panel.transform);

        // 제목
        var title = MakeLabel(panel.transform, "SETTINGS", 26, accent);
        title.alignment = TextAlignmentOptions.Center;
        SetHeight(title.gameObject, 40);

        var refs = new Refs();

        // ── 마이크 ──────────────────────────────────
        Transform sec = MakeSection(panel.transform, "마이크");

        refs.micMode = MakeDropdownRow(sec, "입력 방식");
        var keyRow = MakeButtonRow(sec, "말하기 키", "V");
        refs.pttButton = keyRow.button;
        refs.pttLabel = keyRow.label;
        refs.selfMute = MakeToggleRow(sec, "내 마이크 끄기");
        refs.inDevice = MakeDropdownRow(sec, "마이크 장치");

        var level = MakeLevelRow(sec, "입력 레벨");
        refs.levelBar = level;

        // ── 음성 출력 ───────────────────────────────
        sec = MakeSection(panel.transform, "음성 출력");

        refs.outDevice = MakeDropdownRow(sec, "출력 장치");
        refs.refreshBtn = MakeWideButton(sec, "장치 목록 새로고침");
        MakeHint(sec, "음성에만 적용됩니다. 게임 소리는 윈도우 기본 장치로 나갑니다.");

        // ── 음성 볼륨 ───────────────────────────────
        sec = MakeSection(panel.transform, "음성 볼륨");

        var r1 = MakeSliderRow(sec, "마이크 입력", -50, 50, true);
        refs.inVol = r1.slider; refs.inVolLabel = r1.value;

        var r2 = MakeSliderRow(sec, "상대 목소리", -50, 50, true);
        refs.oppVol = r2.slider; refs.oppVolLabel = r2.value;

        // ── 게임 사운드 ─────────────────────────────
        sec = MakeSection(panel.transform, "게임 사운드");

        refs.master = MakeSliderRow(sec, "전체", 0, 1, false).slider;
        refs.bgm = MakeSliderRow(sec, "음악", 0, 1, false).slider;
        refs.sfx = MakeSliderRow(sec, "효과음", 0, 1, false).slider;

        // ── 닫기 ───────────────────────────────────
        Button closeBtn = null;
        if (includeCloseButton)
        {
            MakeSpacer(panel.transform, 6);
            closeBtn = MakeWideButton(panel.transform, "닫기");
            SetHeight(closeBtn.gameObject, 44);
            Recolor(closeBtn, accent, Color.black);
        }

        // ── 스크립트 연결 ───────────────────────────
        var sp = panel.AddComponent<SettingsPanel>();
        sp.micModeDropdown = refs.micMode;
        sp.pttKeyButton = refs.pttButton;
        sp.pttKeyLabel = refs.pttLabel;
        sp.selfMuteToggle = refs.selfMute;
        sp.deviceDropdown = refs.inDevice;
        //sp.outputDeviceDropdown = refs.outDevice;
        //sp.refreshDevicesButton = refs.refreshBtn;
        sp.inputVolumeSlider = refs.inVol;
        sp.inputVolumeLabel = refs.inVolLabel;
        sp.opponentVolumeSlider = refs.oppVol;
        sp.opponentVolumeLabel = refs.oppVolLabel;
        sp.levelBar = refs.levelBar;
        sp.masterSlider = refs.master;
        sp.bgmSlider = refs.bgm;
        sp.sfxSlider = refs.sfx;
        EditorUtility.SetDirty(sp);

        if (closeBtn != null)
        {
            // 닫기 = 자기 자신 끄기
            var target = panel;
            UnityEditor.Events.UnityEventTools.AddBoolPersistentListener(
                closeBtn.onClick,
                target.SetActive,
                false);
        }

        ApplyFont(panel);

        Undo.RegisterCreatedObjectUndo(panel, "Create Settings UI");
        lastMade = panel;
        Selection.activeGameObject = panel;

        if (startInactive) panel.SetActive(false);

        Debug.Log("[SettingsUI] 설정 창을 만들었습니다. PauseMenu에서 켜고 끄세요.", panel);
    }

    class Refs
    {
        public TMP_Dropdown micMode, inDevice, outDevice;
        public Button pttButton, refreshBtn;
        public TextMeshProUGUI pttLabel, inVolLabel, oppVolLabel;
        public Toggle selfMute;
        public Slider inVol, oppVol, master, bgm, sfx;
        public Image levelBar;
    }


    // ────────────────────────── 조각들 ──────────────────────────

    Canvas FindOrMakeCanvas()
    {
        // 선택한 것이 캔버스 안이면 거기에 넣는다
        var sel = Selection.activeGameObject;
        if (sel != null)
        {
            var c = sel.GetComponentInParent<Canvas>();
            if (c != null) return c.rootCanvas;
        }

        var existing = Object.FindFirstObjectByType<Canvas>();
        if (existing != null) return existing.rootCanvas;

        EditorApplication.ExecuteMenuItem("GameObject/UI/Canvas");
        var made = Selection.activeGameObject.GetComponent<Canvas>();
        made.renderMode = RenderMode.ScreenSpaceOverlay;
        return made;
    }

    /// <summary>유니티 메뉴로 UI를 만든다. TMP 내부 구조를 직접 조립하지 않기 위해</summary>
    static GameObject CreateViaMenu(string menuPath, Transform parent, string name)
    {
        Selection.activeGameObject = parent.gameObject;
        EditorApplication.ExecuteMenuItem(menuPath);

        var go = Selection.activeGameObject;
        if (go == null) return null;

        if (go.transform.parent != parent)
            go.transform.SetParent(parent, false);

        go.name = name;
        return go;
    }

    static GameObject MakeRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    void MakeAccentLine(Transform parent)
    {
        var go = MakeRect("AccentLine", parent);
        var img = go.AddComponent<Image>();
        img.color = accent;
        SetHeight(go, 3);
    }

    void MakeSpacer(Transform parent, float h)
    {
        var go = MakeRect("Spacer", parent);
        SetHeight(go, h);
    }

    Transform MakeSection(Transform parent, string title)
    {
        var box = MakeRect("Section_" + title, parent);
        var img = box.AddComponent<Image>();
        img.color = sectionColor;

        var vl = box.AddComponent<VerticalLayoutGroup>();
        vl.padding = new RectOffset(14, 14, 10, 12);
        vl.spacing = 6;
        vl.childForceExpandHeight = false;
        vl.childForceExpandWidth = true;
        vl.childControlHeight = true;
        vl.childControlWidth = true;

        var fit = box.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var head = MakeLabel(box.transform, title, 15, accent);
        head.fontStyle = FontStyles.Bold;
        SetHeight(head.gameObject, 22);

        return box.transform;
    }

    void MakeHint(Transform parent, string text)
    {
        var t = MakeLabel(parent, text, 11, dimColor);
        SetHeight(t.gameObject, 30);
    }

    TextMeshProUGUI MakeLabel(Transform parent, string text, float size, Color col)
    {
        var go = CreateViaMenu("GameObject/UI/Text - TextMeshPro", parent, "Label");
        var t = go.GetComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.color = col;
        t.alignment = TextAlignmentOptions.Left;
        t.raycastTarget = false;
        return t;
    }

    /// <summary>라벨 + 컨트롤이 가로로 놓인 한 줄</summary>
    Transform MakeRow(Transform parent, string label, float height = 34f)
    {
        var row = MakeRect("Row_" + label, parent);

        var hl = row.AddComponent<HorizontalLayoutGroup>();
        hl.spacing = 10;
        hl.childForceExpandHeight = true;
        hl.childForceExpandWidth = false;
        hl.childControlHeight = true;
        hl.childControlWidth = true;
        hl.childAlignment = TextAnchor.MiddleLeft;

        SetHeight(row, height);

        var t = MakeLabel(row.transform, label, 14, textColor);
        t.alignment = TextAlignmentOptions.MidlineLeft;
        SetWidth(t.gameObject, 150);

        return row.transform;
    }

    TMP_Dropdown MakeDropdownRow(Transform parent, string label)
    {
        var row = MakeRow(parent, label);
        var go = CreateViaMenu("GameObject/UI/Dropdown - TextMeshPro", row, "Dropdown");

        var dd = go.GetComponent<TMP_Dropdown>();
        Flex(go);
        StyleSelectable(dd, go.GetComponent<Image>());

        var lab = go.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
        if (lab != null) { lab.color = textColor; lab.fontSize = 13; }

        var arrow = go.transform.Find("Arrow")?.GetComponent<Image>();
        if (arrow != null) arrow.color = accent;

        // 펼쳐지는 목록
        var temp = go.transform.Find("Template");
        if (temp != null)
        {
            var timg = temp.GetComponent<Image>();
            if (timg != null) timg.color = bgColor;

            var itemLab = temp.Find("Viewport/Content/Item/Item Label")?.GetComponent<TextMeshProUGUI>();
            if (itemLab != null) { itemLab.color = textColor; itemLab.fontSize = 13; }

            var itemBg = temp.Find("Viewport/Content/Item/Item Background")?.GetComponent<Image>();
            if (itemBg != null) itemBg.color = sectionColor;

            var check = temp.Find("Viewport/Content/Item/Item Checkmark")?.GetComponent<Image>();
            if (check != null) check.color = accent;
        }

        return dd;
    }

    (Button button, TextMeshProUGUI label) MakeButtonRow(Transform parent, string label, string value)
    {
        var row = MakeRow(parent, label);
        var go = CreateViaMenu("GameObject/UI/Button - TextMeshPro", row, "KeyButton");

        var btn = go.GetComponent<Button>();
        Flex(go);
        StyleSelectable(btn, go.GetComponent<Image>());

        var t = go.GetComponentInChildren<TextMeshProUGUI>();
        t.text = value;
        t.color = accent;
        t.fontSize = 14;

        return (btn, t);
    }

    Toggle MakeToggleRow(Transform parent, string label)
    {
        var row = MakeRow(parent, label);
        var go = CreateViaMenu("GameObject/UI/Toggle", row, "Toggle");

        var tg = go.GetComponent<Toggle>();
        SetWidth(go, 40);

        var bgImg = go.transform.Find("Background")?.GetComponent<Image>();
        if (bgImg != null) bgImg.color = new Color(0.16f, 0.2f, 0.24f);

        var check = go.transform.Find("Background/Checkmark")?.GetComponent<Image>();
        if (check != null) check.color = accent;

        // 토글에 딸려오는 라벨은 안 쓴다 (왼쪽에 이미 있다)
        var tmpLabel = go.transform.Find("Label");
        if (tmpLabel != null) Object.DestroyImmediate(tmpLabel.gameObject);

        return tg;
    }

    (Slider slider, TextMeshProUGUI value) MakeSliderRow(
        Transform parent, string label, float min, float max, bool whole)
    {
        var row = MakeRow(parent, label);

        var go = CreateViaMenu("GameObject/UI/Slider", row, "Slider");
        var s = go.GetComponent<Slider>();
        s.minValue = min;
        s.maxValue = max;
        s.wholeNumbers = whole;
        s.value = whole ? 0f : 1f;
        Flex(go);

        var bgImg = go.transform.Find("Background")?.GetComponent<Image>();
        if (bgImg != null) bgImg.color = new Color(0.13f, 0.16f, 0.2f);

        var fill = go.transform.Find("Fill Area/Fill")?.GetComponent<Image>();
        if (fill != null) fill.color = accent;

        var handle = go.transform.Find("Handle Slide Area/Handle")?.GetComponent<Image>();
        if (handle != null) handle.color = textColor;

        var val = MakeLabel(row, "0", 13, dimColor);
        val.alignment = TextAlignmentOptions.MidlineRight;
        SetWidth(val.gameObject, 52);

        return (s, val);
    }

    Image MakeLevelRow(Transform parent, string label)
    {
        var row = MakeRow(parent, label, 26);

        var back = MakeRect("LevelBarBG", row);
        var backImg = back.AddComponent<Image>();
        backImg.color = new Color(0.13f, 0.16f, 0.2f);
        Flex(back);

        var barGo = MakeRect("LevelBar", back.transform);
        var rt = (RectTransform)barGo.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var bar = barGo.AddComponent<Image>();
        bar.color = accent;
        bar.type = Image.Type.Filled;
        bar.fillMethod = Image.FillMethod.Horizontal;
        bar.fillOrigin = (int)Image.OriginHorizontal.Left;
        bar.fillAmount = 0f;

        return bar;
    }

    Button MakeWideButton(Transform parent, string text)
    {
        var go = CreateViaMenu("GameObject/UI/Button - TextMeshPro", parent, "Button_" + text);
        var btn = go.GetComponent<Button>();
        SetHeight(go, 36);
        StyleSelectable(btn, go.GetComponent<Image>());

        var t = go.GetComponentInChildren<TextMeshProUGUI>();
        t.text = text;
        t.color = textColor;
        t.fontSize = 14;

        return btn;
    }


    // ────────────────────────── 보조 ──────────────────────────

    void StyleSelectable(Selectable sel, Image img)
    {
        if (img != null) img.color = new Color(0.10f, 0.13f, 0.17f);
        if (sel == null) return;

        var c = sel.colors;
        c.normalColor = Color.white;
        c.highlightedColor = new Color(1.3f, 1.3f, 1.3f);
        c.pressedColor = new Color(0.7f, 0.7f, 0.7f);
        c.selectedColor = Color.white;
        c.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        sel.colors = c;
    }

    void Recolor(Button b, Color bg, Color textCol)
    {
        var img = b.GetComponent<Image>();
        if (img != null) img.color = bg;

        var t = b.GetComponentInChildren<TextMeshProUGUI>();
        if (t != null) t.color = textCol;
    }

    static void SetHeight(GameObject go, float h)
    {
        var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        le.minHeight = le.preferredHeight = h;
        le.flexibleHeight = 0;
    }

    static void SetWidth(GameObject go, float w)
    {
        var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        le.minWidth = le.preferredWidth = w;
        le.flexibleWidth = 0;
    }

    static void Flex(GameObject go)
    {
        var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        le.flexibleWidth = 1;
        le.minWidth = 80;
    }

    void ApplyFont(GameObject root)
    {
        if (root == null) return;
        if (font == null) return;

        int n = 0;
        foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
        {
            Undo.RecordObject(t, "Apply Font");
            t.font = font;
            EditorUtility.SetDirty(t);
            n++;
        }
        Debug.Log($"[SettingsUI] 폰트를 {n}개 텍스트에 적용했습니다.", root);
    }
}
#endif
