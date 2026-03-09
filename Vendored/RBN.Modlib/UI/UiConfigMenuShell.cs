using System;
using UnityEngine;

namespace RBN.Modlib.UI;

public static class UiConfigMenuShell
{
    public static Rect GetPaddedContentRect(Rect panelRect, float padding = 10f)
    {
        return new Rect(
            panelRect.x + padding,
            panelRect.y + padding,
            panelRect.width - (padding * 2f),
            panelRect.height - (padding * 2f));
    }

    public static void DrawPaddedContent(
        Rect panelRect,
        Action drawContent,
        float padding = 10f)
    {
        var contentRect = GetPaddedContentRect(panelRect, padding);
        GUILayout.BeginArea(contentRect);
        drawContent();
        GUILayout.EndArea();
    }

    public static Vector2 DrawScrollablePaddedContent(
        Rect panelRect,
        Vector2 scroll,
        Action drawContent,
        float padding = 10f,
        bool alwaysShowHorizontal = false,
        bool alwaysShowVertical = false)
    {
        var contentRect = GetPaddedContentRect(panelRect, padding);
        GUILayout.BeginArea(contentRect);
        scroll = GUILayout.BeginScrollView(scroll, alwaysShowHorizontal, alwaysShowVertical);
        drawContent();
        GUILayout.EndScrollView();
        GUILayout.EndArea();
        return scroll;
    }

    public static Rect DrawSinglePanel(
        Rect parentRect,
        float gap,
        float panelWidth,
        GUIStyle panelStyle,
        Action<Rect> drawPanelContent)
    {
        var panelRect = new Rect(parentRect.xMax + gap, parentRect.y, panelWidth, parentRect.height);
        GUI.Box(panelRect, GUIContent.none, panelStyle);
        drawPanelContent(panelRect);
        return panelRect;
    }

    public static (Rect menuRect, Rect detailRect) DrawSplitPanels(
        Rect parentRect,
        float gap,
        float menuWidth,
        float detailWidth,
        GUIStyle panelStyle,
        Action<Rect> drawMenuContent,
        Action<Rect>? drawDetailContent)
    {
        var menuRect = new Rect(parentRect.xMax + gap, parentRect.y, menuWidth, parentRect.height);
        GUI.Box(menuRect, GUIContent.none, panelStyle);
        drawMenuContent(menuRect);

        var detailRect = new Rect(menuRect.xMax + gap, parentRect.y, detailWidth, parentRect.height);
        if (drawDetailContent != null)
        {
            GUI.Box(detailRect, GUIContent.none, panelStyle);
            drawDetailContent(detailRect);
        }

        return (menuRect, detailRect);
    }
}
