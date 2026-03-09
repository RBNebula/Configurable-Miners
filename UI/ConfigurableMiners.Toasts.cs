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
	private const float DefaultToastDuration = 3f;

	private void PushToast(string message, ToastType type = ToastType.Info, float duration = DefaultToastDuration)
	{
		_toasts.Push(message, type, duration, allowDebug: _debugToasts);
	}

	private void PushDebugToast(string message)
	{
		PushToast(message, ToastType.Debug, 2.2f);
	}

	private void DrawToasts()
	{
		_toasts.Draw(_labelStyle ?? GUI.skin.label);
	}

	private void DrawDebugOverlay()
	{
		if (!_debugLogging) return;

		var width = Mathf.Min(900f, Screen.width - 24f);
		var lineHeight = 18f;
		var summaryLineCount = 4;
		var logCount = Mathf.Min(20, _debugOverlayLines.Count);
		var totalLineCount = summaryLineCount + logCount;
		var height = Mathf.Min(Screen.height * 0.45f, (totalLineCount * lineHeight) + 14f);
		var rect = new Rect(12f, Screen.height - height - 12f, width, height);

		UiDrawUtils.DrawSolidRect(rect, new Color(0f, 0f, 0f, 0.78f));
		UiDrawUtils.DrawSolidRect(new Rect(rect.x, rect.y, rect.width, 1f), new Color(1f, 1f, 1f, 0.12f));
		UiDrawUtils.DrawSolidRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), new Color(1f, 1f, 1f, 0.08f));

		var style = _labelStyle ?? GUI.skin.label;
		var y = rect.y + 6f;
		GUI.Label(new Rect(rect.x + 8f, y, rect.width - 16f, lineHeight), $"Cached miners: {_minerCache.Count}", style);
		y += lineHeight;
		GUI.Label(new Rect(rect.x + 8f, y, rect.width - 16f, lineHeight), $"Configs applied this frame: {_debugAppliedConfigsThisFrame}", style);
		y += lineHeight;
		GUI.Label(new Rect(rect.x + 8f, y, rect.width - 16f, lineHeight), $"Save path resolved: {(_debugSavePathResolved ? "Yes" : "No")}", style);
		y += lineHeight;
		GUI.Label(new Rect(rect.x + 8f, y, rect.width - 16f, lineHeight), $"Active save: {_debugActiveSaveName}", style);
		y += lineHeight;

		for (var i = Mathf.Max(0, _debugOverlayLines.Count - logCount); i < _debugOverlayLines.Count; i++)
		{
			GUI.Label(new Rect(rect.x + 8f, y, rect.width - 16f, lineHeight), _debugOverlayLines[i], style);
			y += lineHeight;
		}
	}
}
