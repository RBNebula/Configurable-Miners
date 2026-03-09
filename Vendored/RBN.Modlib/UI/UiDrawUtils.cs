using UnityEngine;

namespace RBN.Modlib.UI;

public static class UiDrawUtils
{
    public static void DrawSolidRect(Rect rect, Color color)
    {
        var prev = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false);
        GUI.color = prev;
    }

    public static void DrawRgbWaveBorder(Rect rect, bool enabled, float borderWidth, float borderSegment, float borderSpeed)
    {
        if (!enabled) return;

        var phase = Time.unscaledTime * borderSpeed;
        var w = Mathf.Max(1f, rect.width);
        var h = Mathf.Max(1f, rect.height);
        var perimeter = 2f * (w + h);

        for (float x = 0f; x < w; x += borderSegment)
        {
            var seg = Mathf.Min(borderSegment, w - x);
            var c = ColorAtPerimeter(x, perimeter, phase);
            DrawSolidRect(new Rect(rect.x + x, rect.y - borderWidth, seg, borderWidth), c);
        }

        for (float y = 0f; y < h; y += borderSegment)
        {
            var seg = Mathf.Min(borderSegment, h - y);
            var c = ColorAtPerimeter(w + y, perimeter, phase);
            DrawSolidRect(new Rect(rect.xMax, rect.y + y, borderWidth, seg), c);
        }

        for (float x = 0f; x < w; x += borderSegment)
        {
            var seg = Mathf.Min(borderSegment, w - x);
            var c = ColorAtPerimeter(w + h + x, perimeter, phase);
            DrawSolidRect(new Rect(rect.xMax - x - seg, rect.yMax, seg, borderWidth), c);
        }

        for (float y = 0f; y < h; y += borderSegment)
        {
            var seg = Mathf.Min(borderSegment, h - y);
            var c = ColorAtPerimeter((2f * w) + h + y, perimeter, phase);
            DrawSolidRect(new Rect(rect.x - borderWidth, rect.yMax - y - seg, borderWidth, seg), c);
        }
    }

    private static Color ColorAtPerimeter(float distance, float perimeter, float phase)
    {
        var hue = Mathf.Repeat((distance / Mathf.Max(1f, perimeter)) + phase, 1f);
        return Color.HSVToRGB(hue, 1f, 1f);
    }
}
