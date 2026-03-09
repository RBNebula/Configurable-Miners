using System;
using UnityEngine;

namespace RBN.Modlib.UI;

public enum UiHoverPromptAnchor
{
    Center,
    Top,
    Bottom
}

public static class UiHoverPromptRenderer
{
    public static void Draw(
        string title,
        string subtitle,
        KeyCode key,
        GUIStyle labelStyle,
        UiHoverPromptAnchor anchor,
        Func<float?>? tryGetHotbarTopGuiY = null,
        Action<Rect>? drawBorder = null,
        float width = 240f,
        float height = 78f)
    {
        if (UiControlUtils.ShouldHidePopupUi()) return;

        var y = (Screen.height * 0.5f) + 42f;
        if (anchor == UiHoverPromptAnchor.Top)
        {
            y = 24f;
        }
        else if (anchor == UiHoverPromptAnchor.Bottom)
        {
            y = Screen.height - height - 24f;
            var hotbarTopGuiY = tryGetHotbarTopGuiY?.Invoke();
            if (hotbarTopGuiY.HasValue)
            {
                var aboveHotbarY = hotbarTopGuiY.Value - height - 34f;
                y = Mathf.Min(y, aboveHotbarY);
                y = Mathf.Max(8f, y);
            }
        }

        var rect = new Rect((Screen.width - width) * 0.5f, y, width, height);
        UiDrawUtils.DrawSolidRect(new Rect(rect.x + 2f, rect.y + 3f, rect.width, rect.height), new Color(0f, 0f, 0f, 0.35f));
        UiDrawUtils.DrawSolidRect(rect, new Color(0.04f, 0.06f, 0.08f, 0.84f));
        UiDrawUtils.DrawSolidRect(new Rect(rect.x, rect.y, rect.width, 1f), new Color(1f, 1f, 1f, 0.12f));
        UiDrawUtils.DrawSolidRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), new Color(1f, 1f, 1f, 0.08f));
        UiDrawUtils.DrawSolidRect(new Rect(rect.x, rect.y, 1f, rect.height), new Color(1f, 1f, 1f, 0.10f));
        UiDrawUtils.DrawSolidRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), new Color(1f, 1f, 1f, 0.10f));
        drawBorder?.Invoke(rect);

        var keyRect = new Rect(rect.x + 10f, rect.y + 22f, 28f, 28f);
        UiDrawUtils.DrawSolidRect(keyRect, new Color(0.12f, 0.16f, 0.2f, 1f));
        UiDrawUtils.DrawSolidRect(new Rect(keyRect.x, keyRect.y, keyRect.width, 1f), new Color(1f, 1f, 1f, 0.2f));
        UiDrawUtils.DrawSolidRect(new Rect(keyRect.x, keyRect.yMax - 1f, keyRect.width, 1f), new Color(1f, 1f, 1f, 0.08f));

        var keyText = key.ToString().ToUpperInvariant();
        var keySize = labelStyle.CalcSize(new GUIContent(keyText));
        GUI.Label(new Rect(
            keyRect.x + (keyRect.width - keySize.x) * 0.5f,
            keyRect.y + (keyRect.height - keySize.y) * 0.5f,
            keySize.x,
            keySize.y), keyText, labelStyle);

        var titleX = keyRect.xMax + 10f;
        GUI.Label(new Rect(titleX, rect.y + 18f, rect.width - (titleX - rect.x) - 10f, 20f), title, labelStyle);
        GUI.Label(new Rect(titleX, rect.y + 42f, rect.width - (titleX - rect.x) - 10f, 22f), subtitle, labelStyle);
    }
}
