using System;
using System.Collections.Generic;
using UnityEngine;

namespace RBN.Modlib.UI;

public static class UiConfigRowWidgets
{
    public static bool DrawToggleRow(string label, bool value, GUIStyle labelStyle, float toggleWidth = 18f)
    {
        GUILayout.BeginHorizontal();
        var next = GUILayout.Toggle(value, GUIContent.none, GUI.skin.toggle, GUILayout.Width(toggleWidth));
        GUILayout.Label(label, labelStyle);
        GUILayout.EndHorizontal();
        return next;
    }

    public static float DrawFloatSliderTextRow(
        Dictionary<string, string> textCache,
        string cacheKey,
        string label,
        float value,
        float min,
        float max,
        GUIStyle labelStyle,
        GUIStyle textFieldStyle,
        ref bool clearFocusNextGui,
        float labelWidth,
        float sliderWidth,
        float textWidth,
        float rowHeight = 24f,
        bool centeredSlider = false,
        float centeredSliderDownBias = 2f)
    {
        if (!textCache.TryGetValue(cacheKey, out var text))
        {
            text = value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        }

        GUILayout.BeginHorizontal(GUILayout.Height(rowHeight));
        GUILayout.Label(label, labelStyle, GUILayout.Width(labelWidth), GUILayout.Height(rowHeight));
        var slider = centeredSlider
            ? UiConfigRowUtils.DrawCenteredHorizontalSlider(value, min, max, sliderWidth, rowHeight, centeredSliderDownBias)
            : GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(sliderWidth));
        GUILayout.Space(8f);
        GUI.SetNextControlName(cacheKey);
        text = GUILayout.TextField(text, textFieldStyle, GUILayout.Width(textWidth), GUILayout.Height(rowHeight));
        GUILayout.EndHorizontal();

        var hasFocus = GUI.GetNameOfFocusedControl() == cacheKey;
        if (hasFocus)
        {
            if (float.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            {
                slider = Mathf.Clamp(parsed, min, max);
            }

            if (Event.current.type == EventType.KeyDown &&
                (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
            {
                clearFocusNextGui = true;
            }
        }
        else
        {
            text = slider.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        }

        textCache[cacheKey] = text;
        return slider;
    }

    public static int DrawIntSliderTextRow(
        Dictionary<string, string> textCache,
        string cacheKey,
        string label,
        int value,
        int min,
        int max,
        GUIStyle labelStyle,
        GUIStyle textFieldStyle,
        ref bool clearFocusNextGui,
        float labelWidth,
        float sliderWidth,
        float textWidth,
        float rowHeight = 24f,
        bool centeredSlider = false,
        float centeredSliderDownBias = 2f)
    {
        if (!textCache.TryGetValue(cacheKey, out var text))
        {
            text = value.ToString();
        }

        GUILayout.BeginHorizontal(GUILayout.Height(rowHeight));
        GUILayout.Label(label, labelStyle, GUILayout.Width(labelWidth), GUILayout.Height(rowHeight));
        var sliderRaw = centeredSlider
            ? UiConfigRowUtils.DrawCenteredHorizontalSlider(value, min, max, sliderWidth, rowHeight, centeredSliderDownBias)
            : GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(sliderWidth));
        var slider = Mathf.RoundToInt(sliderRaw);
        GUILayout.Space(8f);
        GUI.SetNextControlName(cacheKey);
        text = GUILayout.TextField(text, textFieldStyle, GUILayout.Width(textWidth), GUILayout.Height(rowHeight));
        GUILayout.EndHorizontal();

        var hasFocus = GUI.GetNameOfFocusedControl() == cacheKey;
        if (hasFocus)
        {
            if (int.TryParse(text, out var parsed))
            {
                slider = Mathf.Clamp(parsed, min, max);
            }

            if (Event.current.type == EventType.KeyDown &&
                (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
            {
                clearFocusNextGui = true;
            }
        }
        else
        {
            text = slider.ToString();
        }

        textCache[cacheKey] = text;
        return slider;
    }

    public static void DrawKeybindCaptureRow(
        string label,
        string captureId,
        KeyCode current,
        KeyCode defaultValue,
        UiKeybindCaptureState captureState,
        GUIStyle labelStyle,
        Action onBeginCapture,
        Action<KeyCode> onSet,
        Action onEndCapture,
        GUIStyle? buttonStyle = null,
        float labelWidth = 120f,
        float keyWidth = 120f,
        float resetWidth = 70f)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, labelStyle, GUILayout.Width(labelWidth));

        var isCapturing = captureState.IsCapturingTarget(captureId);
        var keyText = isCapturing ? "Press key..." : current.ToString();
        if (UiButtonUtils.DrawRichButton(keyText, buttonStyle, GUILayout.Width(keyWidth)))
        {
            captureState.Begin(captureId);
            onBeginCapture();
        }

        if (UiButtonUtils.DrawRichButton("Reset", buttonStyle, GUILayout.Width(resetWidth)))
        {
            onSet(defaultValue);
            captureState.Cancel();
            onEndCapture();
        }

        GUILayout.EndHorizontal();
    }
}
