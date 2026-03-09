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
	private void LoadPresetsFromDisk()
	{
		try
		{
			var targetCount = GetPresetVisibleCount();
			string presetsRaw = string.Empty;
			if (!string.IsNullOrWhiteSpace(_presetsFilePath) && System.IO.File.Exists(_presetsFilePath))
			{
				var json = System.IO.File.ReadAllText(_presetsFilePath);
				presetsRaw = ReadNestedRaw(json, "presets", '{', '}');
				if (!string.IsNullOrWhiteSpace(presetsRaw))
				{
					var maxLoadedSlot = 0;
					for (var i = 1; i <= MaxPresetCount; i++)
					{
						var raw = ReadNestedRaw(presetsRaw, $"slot{i}", '{', '}');
						if (string.IsNullOrWhiteSpace(raw)) continue;
						maxLoadedSlot = i;
					}
					targetCount = Mathf.Clamp(Mathf.Max(targetCount, maxLoadedSlot), MinPresetCount, MaxPresetCount);
				}
			}

			targetCount = Mathf.Clamp(targetCount, MinPresetCount, MaxPresetCount);
			_presets.Clear();
			for (var i = 0; i < targetCount; i++)
			{
				_presets.Add(new PresetEntry
				{
					name = BuildDefaultPresetName(i)
				});
			}
			for (var i = 0; i < _presets.Count; i++)
			{
				var slotRaw = string.IsNullOrWhiteSpace(presetsRaw)
					? string.Empty
					: ReadNestedRaw(presetsRaw, $"slot{i + 1}", '{', '}');
				if (!string.IsNullOrWhiteSpace(slotRaw))
				{
					_presets[i] = ParsePreset(slotRaw, i);
				}
				if (string.IsNullOrWhiteSpace(_presets[i].name))
				{
					_presets[i].name = BuildDefaultPresetName(i);
				}
			}
		}
		catch (Exception ex)
		{
			Logger.LogWarning($"Failed to load presets: {ex.Message}");
		}
	}

	private void SavePresetsToDisk()
	{
		try
		{
			if (string.IsNullOrWhiteSpace(_presetsFilePath))
			{
				TryResolveSavePaths(createFolderIfMissing: true);
			}
			if (string.IsNullOrWhiteSpace(_presetsFilePath)) return;
			var dir = System.IO.Path.GetDirectoryName(_presetsFilePath);
			if (!string.IsNullOrWhiteSpace(dir)) System.IO.Directory.CreateDirectory(dir);

			var lines = new List<string>
			{
				"{",
				"  \"presets\": {"
			};
			for (var i = 0; i < _presets.Count; i++)
			{
				var comma = i < _presets.Count - 1 ? "," : string.Empty;
				lines.Add($"    \"slot{i + 1}\": {BuildPresetJson(_presets[i])}{comma}");
			}
			lines.Add("  }");
			lines.Add("}");
			System.IO.File.WriteAllText(_presetsFilePath, string.Join(Environment.NewLine, lines));
		}
		catch (Exception ex)
		{
			Logger.LogWarning($"Failed to save presets: {ex.Message}");
		}
	}

	private static string BuildPresetJson(PresetEntry p)
	{
		var presetName = string.IsNullOrWhiteSpace(p.name) ? "Preset" : p.name.Trim();
		return $"{{ \"name\": \"{JsonSettingsUtils.EscapeJson(presetName)}\", \"hasValue\": {p.hasValue.ToString().ToLowerInvariant()}, \"overrideOutputs\": {p.overrideOutputs.ToString().ToLowerInvariant()}, \"spawnRate\": {F(p.spawnRate)}, \"spawnProbability\": {F(p.spawnProbability)}, \"allowGems\": {p.allowGems.ToString().ToLowerInvariant()}, \"polishedGems\": {p.polishedGems.ToString().ToLowerInvariant()}, \"allowGeodes\": {p.allowGeodes.ToString().ToLowerInvariant()}, \"polishedGeodes\": {p.polishedGeodes.ToString().ToLowerInvariant()}, \"group\": {p.group}, \"slots\": {BuildSlotsJson(p.slots)} }}";
	}

	private static PresetEntry ParsePreset(string raw, int index)
	{
		var p = new PresetEntry();
		p.name = JsonSettingsUtils.ReadString(raw, "name", BuildDefaultPresetName(index)).Trim();
		p.hasValue = JsonSettingsUtils.ReadBool(raw, "hasValue", false);
		p.overrideOutputs = JsonSettingsUtils.ReadBool(raw, "overrideOutputs", false);
		p.spawnRate = Mathf.Clamp(JsonSettingsUtils.ReadFloat(raw, "spawnRate", 2f), 0.01f, 20f);
		p.spawnProbability = Mathf.Clamp(JsonSettingsUtils.ReadFloat(raw, "spawnProbability", 80f), 0f, 100f);
		p.allowGems = JsonSettingsUtils.ReadBool(raw, "allowGems", true);
		p.polishedGems = JsonSettingsUtils.ReadBool(raw, "polishedGems", false);
		p.allowGeodes = JsonSettingsUtils.ReadBool(raw, "allowGeodes", true);
		p.polishedGeodes = JsonSettingsUtils.ReadBool(raw, "polishedGeodes", false);
		p.group = Mathf.Clamp(JsonSettingsUtils.ReadInt(raw, "group", 0), 0, MaxGroupCount);
		var slotsRaw = ReadNestedRaw(raw, "slots", '[', ']');
		p.slots = ParseSlots(slotsRaw);
		return p;
	}

}

