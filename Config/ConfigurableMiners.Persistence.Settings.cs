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
	private void LoadSettingsFromDisk()
	{
		try
		{
			_settingsFilePath = System.IO.Path.Combine(Paths.ConfigPath, ConfigFolderName, SettingsFileName);
			var s = new SettingsData();
			if (System.IO.File.Exists(_settingsFilePath))
			{
				var json = System.IO.File.ReadAllText(_settingsFilePath);
				if (!string.IsNullOrWhiteSpace(json))
				{
					ParseSettingsJson(json, s);
				}
			}
			ApplySettings(s);
			SaveSettingsToDisk();
		}
		catch (Exception ex)
		{
			Logger.LogWarning($"Failed to load settings: {ex.Message}");
		}
	}

	private void SaveSettingsToDisk()
	{
		try
		{
			if (string.IsNullOrWhiteSpace(_settingsFilePath)) return;
			var dir = System.IO.Path.GetDirectoryName(_settingsFilePath);
			if (!string.IsNullOrWhiteSpace(dir)) System.IO.Directory.CreateDirectory(dir);
			var s = new SettingsData
			{
				toggleKey = _toggleKey.ToString(),
				closeKey = _closeKey.ToString(),
				selectDistance = _selectDistance,
				hoverDistance = _hoverDistance,
				applyInterval = _applyInterval,
				rescanInterval = _rescanInterval,
				positionMatchEpsilon = _positionMatchEpsilon,
				areaApplyRadius = _areaApplyRadius,
				stylePreset = _stylePreset,
				showHoverPrompt = _showHoverPrompt,
				hoverPromptPosition = GetHoverPromptPositionText(_hoverPromptPosition),
				showBuildingInfoDetails = _showBuildingInfoDetails,
				rgbBorderEnabled = _rgbBorderEnabled,
				rgbBorderWidth = _rgbBorderWidth,
				rgbBorderSegment = _rgbBorderSegment,
				rgbBorderSpeed = _rgbBorderSpeed,
				groupCount = _groupCount,
				presetCount = _presetCount,
				groupNames = GetConfiguredGroupNamesSnapshot(),
				groupNameA = _groupNameA,
				groupNameB = _groupNameB,
				groupNameC = _groupNameC,
				groupNameD = _groupNameD,
				debugLogging = _debugLogging,
				debugToasts = _debugToasts
			};
			System.IO.File.WriteAllText(_settingsFilePath, BuildSettingsJson(s));
		}
		catch (Exception ex)
		{
			Logger.LogWarning($"Failed to save settings: {ex.Message}");
		}
	}

	private void ApplySettings(SettingsData s)
	{
		_toggleKey = ParseKeyCode(s.toggleKey, DefaultToggleKey);
		_closeKey = ParseKeyCode(s.closeKey, DefaultCloseKey);
		_selectDistance = Mathf.Clamp(s.selectDistance, 1f, 50f);
		_hoverDistance = Mathf.Clamp(s.hoverDistance, 0.5f, 50f);
		_applyInterval = Mathf.Clamp(s.applyInterval, 0.05f, 5f);
		_rescanInterval = Mathf.Clamp(s.rescanInterval, 0.2f, 15f);
		_positionMatchEpsilon = Mathf.Clamp(s.positionMatchEpsilon, 0.02f, 2f);
		_areaApplyRadius = Mathf.Clamp(s.areaApplyRadius, 1f, 250f);
		_stylePreset = UiStylePresets.NormalizePresetId(s.stylePreset);
		_showHoverPrompt = s.showHoverPrompt;
		_hoverPromptPosition = ParseHoverPromptPosition(s.hoverPromptPosition);
		_showBuildingInfoDetails = s.showBuildingInfoDetails;
		_rgbBorderEnabled = s.rgbBorderEnabled;
		_rgbBorderWidth = Mathf.Clamp(s.rgbBorderWidth, 1f, 8f);
		_rgbBorderSegment = Mathf.Clamp(s.rgbBorderSegment, 1f, 24f);
		_rgbBorderSpeed = Mathf.Clamp(s.rgbBorderSpeed, 0f, 5f);
		SetGroupCount(s.groupCount <= 0 ? DefaultGroupCount : s.groupCount);
		SetPresetCount(s.presetCount <= 0 ? DefaultPresetCount : s.presetCount);
		_groupNames.Clear();
		var loadedNames = (s.groupNames ?? Array.Empty<string>()).Where(n => !string.IsNullOrWhiteSpace(n)).ToArray();
		if (loadedNames.Length > 0)
		{
			for (var i = 0; i < loadedNames.Length; i++)
			{
				_groupNames.Add(loadedNames[i].Trim());
			}
		}
		else
		{
			_groupNames.Add(string.IsNullOrWhiteSpace(s.groupNameA) ? DefaultGroupNameA : s.groupNameA.Trim());
			_groupNames.Add(string.IsNullOrWhiteSpace(s.groupNameB) ? DefaultGroupNameB : s.groupNameB.Trim());
			_groupNames.Add(string.IsNullOrWhiteSpace(s.groupNameC) ? DefaultGroupNameC : s.groupNameC.Trim());
			_groupNames.Add(string.IsNullOrWhiteSpace(s.groupNameD) ? DefaultGroupNameD : s.groupNameD.Trim());
		}
		EnsureGroupNamesInitialized();
		_debugLogging = s.debugLogging;
		_debugToasts = s.debugToasts;
	}

	private void TryResolveSavePaths(bool createFolderIfMissing)
	{
		try
		{
			var saveName = SavePathUtils.TryGetActiveSaveFolderName();
			if (string.IsNullOrWhiteSpace(saveName))
			{
				_debugSavePathResolved = false;
				_debugActiveSaveName = "N/A";
				return;
			}
			_debugActiveSaveName = saveName!.Trim();
			var safe = SavePathUtils.SanitizeFolderName(saveName!);
			if (string.IsNullOrWhiteSpace(safe))
			{
				_debugSavePathResolved = false;
				return;
			}
			var folder = System.IO.Path.Combine(Paths.ConfigPath, ConfigFolderName, safe);
			if (createFolderIfMissing && !System.IO.Directory.Exists(folder))
			{
				System.IO.Directory.CreateDirectory(folder);
			}

			var newSavePath = System.IO.Path.Combine(folder, SaveDataFileName);
			var newPresetPath = System.IO.Path.Combine(folder, PresetsFileName);
			var changed = !string.Equals(newSavePath, _saveDataFilePath, StringComparison.OrdinalIgnoreCase)
				|| !string.Equals(newPresetPath, _presetsFilePath, StringComparison.OrdinalIgnoreCase);
			_saveDataFilePath = newSavePath;
			_presetsFilePath = newPresetPath;
			_debugSavePathResolved = true;
			if (changed)
			{
				LoadSaveDataFromDisk();
				LoadPresetsFromDisk();
				RefreshMinerCache();
				ApplyAllSavedConfigs();
				PruneStaleSavedEntries();
				_nextRescanTime = Time.time + Mathf.Max(0.2f, _rescanInterval);
				_nextApplyTime = 0f;
			}
		}
		catch (Exception ex)
		{
			_debugSavePathResolved = false;
			Logger.LogWarning($"Failed to resolve save path: {ex.Message}");
		}
	}

	private static KeyCode ParseKeyCode(string text, KeyCode fallback)
	{
		if (!string.IsNullOrWhiteSpace(text) && Enum.TryParse(text.Trim(), true, out KeyCode parsed)) return parsed;
		return fallback;
	}

	private static void ParseSettingsJson(string json, SettingsData s)
	{
		s.toggleKey = JsonSettingsUtils.ReadString(json, "toggleKey", s.toggleKey);
		s.closeKey = JsonSettingsUtils.ReadString(json, "closeKey", s.closeKey);
		s.selectDistance = JsonSettingsUtils.ReadFloat(json, "selectDistance", s.selectDistance);
		s.hoverDistance = JsonSettingsUtils.ReadFloat(json, "hoverDistance", s.hoverDistance);
		s.applyInterval = JsonSettingsUtils.ReadFloat(json, "applyInterval", s.applyInterval);
		s.rescanInterval = JsonSettingsUtils.ReadFloat(json, "rescanInterval", s.rescanInterval);
		s.positionMatchEpsilon = JsonSettingsUtils.ReadFloat(json, "positionMatchEpsilon", s.positionMatchEpsilon);
		s.areaApplyRadius = JsonSettingsUtils.ReadFloat(json, "areaApplyRadius", s.areaApplyRadius);
		s.stylePreset = JsonSettingsUtils.ReadString(json, "stylePreset", s.stylePreset);
		s.showHoverPrompt = JsonSettingsUtils.ReadBool(json, "showHoverPrompt", s.showHoverPrompt);
		s.hoverPromptPosition = JsonSettingsUtils.ReadString(json, "hoverPromptPosition", s.hoverPromptPosition);
		s.showBuildingInfoDetails = JsonSettingsUtils.ReadBool(json, "showBuildingInfoDetails", s.showBuildingInfoDetails);
		s.rgbBorderEnabled = JsonSettingsUtils.ReadBool(json, "rgbBorderEnabled", s.rgbBorderEnabled);
		s.rgbBorderWidth = JsonSettingsUtils.ReadFloat(json, "rgbBorderWidth", s.rgbBorderWidth);
		s.rgbBorderSegment = JsonSettingsUtils.ReadFloat(json, "rgbBorderSegment", s.rgbBorderSegment);
		s.rgbBorderSpeed = JsonSettingsUtils.ReadFloat(json, "rgbBorderSpeed", s.rgbBorderSpeed);
		s.groupCount = JsonSettingsUtils.ReadInt(json, "groupCount", s.groupCount);
		s.presetCount = JsonSettingsUtils.ReadInt(json, "presetCount", s.presetCount);
		s.groupNames = ParseStringArray(JsonSettingsUtils.ReadArrayRaw(json, "groupNames"));
		s.groupNameA = JsonSettingsUtils.ReadString(json, "groupNameA", s.groupNameA);
		s.groupNameB = JsonSettingsUtils.ReadString(json, "groupNameB", s.groupNameB);
		s.groupNameC = JsonSettingsUtils.ReadString(json, "groupNameC", s.groupNameC);
		s.groupNameD = JsonSettingsUtils.ReadString(json, "groupNameD", s.groupNameD);
		s.debugLogging = JsonSettingsUtils.ReadBool(json, "debugLogging", s.debugLogging);
		s.debugToasts = JsonSettingsUtils.ReadBool(json, "debugToasts", s.debugToasts);
	}

	private static string BuildSettingsJson(SettingsData s)
	{
		var lines = new List<string>
		{
			"{",
			$"  \"toggleKey\": \"{JsonSettingsUtils.EscapeJson(s.toggleKey)}\",",
			$"  \"closeKey\": \"{JsonSettingsUtils.EscapeJson(s.closeKey)}\",",
			$"  \"selectDistance\": {F(s.selectDistance)},",
			$"  \"hoverDistance\": {F(s.hoverDistance)},",
			$"  \"applyInterval\": {F(s.applyInterval)},",
			$"  \"rescanInterval\": {F(s.rescanInterval)},",
			$"  \"positionMatchEpsilon\": {F(s.positionMatchEpsilon)},",
			$"  \"areaApplyRadius\": {F(s.areaApplyRadius)},",
			$"  \"stylePreset\": \"{JsonSettingsUtils.EscapeJson(UiStylePresets.NormalizePresetId(s.stylePreset))}\",",
			$"  \"_stylePresetOptions\": {JsonSettingsUtils.BuildStringArray(StylePresetReference)},",
			$"  \"showHoverPrompt\": {s.showHoverPrompt.ToString().ToLowerInvariant()},",
			$"  \"hoverPromptPosition\": \"{JsonSettingsUtils.EscapeJson(s.hoverPromptPosition)}\",",
			$"  \"_hoverPromptPositionOptions\": {JsonSettingsUtils.BuildStringArray(HoverPromptPositionReference)},",
			$"  \"showBuildingInfoDetails\": {s.showBuildingInfoDetails.ToString().ToLowerInvariant()},",
			$"  \"rgbBorderEnabled\": {s.rgbBorderEnabled.ToString().ToLowerInvariant()},",
			$"  \"rgbBorderWidth\": {F(s.rgbBorderWidth)},",
			$"  \"rgbBorderSegment\": {F(s.rgbBorderSegment)},",
			$"  \"rgbBorderSpeed\": {F(s.rgbBorderSpeed)},",
			$"  \"groupCount\": {Mathf.Clamp(s.groupCount, MinGroupCount, MaxGroupCount)},",
			$"  \"presetCount\": {Mathf.Clamp(s.presetCount, MinPresetCount, MaxPresetCount)},",
			$"  \"groupNames\": {JsonSettingsUtils.BuildStringArray(s.groupNames)},",
			$"  \"groupNameA\": \"{JsonSettingsUtils.EscapeJson(s.groupNameA)}\",",
			$"  \"groupNameB\": \"{JsonSettingsUtils.EscapeJson(s.groupNameB)}\",",
			$"  \"groupNameC\": \"{JsonSettingsUtils.EscapeJson(s.groupNameC)}\",",
			$"  \"groupNameD\": \"{JsonSettingsUtils.EscapeJson(s.groupNameD)}\",",
			$"  \"debugLogging\": {s.debugLogging.ToString().ToLowerInvariant()},",
			$"  \"debugToasts\": {s.debugToasts.ToString().ToLowerInvariant()}",
			"}"
		};
		return string.Join(Environment.NewLine, lines);
	}

	private static HoverPromptPosition ParseHoverPromptPosition(string? text)
	{
		var value = (text ?? string.Empty).Trim().ToLowerInvariant();
		return value switch
		{
			"top" => HoverPromptPosition.Top,
			"bottom" => HoverPromptPosition.Bottom,
			_ => HoverPromptPosition.Center
		};
	}

	private static string GetHoverPromptPositionText(HoverPromptPosition position)
	{
		return position switch
		{
			HoverPromptPosition.Top => "top",
			HoverPromptPosition.Bottom => "bottom",
			_ => "center"
		};
	}

}

