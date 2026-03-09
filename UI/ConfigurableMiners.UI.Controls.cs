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
	private bool UiButton(string text, params GUILayoutOption[] options)
	{
		return UiButtonUtils.DrawRichButton(text, _richButtonStyle, options);
	}

	private static float DrawCenteredSlider(float value, float min, float max, float width, float rowHeight, float downBias = 2f)
	{
		return UiConfigRowUtils.DrawCenteredHorizontalSlider(value, min, max, width, rowHeight, downBias);
	}

	private void DrawFloatTextRow(string label, ref float value, float min, float max)
	{
		var cacheKey = $"cfg:{label}";
		var rowHeight = Mathf.Max(24f, DefaultWindowRowHeight);
		var slider = UiConfigRowWidgets.DrawFloatSliderTextRow(
			_sliderTextCache,
			cacheKey,
			label,
			value,
			min,
			max,
			_labelStyle ?? GUI.skin.label,
			_textFieldStyle ?? GUI.skin.textField,
			ref _clearFocusNextGui,
			labelWidth: 170f,
			sliderWidth: 140f,
			textWidth: 66f,
			rowHeight: rowHeight,
			centeredSlider: true);
		if (Mathf.Abs(slider - value) <= 0.0001f) return;
		value = slider;
		SaveSettingsToDisk();
	}

	private void DrawSettingToggleRow(string label, ref bool value)
	{
		var next = UiConfigRowWidgets.DrawToggleRow(label, value, _labelStyle ?? GUI.skin.label);
		if (next == value) return;
		value = next;
		SaveSettingsToDisk();
	}

	private void DrawIntTextRow(string label, ref int value, int min, int max)
	{
		var cacheKey = $"cfg:{label}";
		var rowHeight = Mathf.Max(24f, DefaultWindowRowHeight);
		var slider = UiConfigRowWidgets.DrawIntSliderTextRow(
			_sliderTextCache,
			cacheKey,
			label,
			value,
			min,
			max,
			_labelStyle ?? GUI.skin.label,
			_textFieldStyle ?? GUI.skin.textField,
			ref _clearFocusNextGui,
			labelWidth: 170f,
			sliderWidth: 140f,
			textWidth: 66f,
			rowHeight: rowHeight,
			centeredSlider: true);
		if (slider == value) return;
		value = slider;
		SaveSettingsToDisk();
	}

	private void DrawTextRow(string label, ref string value, string fallback)
	{
		var cacheKey = $"cfg_text:{label}";
		var currentText = value ?? string.Empty;
		GUILayout.BeginHorizontal();
		GUILayout.Label(label, _labelStyle ?? GUI.skin.label, GUILayout.Width(170f));
		GUI.SetNextControlName(cacheKey);
		var next = GUILayout.TextField(currentText, _textFieldStyle ?? GUI.skin.textField, GUILayout.Width(150f));
		GUILayout.EndHorizontal();

		var hasFocus = GUI.GetNameOfFocusedControl() == cacheKey;
		if (hasFocus)
		{
			if (Event.current.type == EventType.KeyDown &&
				(Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
			{
				_clearFocusNextGui = true;
			}
		}
		else
		{
			next = string.IsNullOrWhiteSpace(next) ? fallback : next.Trim();
		}

		if (string.Equals(next, value, StringComparison.Ordinal)) return;
		value = next;
		SaveSettingsToDisk();
	}

	private void DrawGroupNameRow(int group)
	{
		EnsureGroupNamesInitialized();
		var index = group - 1;
		if (index < 0 || index >= _groupNames.Count) return;
		var cacheKey = $"cfg_group_name:{group}";
		var currentText = _groupNames[index] ?? string.Empty;
		GUILayout.BeginHorizontal();
		GUILayout.Label($"Group {group} Name", _labelStyle ?? GUI.skin.label, GUILayout.Width(170f));
		GUI.SetNextControlName(cacheKey);
		var next = GUILayout.TextField(currentText, _textFieldStyle ?? GUI.skin.textField, GUILayout.Width(150f));
		GUILayout.EndHorizontal();

		var hasFocus = GUI.GetNameOfFocusedControl() == cacheKey;
		if (hasFocus)
		{
			if (Event.current.type == EventType.KeyDown &&
				(Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
			{
				_clearFocusNextGui = true;
			}
		}
		else
		{
			next = string.IsNullOrWhiteSpace(next) ? BuildDefaultGroupName(group) : next.Trim();
		}

		if (string.Equals(next, _groupNames[index], StringComparison.Ordinal)) return;
		_groupNames[index] = next;
		SyncLegacyGroupNameFields();
		SaveSettingsToDisk();
	}

	private void DrawPresetNameRow(int index)
	{
		if (index < 0 || index >= GetPresetVisibleCount()) return;
		var cacheKey = $"cfg_preset_name:{index + 1}";
		var currentText = GetPresetName(index);
		GUILayout.BeginHorizontal();
		GUILayout.Label($"Preset {index + 1} Name", _labelStyle ?? GUI.skin.label, GUILayout.Width(170f));
		GUI.SetNextControlName(cacheKey);
		var next = GUILayout.TextField(currentText, _textFieldStyle ?? GUI.skin.textField, GUILayout.Width(150f));
		GUILayout.EndHorizontal();

		var hasFocus = GUI.GetNameOfFocusedControl() == cacheKey;
		if (hasFocus)
		{
			if (Event.current.type == EventType.KeyDown &&
				(Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
			{
				_clearFocusNextGui = true;
			}
		}
		else
		{
			next = string.IsNullOrWhiteSpace(next) ? BuildDefaultPresetName(index) : next.Trim();
		}

		if (string.Equals(next, currentText, StringComparison.Ordinal)) return;
		SetPresetName(index, next);
	}

}
