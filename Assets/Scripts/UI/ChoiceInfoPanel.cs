using TMPro;
using UnityEngine;

public class ChoiceInfoPanel : MonoBehaviour
{
    public static ChoiceInfoPanel Instance { get; private set; }

    [Header("참조")]
    public CanvasGroup group;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descText;

    [Header("연출")]
    public float fadeSpeed = 12f;

    object owner;
    float targetAlpha;

    private void Awake()
    {
        Instance = this;
        if (group == null) group = GetComponent<CanvasGroup>();
        if (group != null) group.alpha = 0f;
        targetAlpha = 0f;
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void Update()
    {
        if (group == null) return;
        group.alpha = fadeSpeed <= 0f
            ? targetAlpha
            : Mathf.MoveTowards(group.alpha, targetAlpha, fadeSpeed * Time.deltaTime);
    }

    public void Show(object from, string title, string desc)
    {
        owner = from;
        if(titleText != null) titleText.text = title;
        if(descText != null) descText.text = desc;
        targetAlpha = 1f;
    }

    public void Hide(object from)
    {
        if (owner != from) return;
        owner = null;
        targetAlpha = 0f;
    }
}
