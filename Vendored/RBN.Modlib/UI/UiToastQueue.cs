using System.Collections.Generic;
using UnityEngine;

namespace RBN.Modlib.UI;

public enum ToastType
{
    Info,
    Success,
    Warning,
    Debug
}

public sealed class UiToastQueue
{
    private sealed class ToastEntry
    {
        public string Text = string.Empty;
        public ToastType Type;
        public float ExpiresAt;
        public float Duration;
    }

    private readonly List<ToastEntry> _items = new();

    public int MaxToasts { get; set; } = 6;
    public float DefaultDuration { get; set; } = 3f;

    public void Push(string message, ToastType type = ToastType.Info, float? duration = null, bool allowDebug = true)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        if (type == ToastType.Debug && !allowDebug) return;

        var now = Time.unscaledTime;
        Prune(now);
        var finalDuration = Mathf.Max(0.5f, duration ?? DefaultDuration);
        _items.Add(new ToastEntry
        {
            Text = message.Trim(),
            Type = type,
            Duration = finalDuration,
            ExpiresAt = now + finalDuration
        });

        if (_items.Count <= MaxToasts) return;
        _items.RemoveRange(0, _items.Count - MaxToasts);
    }

    public void Draw(GUIStyle labelStyle)
    {
        if (_items.Count == 0) return;

        var now = Time.unscaledTime;
        Prune(now);
        if (_items.Count == 0) return;
        if (UiControlUtils.ShouldHidePopupUi()) return;

        var width = Mathf.Min(420f, Screen.width - 20f);
        var x = Screen.width - width - 12f;
        var y = 12f;

        for (var i = _items.Count - 1; i >= 0; i--)
        {
            var item = _items[i];
            var content = new GUIContent(item.Text);
            var textHeight = Mathf.Max(18f, labelStyle.CalcHeight(content, width - 26f));
            var height = textHeight + 18f;
            var rect = new Rect(x, y, width, height);
            DrawCard(rect, item, now);
            GUI.Label(new Rect(rect.x + 13f, rect.y + 9f, rect.width - 26f, textHeight), content, labelStyle);
            y += height + 8f;
        }
    }

    private void Prune(float now)
    {
        for (var i = _items.Count - 1; i >= 0; i--)
        {
            if (_items[i].ExpiresAt > now) continue;
            _items.RemoveAt(i);
        }
    }

    private static void DrawCard(Rect rect, ToastEntry item, float now)
    {
        var remaining = Mathf.Clamp01((item.ExpiresAt - now) / Mathf.Max(0.01f, item.Duration));
        var panel = new Color(0.04f, 0.06f, 0.08f, Mathf.Lerp(0.45f, 0.88f, remaining));
        UiDrawUtils.DrawSolidRect(rect, panel);
        UiDrawUtils.DrawSolidRect(new Rect(rect.x, rect.y, rect.width, 1f), new Color(1f, 1f, 1f, 0.12f));
        UiDrawUtils.DrawSolidRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), new Color(1f, 1f, 1f, 0.06f));
        UiDrawUtils.DrawSolidRect(new Rect(rect.x, rect.y, 4f, rect.height), GetAccent(item.Type));
    }

    private static Color GetAccent(ToastType type)
    {
        return type switch
        {
            ToastType.Success => new Color(0.2f, 0.85f, 0.45f, 0.95f),
            ToastType.Warning => new Color(0.95f, 0.75f, 0.25f, 0.95f),
            ToastType.Debug => new Color(0.2f, 0.75f, 0.95f, 0.95f),
            _ => new Color(0.62f, 0.72f, 0.92f, 0.95f)
        };
    }
}
