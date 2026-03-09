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
	private void DrawOutputPicker()
	{
		GUILayout.Space(6f);
		GUILayout.BeginVertical(GUI.skin.box);
		GUILayout.BeginHorizontal();
		GUILayout.Label($"Choose output for Slot {_outputPickerSlot + 1}", _labelStyle ?? GUI.skin.label);
		GUILayout.FlexibleSpace();
		if (UiButton("Close", GUILayout.Width(80f)))
		{
			_outputPickerSlot = -1;
			ExitGuiAfterLayoutMutation();
		}
		GUILayout.EndHorizontal();

		if (_outputOptions.Count == 0)
		{
			GUILayout.Label("No outputs discovered from miner definitions.", _labelStyle ?? GUI.skin.label);
		}
		else
		{
			_pickerScroll = GUILayout.BeginScrollView(_pickerScroll, GUILayout.Height(160f));
			for (var i = 0; i < _outputOptions.Count; i++)
			{
				var opt = _outputOptions[i];
				if (!UiButton(opt.displayName)) continue;
				var slot = _edit.slots[_outputPickerSlot];
				slot.enabled = true;
				slot.resourceType = opt.resourceType;
				slot.pieceType = opt.pieceType;
				slot.polished = opt.polished;
				if (slot.percent <= 0f) slot.percent = 25f;
				_outputPickerSlot = -1;
				_editDirty = true;
				ExitGuiAfterLayoutMutation();
			}
			GUILayout.EndScrollView();
		}

		GUILayout.EndVertical();
	}

	private void DrawHoverPrompt()
	{
		if (!_showHoverPrompt) return;
		if (!TrySelectMinerFromLook(_hoverDistance, out _)) return;
		var anchor = _hoverPromptPosition switch
		{
			HoverPromptPosition.Top => UiHoverPromptAnchor.Top,
			HoverPromptPosition.Bottom => UiHoverPromptAnchor.Bottom,
			_ => UiHoverPromptAnchor.Center
		};
		UiHoverPromptRenderer.Draw(
			title: "Configurable Miners",
			subtitle: $"Press {_toggleKey} to configure miner",
			key: _toggleKey,
			labelStyle: _labelStyle ?? GUI.skin.label,
			anchor: anchor,
			tryGetHotbarTopGuiY: () => TryGetHotbarTopGuiY(out var y) ? y : (float?)null,
			drawBorder: DrawRgbWaveBorder);
	}

	private void DrawConfigPanel(Rect parent)
	{
		try
		{
			CapturePendingKeybind(Event.current);

			UiConfigMenuShell.DrawSinglePanel(parent, 8f, 440f, _windowStyle ?? GUI.skin.window, rect =>
			{
			_configScroll = UiConfigMenuShell.DrawScrollablePaddedContent(
				rect,
				_configScroll,
				() =>
				{
					GUILayout.Label("Config", _labelStyle ?? GUI.skin.label);

					DrawFloatTextRow("Select Distance", ref _selectDistance, 1f, 50f);
					DrawFloatTextRow("Hover Distance", ref _hoverDistance, 0.5f, 50f);
					DrawFloatTextRow("Apply Interval", ref _applyInterval, 0.05f, 5f);
					DrawFloatTextRow("Rescan Interval", ref _rescanInterval, 0.2f, 15f);
					DrawFloatTextRow("Match Epsilon", ref _positionMatchEpsilon, 0.02f, 2f);
					DrawStylePresetRow();
					DrawSettingToggleRow("Show Hover Prompt", ref _showHoverPrompt);
					DrawSettingToggleRow("Show Hover Details", ref _showBuildingInfoDetails);

					GUILayout.BeginHorizontal();
					GUILayout.Label("Hover Prompt Position", _labelStyle ?? GUI.skin.label, GUILayout.Width(170f));
					var newPos = _hoverPromptPosition;
					if (UiButton(newPos == HoverPromptPosition.Center ? "[Center]" : "Center", GUILayout.Width(70f)))
					{
						newPos = HoverPromptPosition.Center;
					}
					if (UiButton(newPos == HoverPromptPosition.Top ? "[Top]" : "Top", GUILayout.Width(50f)))
					{
						newPos = HoverPromptPosition.Top;
					}
					if (UiButton(newPos == HoverPromptPosition.Bottom ? "[Bottom]" : "Bottom", GUILayout.Width(70f)))
					{
						newPos = HoverPromptPosition.Bottom;
					}
					if (newPos != _hoverPromptPosition)
					{
						_hoverPromptPosition = newPos;
						SaveSettingsToDisk();
					}
					GUILayout.EndHorizontal();

					DrawSettingToggleRow("RGB Border", ref _rgbBorderEnabled);
					DrawFloatTextRow("RGB Border Width", ref _rgbBorderWidth, 1f, 8f);
					DrawFloatTextRow("RGB Border Segment", ref _rgbBorderSegment, 1f, 24f);
					DrawFloatTextRow("RGB Border Speed", ref _rgbBorderSpeed, 0f, 5f);

					GUILayout.Space(6f);
					GUILayout.Label("Group:", _labelStyle ?? GUI.skin.label);
					var previousGroupCount = _groupCount;
					DrawIntTextRow("Group Count", ref _groupCount, MinGroupCount, MaxGroupCount);
					if (_groupCount != previousGroupCount)
					{
						SetGroupCount(_groupCount);
						SaveSettingsToDisk();
					}
					for (var group = 1; group <= _groupCount; group++)
					{
						DrawGroupNameRow(group);
					}

					GUILayout.Space(6f);
					GUILayout.Label("Presets:", _labelStyle ?? GUI.skin.label);
					var previousPresetCount = GetPresetVisibleCount();
					var presetCount = previousPresetCount;
					DrawIntTextRow("Preset Count", ref presetCount, MinPresetCount, MaxPresetCount);
					if (presetCount != previousPresetCount)
					{
						SetPresetCount(presetCount);
						SaveSettingsToDisk();
						SavePresetsToDisk();
						RebuildUnityPresetEditor();
						if (_unityConfigOpen)
						{
							RebuildUnityConfigOverlayContent();
						}
					}
					for (var preset = 0; preset < GetPresetVisibleCount(); preset++)
					{
						DrawPresetNameRow(preset);
					}

					DrawSettingToggleRow("Debug Logging", ref _debugLogging);
					DrawSettingToggleRow("Debug Toasts", ref _debugToasts);

					GUILayout.Space(8f);
					if (ShouldUseInlineKeybindUi())
					{
						GUILayout.Label(_keybindCapture.IsCapturing
							? "Press any key to set. Press Escape to clear."
							: "Click a key to rebind.",
							_labelStyle ?? GUI.skin.label);
						DrawKeybindRow("Open Key", ConfigKeybindCaptureTarget.Open, _toggleKey, DefaultToggleKey);
						DrawKeybindRow("Close Key", ConfigKeybindCaptureTarget.Close, _closeKey, DefaultCloseKey);
					}
					else
					{
						GUILayout.Label(
							"Rebind detected. Change Open/Close keys in Settings > Keybinds > MODS > Configurable Miners.",
							_labelStyle ?? GUI.skin.label);
					}

					if (UiButton("Save Settings")) SaveSettingsToDisk();
				});
			});
		}
		catch (Exception ex)
		{
			if (!_configUiErrorLogged)
			{
				Logger.LogError($"Config UI draw failed: {ex}");
				_configUiErrorLogged = true;
			}
			_configPanelOpen = false;
			PushToast("Config UI error - panel closed (see log)", ToastType.Warning);
		}
	}

	private enum ConfigKeybindCaptureTarget
	{
		Open,
		Close
	}

	private void DrawKeybindRow(string label, ConfigKeybindCaptureTarget target, KeyCode current, KeyCode defaultValue)
	{
		var captureId = target.ToString();
		UiConfigRowWidgets.DrawKeybindCaptureRow(
			label,
			captureId,
			current,
			defaultValue,
			_keybindCapture,
			_labelStyle ?? GUI.skin.label,
			onBeginCapture: BeginUiManagerSuppression,
			onSet: key => SetCapturedKeybind(target, key),
			onEndCapture: EndUiManagerSuppression,
			buttonStyle: _richButtonStyle);
	}

	private void CapturePendingKeybind(Event evt)
	{
		if (!_keybindCapture.TryCapture(evt, out var captureId, out var key)) return;
		if (!Enum.TryParse<ConfigKeybindCaptureTarget>(captureId, out var target)) return;
		SetCapturedKeybind(target, key);
		EndUiManagerSuppression();
	}

	private void SetCapturedKeybind(ConfigKeybindCaptureTarget target, KeyCode key)
	{
		switch (target)
		{
			case ConfigKeybindCaptureTarget.Open:
				_toggleKey = key;
				break;
			case ConfigKeybindCaptureTarget.Close:
				_closeKey = key;
				break;
		}

		SaveSettingsToDisk();
		var keyName = key == KeyCode.None ? "None" : key.ToString();
		PushToast($"{target} key set to {keyName}", ToastType.Info);
	}

	private void DrawStylePresetRow()
	{
		GUILayout.BeginHorizontal();
		GUILayout.Label("UI Style", _labelStyle ?? GUI.skin.label, GUILayout.Width(170f));
		if (UiButton(_stylePreset, GUILayout.Width(150f)))
		{
			var next = UiStylePresets.GetNextPresetId(_stylePreset);
			if (!string.Equals(next, _stylePreset, StringComparison.Ordinal))
			{
				_stylePreset = next;
				_stylesInitialized = false;
				SaveSettingsToDisk();
			}
		}
		GUILayout.EndHorizontal();
	}

}
