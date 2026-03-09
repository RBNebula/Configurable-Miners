using UnityEngine;

namespace RBN.Modlib.UI;

public static class UiConfigRowUtils
{
    public static float DrawCenteredHorizontalSlider(float value, float min, float max, float width, float rowHeight, float downBias = 2f)
    {
        var slotRect = GUILayoutUtility.GetRect(width, rowHeight, GUILayout.Width(width), GUILayout.Height(rowHeight));
        const float sliderVisualHeight = 16f;
        var y = slotRect.y + Mathf.Max(0f, ((slotRect.height - sliderVisualHeight) * 0.5f) + downBias);
        var sliderRect = new Rect(slotRect.x, y, slotRect.width, sliderVisualHeight);
        return GUI.HorizontalSlider(sliderRect, value, min, max);
    }
}
