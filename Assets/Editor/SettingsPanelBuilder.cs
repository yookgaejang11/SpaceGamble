using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 설정 창을 씬에 통째로 만들고 SettingsPanel의 칸까지 연결한다.
/// 메뉴: Tools > Space Gamble > Build Settings Panel
///
/// 좌표는 전부 시안 이미지(2560x1440)의 픽셀값 그대로 적었다.
/// S를 곱해 1920x1080 기준으로 바꾼다.
/// </summary>
public static class SettingsPanelBuilder
{
    const string RootDir = "Assets/SettingsUI";
    const string SpriteDir = RootDir + "/Generated";
    const float S = 0.75f;   // 시안 2560 → 캔버스 1920
    const int SS = 2;        // 스프라이트 해상도 배율 (1 UI 단위 = 2px)

    static readonly Color Cyan = new Color(0.16f, 0.83f, 1f);
    static readonly Color FrameLine = new Color(0.45f, 0.93f, 1f);
    static readonly Color SectionLine = new Color(0.02f, 0.42f, 0.62f);
    static readonly Color FieldLine = new Color(0.30f, 0.38f, 0.46f);
    static readonly Color TextMain = new Color(0.92f, 0.96f, 1f);
    static readonly Color TextMuted = new Color(0.62f, 0.72f, 0.80f);
    static readonly Color TrackGray = new Color(0.68f, 0.72f, 0.76f);
    static readonly Color FieldFill = new Color(0.03f, 0.05f, 0.07f);

    static Sprite sFrame, sSection, sField, sFieldSolid, sPill, sPillBig, sKnob, sDisk, sArrow, sRefresh, sMeter;
    static TMP_FontAsset displayFont, bodyFont;

    // 레이아웃 치수 (UI 단위)
    const float LabelW = 330f;
    const float RightLabelW = 260f;
    const float VoiceRowH = 60f;
    const float SliderRowH = 46f;
    const float ScrollbarW = 18f;


    // ────────────────────────── 진입점 ──────────────────────────

    [MenuItem("Tools/Space Gamble/Build Settings Panel")]
    public static void Build()
    {
        displayFont = FindFont("Orbitron");
        bodyFont = FindFont("Rajdhani");
        if (displayFont == null || bodyFont == null)
        {
            EditorUtility.DisplayDialog("TMP 폰트 없음",
                "Window > TextMeshPro > Import TMP Essential Resources를 먼저 실행해 주세요.", "확인");
            return;
        }

        // 다시 실행했을 때 창이 겹쳐 쌓이지 않게 한다
        var old = UnityEngine.Object.FindAnyObjectByType<SettingsPanel>(FindObjectsInactive.Include);
        if (old != null)
        {
            if (!EditorUtility.DisplayDialog("설정 창이 이미 있음",
                    "씬에 있는 '" + old.name + "'를 지우고 새로 만들까요? 직접 고친 내용은 사라집니다.", "새로 만들기", "취소"))
                return;
            Undo.DestroyObjectImmediate(old.gameObject);
        }

        MakeSprites();

        Canvas canvas = FindOrCreateCanvas();
        EnsureEventSystem();

        // 루트: 화면 가운데 1920x1080
        var rootGo = New("SettingsPanel", canvas.transform);
        var rootRt = (RectTransform)rootGo.transform;
        rootRt.anchorMin = rootRt.anchorMax = new Vector2(0.5f, 0.5f);
        rootRt.pivot = new Vector2(0.5f, 0.5f);
        rootRt.anchoredPosition = Vector2.zero;
        rootRt.sizeDelta = new Vector2(1920f, 1080f);

        // 뒤를 가리고 클릭을 막는 검은 판 (울트라와이드까지 덮도록 크게)
        var backdrop = New("Backdrop", rootGo.transform);
        var backRt = (RectTransform)backdrop.transform;
        backRt.anchorMin = backRt.anchorMax = new Vector2(0.5f, 0.5f);
        backRt.sizeDelta = new Vector2(6000f, 4000f);
        backdrop.AddComponent<Image>().color = Color.black;

        // 바깥 네온 테두리 (글로우 여백 24단위 = 시안 32px 만큼 넓게)
        MakeImage("Frame", rootGo.transform, 15 - 32, 30 - 32, 2530 + 64, 1380 + 64, sFrame, Color.white, true, false);

        MakeText("Title", rootGo.transform, "SETTINGS", 780, 90, 1000, 106, 84, TextMain,
            TextAlignmentOptions.Center, displayFont, FontStyles.Bold);

        var panel = rootGo.AddComponent<SettingsPanel>();
        panel.waitingKeyText = "Press a key...";

        RectTransform content = BuildScrollView(rootGo.transform, out ScrollRect scroll);
        BuildVoiceSection(content, panel);
        BuildVoiceVolumeSection(content, panel);
        BuildGameAudioSection(content, panel);
        BuildCloseButton(rootGo);

        // 저장 전에 레이아웃을 계산해 둬야 프리팹에 맞는 크기가 들어간다
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        scroll.verticalNormalizedPosition = 1f;

        // 프리팹으로 저장해 두면 다른 씬에서도 쓸 수 있다
        PrefabUtility.SaveAsPrefabAssetAndConnect(rootGo, RootDir + "/SettingsPanel.prefab", InteractionMode.UserAction);

        Undo.RegisterCreatedObjectUndo(rootGo, "Build Settings Panel");
        EditorSceneManager.MarkSceneDirty(rootGo.scene);
        Selection.activeGameObject = rootGo;
        Debug.Log("설정 창 생성 완료. 프리팹: " + RootDir + "/SettingsPanel.prefab");
    }


    // ────────────────────────── 스크롤 영역 ──────────────────────────

    /// <summary>스크롤 뷰를 만들고, 내용을 넣을 Content를 돌려준다</summary>
    static RectTransform BuildScrollView(Transform root, out ScrollRect scroll)
    {
        var go = New("ScrollView", root);
        Place((RectTransform)go.transform, 55, 225, 2460, 1135);
        // 칸 사이 빈 곳에서도 휠과 드래그가 먹도록 투명한 판을 깐다
        go.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

        var viewport = New("Viewport", go.transform);
        var vRt = (RectTransform)viewport.transform;
        Stretch(vRt);
        vRt.offsetMax = new Vector2(-(ScrollbarW + 8f), 0f);
        viewport.AddComponent<RectMask2D>();

        var content = New("Content", vRt);
        var cRt = (RectTransform)content.transform;
        cRt.anchorMin = new Vector2(0f, 1f);
        cRt.anchorMax = new Vector2(1f, 1f);
        cRt.pivot = new Vector2(0.5f, 1f);
        cRt.anchoredPosition = Vector2.zero;
        cRt.sizeDelta = Vector2.zero;
        var layout = Vertical(content, 14f, new RectOffset(0, 0, 6, 6));
        layout.childForceExpandWidth = true;
        var fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll = go.AddComponent<ScrollRect>();
        scroll.viewport = vRt;
        scroll.content = cRt;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;
        scroll.verticalScrollbar = MakeScrollbar(go.transform);
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        return cRt;
    }

    static Scrollbar MakeScrollbar(Transform parent)
    {
        var go = New("Scrollbar", parent);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(ScrollbarW, 0f);
        var track = go.AddComponent<Image>();
        track.sprite = sPill;
        track.type = Image.Type.Sliced;
        track.color = new Color(0.14f, 0.20f, 0.25f);

        var area = New("Sliding Area", rt);
        Stretch((RectTransform)area.transform);
        var handle = New("Handle", area.transform);
        var handleRt = (RectTransform)handle.transform;
        Stretch(handleRt);
        var handleImg = handle.AddComponent<Image>();
        handleImg.sprite = sPill;
        handleImg.type = Image.Type.Sliced;

        var bar = go.AddComponent<Scrollbar>();
        bar.handleRect = handleRt;
        bar.targetGraphic = handleImg;
        bar.direction = Scrollbar.Direction.BottomToTop;
        bar.navigation = new Navigation { mode = Navigation.Mode.None };
        bar.colors = Tint(Cyan, new Color(0.6f, 0.93f, 1f));
        return bar;
    }


    // ────────────────────────── 섹션 ──────────────────────────
    // 새 줄을 넣으려면 해당 Build...Section에 Row와 위젯을 한 번 더 호출하면 된다.

    static void BuildVoiceSection(Transform content, SettingsPanel panel)
    {
        var sec = Section(content, "Voice", "VOICE");

        // CanvasGroup: 음성 서비스 준비 전에는 SettingsPanel이 이 묶음을 흐리게 한다
        panel.voiceGroup = sec.gameObject.AddComponent<CanvasGroup>();

        var body = New("Body", sec);
        var h = body.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(58, 40, 14, 14);
        h.spacing = 30f;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = true;

        var left = Column(body.transform, "LeftColumn");

        var divider = New("Divider", body.transform);
        var dividerImg = divider.AddComponent<Image>();
        dividerImg.color = SectionLine;
        dividerImg.raycastTarget = false;
        Element(divider, 2f, -1f, 0f);

        var right = Column(body.transform, "RightColumn");

        var row = Row(left, "MicModeRow", VoiceRowH);
        Label(row, "Mic Mode", LabelW);
        panel.micModeDropdown = MakeDropdown(row, "MicModeDropdown", new List<string> { "Open Mic", "Input Mic" });

        row = Row(left, "PttKeyRow", VoiceRowH);
        Label(row, "Push-to-Talk Key", LabelW);
        var ptt = MakeFieldButton(row, "PttKeyButton");
        Element(ptt.gameObject, -1f, VoiceRowH, 1f);
        panel.pttKeyLabel = MakeInsetText("Label", (RectTransform)ptt.transform, "V", 30, 30, 38, TextMain);
        panel.pttKeyButton = ptt;

        row = Row(left, "SelfMuteRow", VoiceRowH);
        Label(row, "Self Mute", LabelW);
        panel.selfMuteToggle = MakeSwitch(row);

        row = Row(right, "InputDeviceRow", VoiceRowH);
        Label(row, "Input Device", RightLabelW);
        panel.deviceDropdown = MakeDropdown(row, "InputDeviceDropdown", new List<string> { "Default Microphone" });

        row = Row(right, "OutputDeviceRow", VoiceRowH);
        Label(row, "Output Device", RightLabelW);
        panel.outputDeviceDropdown = MakeDropdown(row, "OutputDeviceDropdown", new List<string> { "Default Speakers" });

        row = Row(right, "DeviceActionsRow", VoiceRowH);
        var refresh = MakeFieldButton(row, "RefreshDevicesButton");
        Element(refresh.gameObject, 330f, VoiceRowH, 0f);
        var refreshRt = (RectTransform)refresh.transform;
        var icon = New("Icon", refreshRt);
        var iconRt = (RectTransform)icon.transform;
        iconRt.anchorMin = iconRt.anchorMax = new Vector2(0f, 0.5f);
        iconRt.anchoredPosition = new Vector2(36f, 0f);
        iconRt.sizeDelta = new Vector2(32f, 32f);
        var iconImg = icon.AddComponent<Image>();
        iconImg.sprite = sRefresh;
        iconImg.color = TextMuted;
        iconImg.raycastTarget = false;
        MakeInsetText("Label", refreshRt, "Refresh Devices", 66, 10, 30, TextMuted);
        panel.refreshDevicesButton = refresh;

        Element(New("Space", row), -1f, -1f, 1f);

        // 연결 상태. 섹션이 흐려져도 이 줄은 또렷하게 둔다
        var statusGo = New("VoiceStatus", row);
        var statusLayout = statusGo.AddComponent<HorizontalLayoutGroup>();
        statusLayout.spacing = 10f;
        statusLayout.childAlignment = TextAnchor.MiddleLeft;
        statusLayout.childControlWidth = statusLayout.childControlHeight = true;
        statusLayout.childForceExpandWidth = statusLayout.childForceExpandHeight = false;
        Element(statusGo, 406f, VoiceRowH, 0f);
        var statusGroup = statusGo.AddComponent<CanvasGroup>();
        statusGroup.ignoreParentGroups = true;
        statusGroup.blocksRaycasts = false;

        var dotGo = New("Dot", statusGo.transform);
        var dot = dotGo.AddComponent<Image>();
        dot.sprite = sDisk;
        dot.color = new Color(0.3f, 0.95f, 0.5f);
        dot.raycastTarget = false;
        Element(dotGo, 24f, 24f, 0f);

        var status = statusGo.AddComponent<SettingsVoiceStatus>();
        status.dot = dot;
        status.label = RowText(statusGo.transform, "Text", "Voice service connected", 372f, 30, TextMuted, TextAlignmentOptions.Left);
    }

    static void BuildVoiceVolumeSection(Transform content, SettingsPanel panel)
    {
        var body = SectionBody(Section(content, "VoiceVolume", "VOICE VOLUME"));

        var row = Row(body, "MicInputRow", SliderRowH);
        Label(row, "Mic Input", LabelW);
        panel.inputVolumeSlider = MakeSlider(row, "MicInputSlider", -50f, 50f, true, 25f);
        panel.inputVolumeLabel = Percent(row, "MicInputValue", "75%");

        row = Row(body, "OtherPlayersRow", SliderRowH);
        Label(row, "Other Players", LabelW);
        panel.opponentVolumeSlider = MakeSlider(row, "OtherPlayersSlider", -50f, 50f, true, 20f);
        panel.opponentVolumeLabel = Percent(row, "OtherPlayersValue", "70%");

        row = Row(body, "InputLevelRow", SliderRowH);
        Label(row, "INPUT LEVEL", LabelW);

        // 세그먼트 미터: 어두운 칸 위에 밝은 칸을 Filled로 겹친다
        var backGo = New("LevelBack", row);
        var back = backGo.AddComponent<Image>();
        back.sprite = sMeter;
        back.color = new Color(0.14f, 0.20f, 0.25f);
        back.raycastTarget = false;
        Element(backGo, 943f, 28.5f, 0f);

        var litGo = New("LevelFill", backGo.transform);
        Stretch((RectTransform)litGo.transform);
        var lit = litGo.AddComponent<Image>();
        lit.sprite = sMeter;
        lit.color = Cyan;
        lit.raycastTarget = false;
        lit.type = Image.Type.Filled;
        lit.fillMethod = Image.FillMethod.Horizontal;
        lit.fillOrigin = (int)Image.OriginHorizontal.Left;
        lit.fillAmount = 0.38f;
        panel.levelBar = lit;
    }

    static void BuildGameAudioSection(Transform content, SettingsPanel panel)
    {
        var body = SectionBody(Section(content, "GameAudio", "GAME AUDIO"));

        panel.masterSlider = AudioRow(body, "Master", 0.8f);
        panel.bgmSlider = AudioRow(body, "BGM", 0.6f);
        panel.sfxSlider = AudioRow(body, "SFX", 0.7f);
    }

    static Slider AudioRow(Transform body, string title, float value)
    {
        var row = Row(body, title + "Row", SliderRowH);
        Label(row, title, LabelW);
        var slider = MakeSlider(row, title + "Slider", 0f, 1f, false, value);
        var text = Percent(row, title + "Value", Mathf.RoundToInt(value * 100f) + "%");

        // SettingsPanel에 게임 사운드 % 칸이 없어서 따로 붙인다
        var pct = slider.gameObject.AddComponent<SettingsSliderPercent>();
        pct.slider = slider;
        pct.label = text;
        return slider;
    }

    static void BuildCloseButton(GameObject root)
    {
        var go = New("CloseButton", root.transform);
        var rt = (RectTransform)go.transform;
        PlaceCenter(rt, 2455, 110, 86, 86);
        rt.localRotation = Quaternion.Euler(0f, 0f, 45f);

        var img = go.AddComponent<Image>();
        img.sprite = sFieldSolid;
        img.type = Image.Type.Sliced;
        img.color = new Color(0.85f, 0.05f, 0.05f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.colors = Tint(new Color(0.8f, 0.8f, 0.8f), Color.white);

        CloseBar(rt, 40f, 5f);
        CloseBar(rt, 5f, 40f);

        UnityEventTools.AddBoolPersistentListener(btn.onClick, root.SetActive, false);
    }

    static void CloseBar(RectTransform parent, float w, float h)
    {
        var bar = New("Bar", parent);
        var rt = (RectTransform)bar.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(w, h);
        var img = bar.AddComponent<Image>();
        img.color = new Color(0.32f, 0f, 0f);
        img.raycastTarget = false;
    }

    /// <summary>테두리 + 제목 + 구분선까지 만든 섹션을 돌려준다. 높이는 내용에 맞춰 자동으로 정해진다</summary>
    static Transform Section(Transform content, string name, string title)
    {
        var go = New(name, content);
        var border = go.AddComponent<Image>();
        border.sprite = sSection;
        border.type = Image.Type.Sliced;
        border.raycastTarget = false;
        Vertical(go, 0f, new RectOffset(0, 0, 0, 0)).childForceExpandWidth = true;

        var bar = New("TitleBar", go.transform);
        var h = bar.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(58, 40, 0, 0);
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;
        Element(bar, -1f, 60f, 0f);

        var text = RowText(bar.transform, "Title", title, -1f, 50, TextMain, TextAlignmentOptions.Left, displayFont, FontStyles.Bold);
        Element(text.gameObject, -1f, 60f, 1f);

        var line = New("TitleLine", go.transform);
        var lineImg = line.AddComponent<Image>();
        lineImg.color = SectionLine;
        lineImg.raycastTarget = false;
        Element(line, -1f, 2f, 0f);
        return go.transform;
    }

    /// <summary>줄을 위에서 아래로 쌓는 섹션 본문</summary>
    static Transform SectionBody(Transform section)
    {
        var body = New("Body", section);
        Vertical(body, 12f, new RectOffset(58, 40, 14, 14)).childForceExpandWidth = true;
        return body.transform;
    }

    static Transform Column(Transform parent, string name)
    {
        var go = New(name, parent);
        Vertical(go, 14f, new RectOffset(0, 0, 0, 0)).childForceExpandWidth = true;
        // 기본 너비를 똑같이 두고 남는 폭을 나눠야 두 칸의 너비가 같아진다
        var le = go.AddComponent<LayoutElement>();
        le.minWidth = 0f;
        le.preferredWidth = 1f;
        le.flexibleWidth = 1f;
        return go.transform;
    }

    /// <summary>라벨과 위젯을 가로로 놓는 한 줄</summary>
    static Transform Row(Transform parent, string name, float height)
    {
        var go = New(name, parent);
        var h = go.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 16f;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;
        Element(go, -1f, height, 0f);
        return go.transform;
    }

    static VerticalLayoutGroup Vertical(GameObject go, float spacing, RectOffset padding)
    {
        var v = go.AddComponent<VerticalLayoutGroup>();
        v.spacing = spacing;
        v.padding = padding;
        v.childAlignment = TextAnchor.UpperLeft;
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = v.childForceExpandHeight = false;
        return v;
    }

    /// <summary>레이아웃 크기를 지정한다. 0보다 작은 값은 지정하지 않는다</summary>
    static void Element(GameObject go, float width, float height, float flexibleWidth)
    {
        var le = go.AddComponent<LayoutElement>();
        if (width >= 0f) { le.minWidth = width; le.preferredWidth = width; }
        if (height >= 0f) { le.minHeight = height; le.preferredHeight = height; }
        if (flexibleWidth > 0f) { le.minWidth = Mathf.Max(le.minWidth, 0f); le.flexibleWidth = flexibleWidth; }
    }


    // ────────────────────────── 위젯 ──────────────────────────

    static TextMeshProUGUI RowText(Transform parent, string name, string text, float width, float size, Color color,
                                   TextAlignmentOptions align) =>
        RowText(parent, name, text, width, size, color, align, bodyFont, FontStyles.Normal);

    static TextMeshProUGUI RowText(Transform parent, string name, string text, float width, float size, Color color,
                                   TextAlignmentOptions align, TMP_FontAsset font, FontStyles style)
    {
        var go = New(name, parent);
        var t = go.AddComponent<TextMeshProUGUI>();
        Style(t, text, size * S, color, align, font, style);
        if (width >= 0f) Element(go, width, 44f, 0f);
        return t;
    }

    static TextMeshProUGUI Label(Transform row, string text, float width) =>
        RowText(row, text + " Label", text, width, 38, TextMain, TextAlignmentOptions.Left);

    static TextMeshProUGUI Percent(Transform row, string name, string text) =>
        RowText(row, name, text, 92f, 38, TextMain, TextAlignmentOptions.Right);

    static Slider MakeSlider(Transform parent, string name, float min, float max, bool whole, float value)
    {
        var go = New(name, parent);
        Element(go, -1f, SliderRowH, 1f);
        // 트랙이 얇아서 클릭 영역을 넓혀 둔다
        go.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

        var slider = go.AddComponent<Slider>();

        var bg = New("Background", go.transform);
        var bgRt = (RectTransform)bg.transform;
        CenterBand(bgRt, 16.5f);
        var bgImg = bg.AddComponent<Image>();
        bgImg.sprite = sPill;
        bgImg.type = Image.Type.Sliced;
        bgImg.color = TrackGray;
        bgImg.raycastTarget = false;

        var fillArea = New("Fill Area", go.transform);
        CenterBand((RectTransform)fillArea.transform, 16.5f);
        var fill = New("Fill", fillArea.transform);
        var fillRt = (RectTransform)fill.transform;
        Stretch(fillRt);
        var fillImg = fill.AddComponent<Image>();
        fillImg.sprite = sPill;
        fillImg.type = Image.Type.Sliced;
        fillImg.color = Cyan;
        fillImg.raycastTarget = false;

        var slide = New("Handle Slide Area", go.transform);
        // Slider는 손잡이의 세로 앵커를 0~1로 덮어쓴다. 손잡이 높이 = 이 영역 높이 + sizeDelta.y 이므로
        // 영역 높이를 0으로 둬야 sizeDelta 그대로 36이 된다.
        // 좌우는 손잡이 반지름만큼 줄여 끝까지 가도 트랙 밖(퍼센트 글자 쪽)으로 나가지 않게 한다.
        var slideRt = (RectTransform)slide.transform;
        CenterBand(slideRt, 0f);
        slideRt.offsetMin = new Vector2(18f, slideRt.offsetMin.y);
        slideRt.offsetMax = new Vector2(-18f, slideRt.offsetMax.y);
        var handle = New("Handle", slide.transform);
        var handleRt = (RectTransform)handle.transform;
        handleRt.anchorMin = handleRt.anchorMax = new Vector2(0f, 0.5f);
        handleRt.anchoredPosition = Vector2.zero;
        handleRt.sizeDelta = new Vector2(36f, 36f);
        var handleImg = handle.AddComponent<Image>();
        handleImg.sprite = sKnob;

        slider.fillRect = fillRt;
        slider.handleRect = handleRt;
        slider.targetGraphic = handleImg;
        slider.direction = Slider.Direction.LeftToRight;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };
        slider.colors = Tint(Color.white, new Color(0.85f, 0.97f, 1f));
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = whole;
        slider.SetValueWithoutNotify(value);
        return slider;
    }

    static Toggle MakeSwitch(Transform parent)
    {
        const float width = 106.5f, height = 52.5f;

        var go = New("SelfMuteToggle", parent);
        var rt = (RectTransform)go.transform;
        Element(go, width, height, 0f);

        var track = go.AddComponent<Image>();
        track.sprite = sPillBig;
        track.type = Image.Type.Sliced;

        var knobGo = New("Knob", rt);
        var knobRt = (RectTransform)knobGo.transform;
        knobRt.anchorMin = knobRt.anchorMax = new Vector2(0f, 0.5f);
        knobRt.sizeDelta = new Vector2(36f, 36f);
        var knobImg = knobGo.AddComponent<Image>();
        knobImg.sprite = sDisk;
        knobImg.raycastTarget = false;

        var toggle = go.AddComponent<Toggle>();
        toggle.targetGraphic = track;
        toggle.transition = Selectable.Transition.None;
        toggle.navigation = new Navigation { mode = Navigation.Mode.None };
        toggle.SetIsOnWithoutNotify(false);

        var sw = go.AddComponent<SettingsToggleSwitch>();
        sw.toggle = toggle;
        sw.track = track;
        sw.knob = knobRt;
        sw.offX = height / 2f;
        sw.onX = width - height / 2f;
        sw.onColor = Cyan;
        sw.offColor = new Color(0.20f, 0.23f, 0.27f);

        // 에디터에서도 꺼진 상태로 보이게
        track.color = sw.offColor;
        knobRt.anchoredPosition = new Vector2(sw.offX, 0f);
        return toggle;
    }

    static Button MakeFieldButton(Transform parent, string name)
    {
        var go = New(name, parent);
        var img = go.AddComponent<Image>();
        img.sprite = sField;
        img.type = Image.Type.Sliced;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.colors = Tint(FieldLine, Cyan);
        btn.navigation = new Navigation { mode = Navigation.Mode.None };
        return btn;
    }

    static TMP_Dropdown MakeDropdown(Transform parent, string name, List<string> options)
    {
        const float itemH = 44f;

        var go = New(name, parent);
        var rt = (RectTransform)go.transform;
        Element(go, -1f, VoiceRowH, 1f);
        var img = go.AddComponent<Image>();
        img.sprite = sField;
        img.type = Image.Type.Sliced;

        var caption = MakeInsetText("Label", rt, options[0], 32, 56, 36, TextMain);

        var arrow = New("Arrow", rt);
        var arrowRt = (RectTransform)arrow.transform;
        arrowRt.anchorMin = arrowRt.anchorMax = new Vector2(1f, 0.5f);
        arrowRt.anchoredPosition = new Vector2(-34f, 0f);
        arrowRt.sizeDelta = new Vector2(18f, 13f);
        var arrowImg = arrow.AddComponent<Image>();
        arrowImg.sprite = sArrow;
        arrowImg.raycastTarget = false;

        // ── 펼침 목록 템플릿 ──
        var template = New("Template", rt);
        var tRt = (RectTransform)template.transform;
        tRt.anchorMin = new Vector2(0f, 0f);
        tRt.anchorMax = new Vector2(1f, 0f);
        tRt.pivot = new Vector2(0.5f, 1f);
        tRt.anchoredPosition = new Vector2(0f, -3f);
        tRt.sizeDelta = new Vector2(0f, itemH * 5f + 8f);
        var tImg = template.AddComponent<Image>();
        tImg.sprite = sField;
        tImg.type = Image.Type.Sliced;
        tImg.color = Cyan;

        var viewport = New("Viewport", tRt);
        var vRt = (RectTransform)viewport.transform;
        Stretch(vRt);
        vRt.offsetMin = new Vector2(3f, 3f);
        vRt.offsetMax = new Vector2(-3f, -3f);
        var vImg = viewport.AddComponent<Image>();
        var mask = viewport.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        vImg.color = Color.white;

        var content = New("Content", vRt);
        var cRt = (RectTransform)content.transform;
        cRt.anchorMin = new Vector2(0f, 1f);
        cRt.anchorMax = new Vector2(1f, 1f);
        cRt.pivot = new Vector2(0.5f, 1f);
        cRt.anchoredPosition = Vector2.zero;
        cRt.sizeDelta = new Vector2(0f, itemH);

        var item = New("Item", cRt);
        var iRt = (RectTransform)item.transform;
        iRt.anchorMin = new Vector2(0f, 0.5f);
        iRt.anchorMax = new Vector2(1f, 0.5f);
        iRt.anchoredPosition = Vector2.zero;
        iRt.sizeDelta = new Vector2(0f, itemH);

        var itemBgGo = New("Item Background", iRt);
        Stretch((RectTransform)itemBgGo.transform);
        var itemBg = itemBgGo.AddComponent<Image>();
        itemBg.color = Color.white;

        var checkGo = New("Item Checkmark", iRt);
        var checkRt = (RectTransform)checkGo.transform;
        checkRt.anchorMin = new Vector2(0f, 0f);
        checkRt.anchorMax = new Vector2(0f, 1f);
        checkRt.pivot = new Vector2(0f, 0.5f);
        checkRt.anchoredPosition = Vector2.zero;
        checkRt.sizeDelta = new Vector2(5f, 0f);
        var check = checkGo.AddComponent<Image>();
        check.color = Cyan;
        check.raycastTarget = false;

        var itemLabel = MakeInsetText("Item Label", iRt, "Option", 28, 12, 34, TextMain);

        var itemToggle = item.AddComponent<Toggle>();
        itemToggle.targetGraphic = itemBg;
        itemToggle.graphic = check;
        itemToggle.isOn = true;
        itemToggle.colors = Tint(new Color(0.03f, 0.05f, 0.07f), new Color(0.04f, 0.28f, 0.40f));

        var scroll = template.AddComponent<ScrollRect>();
        scroll.content = cRt;
        scroll.viewport = vRt;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 20f;

        template.SetActive(false);

        var dd = go.AddComponent<TMP_Dropdown>();
        dd.targetGraphic = img;
        dd.template = tRt;
        dd.captionText = caption;
        dd.itemText = itemLabel;
        dd.colors = Tint(FieldLine, Cyan);
        dd.navigation = new Navigation { mode = Navigation.Mode.None };
        dd.ClearOptions();
        dd.AddOptions(options);
        dd.SetValueWithoutNotify(0);
        dd.RefreshShownValue();
        return dd;
    }


    // ────────────────────────── 공통 도우미 ──────────────────────────

    static GameObject New(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = LayerMask.NameToLayer("UI");
        return go;
    }

    /// <summary>시안 픽셀 좌표(왼쪽 위 기준)로 배치한다. 부모는 1920x1080 크기여야 한다</summary>
    static void Place(RectTransform rt, float x, float y, float w, float h) =>
        PlaceCenter(rt, x + w / 2f, y + h / 2f, w, h);

    static void PlaceCenter(RectTransform rt, float cx, float cy, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(cx * S, -cy * S);
        rt.sizeDelta = new Vector2(w * S, h * S);
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    /// <summary>부모 가로 전체, 세로 가운데의 띠</summary>
    static void CenterBand(RectTransform rt, float height)
    {
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, height);
    }

    static Image MakeImage(string name, Transform parent, float x, float y, float w, float h,
                           Sprite sprite, Color color, bool sliced, bool raycast)
    {
        var go = New(name, parent);
        Place((RectTransform)go.transform, x, y, w, h);
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = raycast;
        if (sliced) img.type = Image.Type.Sliced;
        return img;
    }

    static TextMeshProUGUI MakeText(string name, Transform parent, string text, float x, float y, float w, float h,
                                    float size, Color color, TextAlignmentOptions align, TMP_FontAsset font, FontStyles style)
    {
        var go = New(name, parent);
        Place((RectTransform)go.transform, x, y, w, h);
        var t = go.AddComponent<TextMeshProUGUI>();
        Style(t, text, size * S, color, align, font, style);
        return t;
    }

    /// <summary>부모를 꽉 채우되 좌우를 띄운 왼쪽 정렬 글자 (UI 단위 그대로 받는 인셋)</summary>
    static TextMeshProUGUI MakeInsetText(string name, RectTransform parent, string text,
                                         float left, float right, float size, Color color)
    {
        var go = New(name, parent);
        var rt = (RectTransform)go.transform;
        Stretch(rt);
        rt.offsetMin = new Vector2(left, 0f);
        rt.offsetMax = new Vector2(-right, 0f);
        var t = go.AddComponent<TextMeshProUGUI>();
        Style(t, text, size * S, color, TextAlignmentOptions.Left, bodyFont, FontStyles.Normal);
        return t;
    }

    static void Style(TextMeshProUGUI t, string text, float size, Color color,
                      TextAlignmentOptions align, TMP_FontAsset font, FontStyles style)
    {
        t.text = text;
        t.font = font;
        t.fontSize = size;
        // 칸보다 길면 먼저 글자를 줄이고, 그래도 넘치면 말줄임표를 쓴다
        t.enableAutoSizing = true;
        t.fontSizeMax = size;
        t.fontSizeMin = size * 0.6f;
        t.color = color;
        t.alignment = align;   // Left/Center/Right에 이미 세로 가운데가 포함돼 있다
        t.fontStyle = style;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Ellipsis;   // 긴 장치 이름 대비
        t.raycastTarget = false;
    }

    static ColorBlock Tint(Color normal, Color active)
    {
        var c = ColorBlock.defaultColorBlock;
        c.normalColor = normal;
        c.highlightedColor = active;
        c.pressedColor = active;
        c.selectedColor = active;
        c.disabledColor = new Color(normal.r, normal.g, normal.b, 0.4f);
        c.colorMultiplier = 1f;
        c.fadeDuration = 0.08f;
        return c;
    }

    static TMP_FontAsset FindFont(string keyword)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:TMP_FontAsset " + keyword))
        {
            var f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
            if (f != null) return f;
        }
        return TMP_Settings.instance != null ? TMP_Settings.defaultFontAsset : null;
    }

    static Canvas FindOrCreateCanvas()
    {
        if (Selection.activeGameObject != null)
        {
            var selected = Selection.activeGameObject.GetComponentInParent<Canvas>();
            if (selected != null) return selected;
        }

        var go = new GameObject("SettingsCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.layer = LayerMask.NameToLayer("UI");
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        Undo.RegisterCreatedObjectUndo(go, "Create Settings Canvas");
        return canvas;
    }

    static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
    }


    // ────────────────────────── 스프라이트 생성 ──────────────────────────

    static void MakeSprites()
    {
        if (!AssetDatabase.IsValidFolder(RootDir)) AssetDatabase.CreateFolder("Assets", "SettingsUI");
        if (!AssetDatabase.IsValidFolder(SpriteDir)) AssetDatabase.CreateFolder(RootDir, "Generated");

        var clear = new Color(0f, 0f, 0f, 0f);

        sFrame = Box("frame", 0, 60, 0, 24, 5, 24, clear, FrameLine, Cyan);
        sSection = Box("section", 0, 26, 0, 1, 2, 0, clear, SectionLine, clear);
        sField = Box("field", 0, 0, 5, 0, 2, 0, FieldFill, Color.white, clear);
        sFieldSolid = Box("field_solid", 0, 0, 6, 0, 0, 0, Color.white, clear, clear);
        sPill = Box("pill", 0, 0, 8, 0, 0, 0, Color.white, clear, clear);
        sPillBig = Box("pill_big", 0, 0, 26, 0, 0, 0, Color.white, clear, clear);
        sDisk = Box("disk", 36, 0, 18, 0, 0, 0, Color.white, clear, clear);
        sKnob = Box("knob", 36, 0, 18, 0, 3, 0, Color.white, Cyan, clear);
        sArrow = MaskSprite("arrow", 36, 26, InArrow);
        sRefresh = MaskSprite("refresh", 64, 64, InRefresh);
        sMeter = Meter();

        AssetDatabase.Refresh();
    }

    /// <summary>
    /// 사각형 SDF로 그린다. chamfer는 깎은 모서리, radius는 둥근 모서리, 단위는 UI 단위.
    /// size가 0이면 9-slice용으로 최소 크기를 쓰고, 0보다 크면 슬라이스 없는 고정 크기 스프라이트다.
    /// </summary>
    static Sprite Box(string name, float size, float chamfer, float radius, float margin, float line, float glow,
                      Color fill, Color lineColor, Color glowColor)
    {
        bool sliced = size <= 0f;
        float border = margin + Mathf.Max(chamfer, radius);
        float sizeU = sliced ? 2f * border + 4f : size;
        int px = Mathf.RoundToInt(sizeU * SS);
        float half = sizeU / 2f - margin;

        var pixels = new Color[px * px];
        for (int y = 0; y < px; y++)
        {
            for (int x = 0; x < px; x++)
            {
                float qx = Mathf.Abs((x + 0.5f) / SS - sizeU / 2f) - half;
                float qy = Mathf.Abs((y + 0.5f) / SS - sizeU / 2f) - half;

                float d;
                if (chamfer > 0f)
                {
                    d = Mathf.Max(Mathf.Max(qx, qy), (qx + qy + chamfer) / 1.41421f);
                }
                else
                {
                    float rx = qx + radius, ry = qy + radius;
                    d = new Vector2(Mathf.Max(rx, 0f), Mathf.Max(ry, 0f)).magnitude
                        + Mathf.Min(Mathf.Max(rx, ry), 0f) - radius;
                }

                // 글자/선 가장자리가 어둡게 번지지 않도록 투명 픽셀에도 색을 넣어 둔다
                Color baseColor = fill.a > 0f ? fill : (line > 0f ? lineColor : Color.white);
                Color c = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);

                if (fill.a > 0f)
                    c = Over(WithAlpha(fill, fill.a * Mathf.Clamp01(0.5f - d * SS)), c);

                if (glow > 0f)
                {
                    float dist = Mathf.Abs(d + line / 2f);
                    float a = Mathf.Exp(-dist / (glow * 0.33f)) * Mathf.Clamp01(1f - Mathf.Abs(d) / margin) * 0.8f;
                    c = Over(WithAlpha(glowColor, a), c);
                }

                if (line > 0f)
                {
                    float a = Mathf.Clamp01(0.5f - (Mathf.Abs(d + line / 2f) - line / 2f) * SS);
                    c = Over(WithAlpha(lineColor, lineColor.a * a), c);
                }

                pixels[y * px + x] = c;
            }
        }

        float bpx = sliced ? border * SS : 0f;
        return Save(name, px, px, pixels, new Vector4(bpx, bpx, bpx, bpx));
    }

    static Sprite MaskSprite(string name, int w, int h, Func<float, float, bool> inside)
    {
        const int ss = 4;
        var pixels = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int hits = 0;
                for (int sy = 0; sy < ss; sy++)
                    for (int sx = 0; sx < ss; sx++)
                    {
                        float u = (x + (sx + 0.5f) / ss) / w * 2f - 1f;
                        float v = (y + (sy + 0.5f) / ss) / h * 2f - 1f;
                        if (inside(u, v)) hits++;
                    }
                pixels[y * w + x] = new Color(1f, 1f, 1f, hits / (float)(ss * ss));
            }
        }
        return Save(name, w, h, pixels, Vector4.zero);
    }

    /// <summary>시안대로 37칸짜리 미터. 칸 간격 34, 칸 너비 23, 높이 38 (시안 px)</summary>
    static Sprite Meter()
    {
        const int count = 37;
        int pitch = Mathf.RoundToInt(34f * S * SS);
        int seg = Mathf.RoundToInt(23f * S * SS);
        int h = Mathf.RoundToInt(38f * S * SS);
        int w = pitch * (count - 1) + seg;

        var pixels = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                pixels[y * w + x] = new Color(1f, 1f, 1f, x % pitch < seg ? 1f : 0f);

        return Save("meter", w, h, pixels, Vector4.zero);
    }

    static bool InArrow(float u, float v)
    {
        // 아래를 가리키는 삼각형: 위쪽(v=1)이 넓고 아래쪽(v=-1)이 뾰족하다
        float t = (v + 1f) / 2f;
        return Mathf.Abs(u) <= t;
    }

    static bool InRefresh(float u, float v)
    {
        float r = Mathf.Sqrt(u * u + v * v);
        float ang = Mathf.Atan2(v, u) * Mathf.Rad2Deg;
        if (ang < 0f) ang += 360f;

        bool inBand = r > 0.56f && r < 0.76f;
        bool inArc = (ang > 25f && ang < 165f) || (ang > 205f && ang < 345f);
        if (inBand && inArc) return true;

        return InHead(u, v, 25f) || InHead(u, v, 205f);
    }

    /// <summary>호 끝에 붙는 화살촉. 시계 방향을 가리킨다</summary>
    static bool InHead(float u, float v, float angleDeg)
    {
        float a = angleDeg * Mathf.Deg2Rad;
        var n = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        var t = new Vector2(n.y, -n.x);
        Vector2 c = n * 0.66f;
        Vector2 p0 = c + n * 0.27f;
        Vector2 p1 = c - n * 0.27f;
        Vector2 p2 = c + t * 0.34f;
        var p = new Vector2(u, v);

        float s0 = Cross(p1 - p0, p - p0);
        float s1 = Cross(p2 - p1, p - p1);
        float s2 = Cross(p0 - p2, p - p2);
        return (s0 >= 0f && s1 >= 0f && s2 >= 0f) || (s0 <= 0f && s1 <= 0f && s2 <= 0f);
    }

    static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

    static Color Over(Color top, Color bottom)
    {
        float a = top.a + bottom.a * (1f - top.a);
        if (a <= 0.0001f) return new Color(bottom.r, bottom.g, bottom.b, 0f);
        float k = bottom.a * (1f - top.a);
        return new Color(
            (top.r * top.a + bottom.r * k) / a,
            (top.g * top.a + bottom.g * k) / a,
            (top.b * top.a + bottom.b * k) / a,
            a);
    }

    static Sprite Save(string name, int w, int h, Color[] pixels, Vector4 border)
    {
        string path = SpriteDir + "/" + name + ".png";

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.SetPixels(pixels);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;

        // 투명 여백이 있어도 9-slice가 되도록 FullRect
        var settings = new TextureImporterSettings();
        imp.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        imp.SetTextureSettings(settings);

        imp.spritePixelsPerUnit = 100f * SS;
        imp.spriteBorder = border;
        imp.mipmapEnabled = false;
        imp.alphaIsTransparency = true;
        imp.filterMode = FilterMode.Bilinear;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}