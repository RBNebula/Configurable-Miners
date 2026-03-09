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
	private void DrawToggleRow(string label, ref bool value)
	{
		var next = UiConfigRowWidgets.DrawToggleRow(label, value, _labelStyle ?? GUI.skin.label);
		if (next == value) return;
		value = next;
		_editDirty = true;
	}

	private void DrawSliderRow(string label, ref float value, float min, float max, string format)
	{
		var cacheKey = $"main:{label}";
		if (!_sliderTextCache.TryGetValue(cacheKey, out var text))
		{
			text = value.ToString(format, System.Globalization.CultureInfo.InvariantCulture);
		}

		var rowHeight = Mathf.Max(24f, DefaultWindowRowHeight);
		GUILayout.BeginHorizontal(GUILayout.Height(rowHeight));
		GUILayout.Label(label, _labelStyle ?? GUI.skin.label, GUILayout.Width(220f), GUILayout.Height(rowHeight));
		var next = DrawCenteredSlider(value, min, max, 200f, rowHeight);
		GUILayout.Space(8f);
		GUI.SetNextControlName(cacheKey);
		text = GUILayout.TextField(text, _textFieldStyle ?? GUI.skin.textField, GUILayout.Width(66f), GUILayout.Height(rowHeight));
		GUILayout.EndHorizontal();

		var hasFocus = GUI.GetNameOfFocusedControl() == cacheKey;
		if (hasFocus)
		{
			if (float.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
			{
				next = Mathf.Clamp(parsed, min, max);
			}
			if (Event.current.type == EventType.KeyDown &&
				(Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
			{
				_clearFocusNextGui = true;
			}
		}
		else
		{
			text = next.ToString(format, System.Globalization.CultureInfo.InvariantCulture);
		}

		_sliderTextCache[cacheKey] = text;
		if (Mathf.Abs(next - value) <= 0.0001f) return;
		value = next;
		_editDirty = true;
	}

	private void DrawGroupSelectorRow()
	{
		_edit.group = Mathf.Clamp(_edit.group, 0, _groupCount);
		GUILayout.BeginHorizontal();
		GUILayout.FlexibleSpace();
		if (UiButton(GetGroupDisplayName(_edit.group), GUILayout.Width(240f)))
		{
			_groupPickerOpen = !_groupPickerOpen;
			ExitGuiAfterLayoutMutation();
		}
		if (_groupPickerOpen && UiButton("Close", GUILayout.Width(70f)))
		{
			_groupPickerOpen = false;
			ExitGuiAfterLayoutMutation();
		}
		GUILayout.FlexibleSpace();
		GUILayout.EndHorizontal();
	}

	private void DrawGroupPicker()
	{
		GUILayout.BeginVertical(GUI.skin.box);
		GUILayout.BeginHorizontal();
		GUILayout.Label("Select Group", _labelStyle ?? GUI.skin.label);
		GUILayout.FlexibleSpace();
		if (UiButton("Close", GUILayout.Width(80f)))
		{
			_groupPickerOpen = false;
			ExitGuiAfterLayoutMutation();
		}
		GUILayout.EndHorizontal();

		DrawGroupPickerOption(0, GetGroupLabelWithCount("None", 0));
		for (var group = 1; group <= _groupCount; group++)
		{
			DrawGroupPickerOption(group, GetGroupLabelWithCount(GetGroupDisplayName(group), group));
		}

		GUILayout.EndVertical();
	}

	private void DrawGroupPickerOption(int value, string label)
	{
		if (!UiButton(label)) return;
		if (_edit.group != value)
		{
			_edit.group = value;
			_editDirty = true;
		}
		_groupPickerOpen = false;
		ExitGuiAfterLayoutMutation();
	}

	private string GetGroupLabelWithCount(string baseLabel, int group)
	{
		var count = 0;
		foreach (var kv in _minerCache)
		{
			if (kv.Value.miner is not MonoBehaviour mb || mb == null) continue;
			var cfg = ReadConfigForMiner(mb) ?? FindSavedEntryForPosition(mb.transform.position);
			var minerGroup = cfg?.group ?? 0;
			if (minerGroup != group) continue;
			count++;
		}
		return $"{baseLabel} ({count})";
	}

	private void DrawAreaApplyRadiusRow(float labelWidth = 220f, float sliderWidth = 200f, float textWidth = 66f, float rowHeight = 0f)
	{
		const string cacheKey = "main:AreaApplyRadius";
		if (!_sliderTextCache.TryGetValue(cacheKey, out var text))
		{
			text = _areaApplyRadius.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
		}

		var effectiveRowHeight = rowHeight > 0f ? rowHeight : Mathf.Max(24f, DefaultWindowRowHeight);
		GUILayout.BeginHorizontal(GUILayout.Height(effectiveRowHeight));
		GUILayout.Label("Area Apply Radius (m)", _labelStyle ?? GUI.skin.label, GUILayout.Width(labelWidth), GUILayout.Height(effectiveRowHeight));
		var next = DrawCenteredSlider(_areaApplyRadius, 1f, 250f, sliderWidth, effectiveRowHeight);
		GUILayout.Space(8f);
		GUI.SetNextControlName(cacheKey);
		text = GUILayout.TextField(text, _textFieldStyle ?? GUI.skin.textField, GUILayout.Width(textWidth), GUILayout.Height(effectiveRowHeight));
		GUILayout.EndHorizontal();

		var hasFocus = GUI.GetNameOfFocusedControl() == cacheKey;
		if (hasFocus)
		{
			if (float.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
			{
				next = Mathf.Clamp(parsed, 1f, 250f);
			}
			if (Event.current.type == EventType.KeyDown &&
				(Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
			{
				_clearFocusNextGui = true;
			}
		}
		else
		{
			text = next.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
		}

		_sliderTextCache[cacheKey] = text;
		if (Mathf.Abs(next - _areaApplyRadius) <= 0.0001f) return;
		_areaApplyRadius = next;
		SaveSettingsToDisk();
	}

	private void DrawSlotRow(int slotIndex)
	{
		var slot = _edit.slots[slotIndex];
		GUILayout.BeginHorizontal();
		var nextEnabled = GUILayout.Toggle(slot.enabled, GUIContent.none, GUI.skin.toggle, GUILayout.Width(18f));
		if (nextEnabled != slot.enabled)
		{
			slot.enabled = nextEnabled;
			_editDirty = true;
		}

		var pieceDisplay = string.Equals(slot.pieceType, "OreCluster", StringComparison.OrdinalIgnoreCase) ? "Ore Cluster" : slot.pieceType;
		var display = string.IsNullOrWhiteSpace(slot.resourceType)
			? "Choose Output"
			: $"{slot.resourceType} {pieceDisplay}" + (slot.polished ? " (P)" : string.Empty);
		if (UiButton($"S{slotIndex + 1}: {display}", GUILayout.Width(220f)))
		{
			_outputPickerSlot = slotIndex;
			if (_outputOptions.Count == 0) RefreshOutputOptionsForCurrentFilter();
			ExitGuiAfterLayoutMutation();
		}

		var percentCacheKey = $"slotPercent:{slotIndex}";
		if (!_sliderTextCache.TryGetValue(percentCacheKey, out var percentText))
		{
			percentText = slot.percent.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
		}

		var rowHeight = Mathf.Max(24f, DefaultWindowRowHeight);
		var pct = DrawCenteredSlider(slot.percent, 0f, 100f, 140f, rowHeight);
		GUILayout.Space(8f);
		GUI.SetNextControlName(percentCacheKey);
		percentText = GUILayout.TextField(percentText, _textFieldStyle ?? GUI.skin.textField, GUILayout.Width(66f), GUILayout.Height(rowHeight));
		var hasPercentFocus = GUI.GetNameOfFocusedControl() == percentCacheKey;
		if (hasPercentFocus)
		{
			var parseText = percentText.Trim();
			if (parseText.EndsWith("%", StringComparison.Ordinal))
			{
				parseText = parseText.Substring(0, parseText.Length - 1).TrimEnd();
			}
			if (float.TryParse(parseText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsedPct))
			{
				pct = Mathf.Clamp(parsedPct, 0f, 100f);
			}
			if (Event.current.type == EventType.KeyDown &&
				(Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
			{
				_clearFocusNextGui = true;
			}
		}
		else
		{
			percentText = pct.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
		}
		_sliderTextCache[percentCacheKey] = percentText;

		GUILayout.FlexibleSpace();
		if (Mathf.Abs(pct - slot.percent) > 0.0001f)
		{
			slot.percent = pct;
			_editDirty = true;
		}
		var nextLocked = GUILayout.Toggle(slot.locked, "Lock:", GUI.skin.toggle, GUILayout.Width(68f));
		if (nextLocked != slot.locked)
		{
			slot.locked = nextLocked;
			_editDirty = true;
		}
		GUILayout.EndHorizontal();
	}

	private void NormalizeSlotPercentages()
	{
		for (var i = 0; i < _edit.slots.Length; i++)
		{
			var slot = _edit.slots[i];
			var hasOutput = !string.IsNullOrWhiteSpace(slot.resourceType) && !string.IsNullOrWhiteSpace(slot.pieceType);
			if (!slot.enabled || !hasOutput)
			{
				slot.percent = 0f;
			}
		}

		var active = _edit.slots.Where(s => s.enabled && !string.IsNullOrWhiteSpace(s.resourceType)).ToArray();
		if (active.Length == 0)
		{
			PushToast("No active slots to normalize", ToastType.Warning);
			return;
		}

		var locked = active.Where(s => s.locked).ToArray();
		var unlocked = active.Where(s => !s.locked).ToArray();
		var lockedTotal = locked.Sum(s => Mathf.Max(0f, s.percent));

		if (lockedTotal > 100f && locked.Length > 0)
		{
			var scale = 100f / lockedTotal;
			for (var i = 0; i < locked.Length; i++)
			{
				locked[i].percent = Mathf.Max(0f, locked[i].percent) * scale;
			}
			lockedTotal = 100f;
		}

		var remaining = Mathf.Max(0f, 100f - lockedTotal);
		if (unlocked.Length > 0)
		{
			var each = remaining / unlocked.Length;
			for (var i = 0; i < unlocked.Length; i++) unlocked[i].percent = each;
		}
		else if (locked.Length > 0 && remaining > 0.001f)
		{
			var each = 100f / locked.Length;
			for (var i = 0; i < locked.Length; i++) locked[i].percent = each;
		}

		_editDirty = true;
		PushToast($"Normalized {active.Length} slot percentages (locks respected)", ToastType.Info);
	}

	private void DrawPresetCell(int index, float width = 245f)
	{
		if (index < 0 || index >= GetPresetVisibleCount()) return;
		GUILayout.BeginVertical(GUILayout.Width(width));
		GUILayout.BeginHorizontal();
		GUILayout.Label($"Preset {index + 1}:", _labelStyle ?? GUI.skin.label, GUILayout.Width(74f));
		if (UiButton("Save", GUILayout.Width(68f)))
		{
			_presets[index].hasValue = true;
			_presets[index].overrideOutputs = _edit.overrideOutputs;
			_presets[index].spawnRate = _edit.spawnRate;
			_presets[index].spawnProbability = _edit.spawnProbability;
			_presets[index].allowGems = _edit.allowGems;
			_presets[index].polishedGems = _edit.polishedGems;
			_presets[index].allowGeodes = _edit.allowGeodes;
			_presets[index].polishedGeodes = _edit.polishedGeodes;
			_presets[index].group = Mathf.Clamp(_edit.group, 0, _groupCount);
			_presets[index].slots = CloneConfig(_edit).slots;
			SavePresetsToDisk();
			PushToast($"Saved preset {index + 1}", ToastType.Success);
		}
		GUILayout.Label("|", _labelStyle ?? GUI.skin.label, GUILayout.Width(8f));
		if (UiButton("Load", GUILayout.Width(68f)))
		{
			if (_presets[index].hasValue)
			{
				_edit.overrideOutputs = _presets[index].overrideOutputs;
				_edit.spawnRate = _presets[index].spawnRate;
				_edit.spawnProbability = _presets[index].spawnProbability;
				_edit.allowGems = _presets[index].allowGems;
				_edit.polishedGems = _presets[index].polishedGems;
				_edit.allowGeodes = _presets[index].allowGeodes;
				_edit.polishedGeodes = _presets[index].polishedGeodes;
				_edit.group = Mathf.Clamp(_presets[index].group, 0, _groupCount);
				_edit.slots = CloneConfig(new MinerConfigEntry { slots = _presets[index].slots }).slots;
				RefreshOutputOptionsForCurrentFilter();
				_editDirty = true;
				PushToast($"Loaded preset {index + 1}", ToastType.Success);
			}
			else
			{
				PushToast($"Preset {index + 1} is empty", ToastType.Warning);
			}
		}
		GUILayout.EndHorizontal();
		GUILayout.EndVertical();
	}

	private void DrawGemGeodeTogglesRow()
	{
		GUILayout.BeginHorizontal();
		DrawInlineToggle("Allow Gems", ref _edit.allowGems, 120f);
		DrawInlineToggle("Polished Gems", ref _edit.polishedGems, 130f, !_edit.allowGems);
		GUILayout.Space(12f);
		DrawInlineToggle("Allow Geodes", ref _edit.allowGeodes, 130f);
		DrawInlineToggle("Polished Geodes", ref _edit.polishedGeodes, 140f, !_edit.allowGeodes);
		GUILayout.EndHorizontal();
	}

	private void DrawInlineToggle(string label, ref bool value, float width, bool disabled = false)
	{
		var previous = GUI.enabled;
		if (disabled) GUI.enabled = false;
		var next = GUILayout.Toggle(value, label, GUI.skin.toggle, GUILayout.Width(width));
		GUI.enabled = previous;
		if (disabled) return;
		if (next == value) return;
		value = next;
		_editDirty = true;
	}

}
