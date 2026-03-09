using System;
using UnityEngine;

namespace RBN.Modlib.UI;

public sealed class UiKeybindCaptureState
{
    public string PendingTargetId { get; private set; } = string.Empty;

    public bool IsCapturing => !string.IsNullOrEmpty(PendingTargetId);

    public void Begin(string targetId)
    {
        PendingTargetId = string.IsNullOrWhiteSpace(targetId) ? string.Empty : targetId.Trim();
    }

    public void Cancel()
    {
        PendingTargetId = string.Empty;
    }

    public bool IsCapturingTarget(string targetId)
    {
        if (!IsCapturing) return false;
        return string.Equals(PendingTargetId, targetId, StringComparison.Ordinal);
    }

    public bool TryCapture(Event? evt, out string targetId, out KeyCode key, bool escapeClears = true)
    {
        targetId = string.Empty;
        key = KeyCode.None;
        if (!IsCapturing || evt == null) return false;
        if (evt.type != EventType.KeyDown || evt.keyCode == KeyCode.None) return false;

        targetId = PendingTargetId;
        key = escapeClears && evt.keyCode == KeyCode.Escape ? KeyCode.None : evt.keyCode;
        PendingTargetId = string.Empty;
        evt.Use();
        return true;
    }
}
