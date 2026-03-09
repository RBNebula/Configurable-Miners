using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using RBN.Modlib.Game;
using RBN.Modlib.Persistence;
using RBN.Modlib.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ConfigurableMiners;
public sealed partial class ConfigurableMiners
{
	private static void DrawSolidRect(Rect rect, Color color)
	{
		UiDrawUtils.DrawSolidRect(rect, color);
	}

	private void DrawRgbWaveBorder(Rect rect)
	{
		UiDrawUtils.DrawRgbWaveBorder(rect, _rgbBorderEnabled, _rgbBorderWidth, _rgbBorderSegment, _rgbBorderSpeed);
	}

	private bool TryGetHotbarTopGuiY(out float hotbarTopGuiY)
	{
		hotbarTopGuiY = 0f;
		var hotbarPanel = ResolveHotbarPanel();
		if (hotbarPanel == null) return false;
		if (!hotbarPanel.activeInHierarchy) return false;

		var rectTransform = hotbarPanel.transform as RectTransform;
		if (rectTransform == null) return false;

		var corners = new Vector3[4];
		rectTransform.GetWorldCorners(corners);
		var topScreenY = Mathf.Max(corners[1].y, corners[2].y);
		hotbarTopGuiY = Screen.height - topScreenY;
		return true;
	}

	private GameObject? ResolveHotbarPanel()
	{
		if (Time.time < _nextHoverPromptHotbarResolveTime && _hoverPromptHotbarPanel != null)
		{
			return _hoverPromptHotbarPanel;
		}

		_nextHoverPromptHotbarResolveTime = Time.time + 2f;
		_hoverPromptHotbarPanel = null;

		var type = FindGameTypeByName("InventoryUIManager");
		if (type == null) return null;
		var instance = GetSingletonInstance("InventoryUIManager");
		if (instance == null) return null;

		var field = type.GetField("HotbarPanel", AnyInstance);
		if (field != null && field.GetValue(instance) is GameObject fieldValue)
		{
			_hoverPromptHotbarPanel = fieldValue;
			return _hoverPromptHotbarPanel;
		}

		var prop = type.GetProperty("HotbarPanel", AnyInstance);
		if (prop != null && prop.CanRead && prop.GetValue(instance, null) is GameObject propValue)
		{
			_hoverPromptHotbarPanel = propValue;
			return _hoverPromptHotbarPanel;
		}

		return null;
	}

}
