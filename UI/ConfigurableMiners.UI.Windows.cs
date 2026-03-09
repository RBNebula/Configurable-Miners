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
	private void DrawWindow(int id)
	{
		GUILayout.BeginHorizontal();
		GUILayout.FlexibleSpace();
		GUILayout.Label(_editDirty ? "Configurable Miners *" : "Configurable Miners", _labelStyle ?? GUI.skin.label);
		GUILayout.FlexibleSpace();
		GUILayout.EndHorizontal();
		GUILayout.Label(string.IsNullOrWhiteSpace(_selectedMinerLabel) ? "No miner selected" : _selectedMinerLabel, _labelStyle ?? GUI.skin.label);
		GUILayout.Label($"Pos: {_selectedMinerPos.x:0.##}, {_selectedMinerPos.y:0.##}, {_selectedMinerPos.z:0.##}", _labelStyle ?? GUI.skin.label);
		const float actionsBoxHeight = 128f;
		GUILayout.BeginVertical(GUILayout.ExpandHeight(true));
		_mainScroll = GUILayout.BeginScrollView(_mainScroll, GUILayout.ExpandHeight(true));

		GUILayout.BeginVertical(GUI.skin.box);
		DrawSectionTitle("Group");
		DrawGroupSelectorRow();
		GUILayout.EndVertical();

		GUILayout.BeginVertical(GUI.skin.box);
		DrawSliderRow("Spawn Rate (Seconds)", ref _edit.spawnRate, 0.01f, 20f, "0.###");
		DrawSliderRow("Spawn Probability (%)", ref _edit.spawnProbability, 0f, 100f, "0.##");
		GUILayout.EndVertical();

		GUILayout.BeginVertical(GUI.skin.box);
		DrawSectionTitle("Custom output");
		DrawToggleRow("Override this miner's ore output", ref _edit.overrideOutputs);
		var previousAllowGems = _edit.allowGems;
		var previousPolishedGems = _edit.polishedGems;
		var previousAllowGeodes = _edit.allowGeodes;
		var previousPolishedGeodes = _edit.polishedGeodes;
		DrawGemGeodeTogglesRow();
		if (previousAllowGems != _edit.allowGems)
		{
			RefreshOutputOptionsForCurrentFilter();
		}
		if (previousPolishedGems != _edit.polishedGems)
		{
			RefreshOutputOptionsForCurrentFilter();
		}
		if (previousAllowGeodes != _edit.allowGeodes)
		{
			RefreshOutputOptionsForCurrentFilter();
		}
		if (previousPolishedGeodes != _edit.polishedGeodes)
		{
			RefreshOutputOptionsForCurrentFilter();
		}
		GUILayout.BeginHorizontal();
		GUILayout.Label("Custom Slots (4)", _labelStyle ?? GUI.skin.label);
		var configuredTotal = _edit.slots
			.Where(s => s.enabled && !string.IsNullOrWhiteSpace(s.resourceType) && !string.IsNullOrWhiteSpace(s.pieceType))
			.Sum(s => Mathf.Max(0f, s.percent));
		GUILayout.Space(8f);
		GUILayout.Label($"Total: {configuredTotal:0.#}%", _labelStyle ?? GUI.skin.label, GUILayout.Width(92f));
		if (UiButton("Normalize %", GUILayout.Width(120f)))
		{
			NormalizeSlotPercentages();
		}
		GUILayout.EndHorizontal();
		for (var i = 0; i < 4; i++) DrawSlotRow(i);
		GUILayout.EndVertical();

		GUILayout.Space(8f);
		if (_outputPickerSlot >= 0)
		{
			DrawOutputPicker();
			GUILayout.Space(8f);
		}
		else if (_groupPickerOpen)
		{
			DrawGroupPicker();
			GUILayout.Space(8f);
		}

		GUILayout.Space(6f);
		GUILayout.BeginVertical(GUI.skin.box);
		DrawSectionTitle("Presets");
		const float presetColumnWidth = 245f;
		const float presetColumnGap = 20f;
		var presetCount = GetPresetVisibleCount();
		var rowCount = Mathf.CeilToInt(presetCount / 2f);
		GUILayout.BeginHorizontal();
		GUILayout.FlexibleSpace();
		GUILayout.BeginVertical(GUILayout.Width(presetColumnWidth));
		for (var row = 0; row < rowCount; row++)
		{
			var leftIndex = row * 2;
			if (leftIndex < presetCount)
			{
				DrawPresetCell(leftIndex, presetColumnWidth);
			}
		}
		GUILayout.EndVertical();
		GUILayout.Space(presetColumnGap);
		GUILayout.BeginVertical(GUILayout.Width(presetColumnWidth));
		for (var row = 0; row < rowCount; row++)
		{
			var rightIndex = (row * 2) + 1;
			if (rightIndex < presetCount)
			{
				DrawPresetCell(rightIndex, presetColumnWidth);
			}
		}
		GUILayout.EndVertical();
		GUILayout.FlexibleSpace();
		GUILayout.EndHorizontal();
		GUILayout.EndVertical();
		GUILayout.EndScrollView();
		GUILayout.EndVertical();

		GUILayout.BeginVertical(GUI.skin.box, GUILayout.Height(actionsBoxHeight));
		DrawSectionTitle("Actions");
		const float actionButtonHeight = 26f;
		const float columnGap = 8f;
		const float buttonGap = 6f;
		const float textWidth = 56f;
		var actionsContentWidth = Mathf.Max(380f, DefaultWindowWidth - 70f);
		var actionButtonWidth = Mathf.Clamp(actionsContentWidth * 0.2f, 96f, 150f);
		var rightColumnWidth = Mathf.Max(280f, actionsContentWidth - actionButtonWidth - columnGap);
		var miscButtonWidth = Mathf.Clamp(Mathf.Floor((rightColumnWidth - (buttonGap * 4f)) / 5f), 72f, 84f);
		var labelWidth = Mathf.Clamp(150f, 120f, rightColumnWidth - textWidth - 90f);
		var sliderWidth = Mathf.Max(90f, rightColumnWidth - labelWidth - textWidth - 8f);

		GUILayout.BeginHorizontal();
		GUILayout.BeginVertical(GUILayout.Width(actionButtonWidth));
		GUILayout.Space(2f);
		if (UiButton("Apply Area", GUILayout.Width(actionButtonWidth), GUILayout.Height(actionButtonHeight)))
		{
			ApplyCurrentConfigAroundPlayer();
		}
		if (UiButton("Apply Group", GUILayout.Width(actionButtonWidth), GUILayout.Height(actionButtonHeight)))
		{
			ApplyCurrentConfigToGroup();
		}
		if (UiButton("Apply", GUILayout.Width(actionButtonWidth), GUILayout.Height(actionButtonHeight)))
		{
			ApplySelectedConfig();
		}
		GUILayout.EndVertical();

		GUILayout.Space(columnGap);
		GUILayout.BeginVertical(GUILayout.Width(rightColumnWidth));
		GUILayout.Space(actionButtonHeight);
		DrawAreaApplyRadiusRow(labelWidth, sliderWidth, textWidth, actionButtonHeight);
		GUILayout.Space(buttonGap);
		GUILayout.BeginHorizontal();
		if (UiButton("Copy", GUILayout.Width(miscButtonWidth), GUILayout.Height(actionButtonHeight)))
		{
			CopySelectedConfigToClipboard();
		}
		GUILayout.Space(buttonGap);
		if (UiButton("Paste", GUILayout.Width(miscButtonWidth), GUILayout.Height(actionButtonHeight)))
		{
			PasteClipboardToSelectedConfig();
		}
		GUILayout.Space(buttonGap);
		if (UiButton("Revert", GUILayout.Width(miscButtonWidth), GUILayout.Height(actionButtonHeight)))
		{
			RevertSelectedMiner();
		}
		GUILayout.Space(buttonGap);
		if (UiButton("Config", GUILayout.Width(miscButtonWidth), GUILayout.Height(actionButtonHeight)))
		{
			_configPanelOpen = !_configPanelOpen;
			ExitGuiAfterLayoutMutation();
		}
		GUILayout.Space(buttonGap);
		if (UiButton("Close", GUILayout.Width(miscButtonWidth), GUILayout.Height(actionButtonHeight)))
		{
			_closeFocusAndClose();
			ExitGuiAfterLayoutMutation();
		}
		GUILayout.EndHorizontal();
		GUILayout.EndVertical();
		GUILayout.EndHorizontal();
		GUILayout.EndVertical();
		GUI.DragWindow();
	}

	private void DrawToggleMenuWindow(int id)
	{
		GUILayout.BeginHorizontal();
		GUILayout.FlexibleSpace();
		GUILayout.Label("Miner Toggle Menu", _labelStyle ?? GUI.skin.label);
		GUILayout.FlexibleSpace();
		GUILayout.EndHorizontal();

		GUILayout.Label("Toggle all miners or specific groups", _labelStyle ?? GUI.skin.label);
		GUILayout.Space(8f);

		DrawToggleMenuRow("All AutoMiners", -1);
		for (var group = 1; group <= _groupCount; group++)
		{
			DrawToggleMenuRow(GetGroupLabelWithCount(GetGroupDisplayName(group), group), group);
		}

		GUILayout.Space(8f);
		GUILayout.BeginHorizontal();
		if (UiButton("Close"))
		{
			_closeFocusAndClose();
			ExitGuiAfterLayoutMutation();
		}
		GUILayout.EndHorizontal();

		GUI.DragWindow();
	}

	private void DrawToggleMenuRow(string label, int group)
	{
		GUILayout.BeginHorizontal();
		GUILayout.Label(label, _labelStyle ?? GUI.skin.label, GUILayout.Width(220f));
		if (UiButton("On", GUILayout.Width(80f)))
		{
			if (group < 0) ToggleAllMiners(true);
			else ToggleGroupMiners(group, true);
		}
		if (UiButton("Off", GUILayout.Width(80f)))
		{
			if (group < 0) ToggleAllMiners(false);
			else ToggleGroupMiners(group, false);
		}
		GUILayout.EndHorizontal();
	}

	private void DrawSectionTitle(string title)
	{
		var style = _labelStyle ?? GUI.skin.label;
		GUILayout.BeginHorizontal();
		GUILayout.FlexibleSpace();
		GUILayout.Label($"<size=112%>{title}</size>", style);
		GUILayout.FlexibleSpace();
		GUILayout.EndHorizontal();
	}

}
