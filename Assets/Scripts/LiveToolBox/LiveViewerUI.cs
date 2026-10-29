using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UI;

public class LiveViewerUI : MonoBehaviour
{
    public static LiveViewerUI Instance;

    public UnityEngine.UI.Slider ProgressBar;

    public RectTransform BottonUITransform;

    public Dropdown FrameRateDropDown;

    public GameObject RecordingUI;

    public Text RecordingText;

    public Text LyricsText;

    public List<UmaLyricsData> CurrentLyrics = new List<UmaLyricsData>();

    float targetHeight = 0;
    float height;
    private void Awake()
    {
        if (Config.Instance == null)
        {
            new Config();
        }
        ApplyFrameRateOptions();
        UmaViewerMain.ApplyFrameRateLimit();

        height = BottonUITransform.rect.height;
        targetHeight = 0;
        Invoke(nameof(HideSlider), 1.5f);
        Instance = this;
        //TrueProgressBar = (UnityEngine.UIElements.Slider)ProgressBar;
    }

    public void OnMouse(bool isEnter)
    {
        if (isEnter)
        {
            targetHeight = 0;
            CancelInvoke();
        }
        else
        {
            Invoke(nameof(HideSlider), 2.5f);
        }
    }

    private void FixedUpdate()
    {
        BottonUITransform.anchoredPosition = Vector2.Lerp(BottonUITransform.anchoredPosition, new Vector2(0, targetHeight), Time.fixedDeltaTime * 5);
    }

    private void HideSlider()
    {
        targetHeight = -height;
    }

    public void SetFrameRate(int fps)
    {
        Config.Instance.TargetFrameRate = fps == 1 ? 30 : 60;
        Config.Instance.UpdateConfig(false);
        UmaViewerMain.ApplyFrameRateLimit();
    }

    private void ApplyFrameRateOptions()
    {
        if (FrameRateDropDown == null) return;

        FrameRateDropDown.ClearOptions();
        FrameRateDropDown.AddOptions(new List<string> { "60", "30" });
        FrameRateDropDown.SetValueWithoutNotify(Config.Instance.GetTargetFrameRate() == 30 ? 1 : 0);
        FrameRateDropDown.RefreshShownValue();
    }

    public void UpdateLyrics(float time)
    {
        var text = UmaUtility.GetCurrentLyrics(time, CurrentLyrics);
        LyricsText.text = text;
    }

    private Text timeText;

    /// <summary>
    /// Shows elapsed / total time and the timeline frame (60 per second, the frame the log prints) in the top left
    /// corner, always visible so screenshots can be matched with the log. A copy of the lyrics text, for its font.
    /// </summary>
    public void UpdateTime(float time, float totalTime)
    {
        if (!timeText)
        {
            if (!LyricsText) return;
            timeText = Instantiate(LyricsText, LyricsText.canvas.transform);
            timeText.name = "LiveTimeText";
            timeText.alignment = TextAnchor.UpperLeft;
            timeText.horizontalOverflow = HorizontalWrapMode.Overflow;
            timeText.verticalOverflow = VerticalWrapMode.Overflow;
            timeText.fontSize = Mathf.Max(14, LyricsText.fontSize / 2);
            timeText.raycastTarget = false;
            var rect = timeText.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(10, -10);
            rect.sizeDelta = new Vector2(400, 40);
        }
        timeText.text = $"{FormatTime(time)} / {FormatTime(totalTime)}   frame {(int)(time * 60)}";
    }

    private static string FormatTime(float seconds)
    {
        seconds = Mathf.Max(0, seconds);
        return $"{(int)(seconds / 60):00}:{seconds % 60:00.00}";
    }
}
