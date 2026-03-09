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
	private void LoadSaveDataFromDisk()
	{
		try
		{
			_savedEntries.Clear();
			_staleSavedEntryFirstMissingAt.Clear();
			if (string.IsNullOrWhiteSpace(_saveDataFilePath)) return;
			if (!System.IO.File.Exists(_saveDataFilePath)) return;
			var json = System.IO.File.ReadAllText(_saveDataFilePath);
			var minersRaw = ReadNestedRaw(json, "miners", '[', ']');
			if (string.IsNullOrWhiteSpace(minersRaw)) return;
			var objects = SplitTopLevelObjects(minersRaw);
			for (var i = 0; i < objects.Count; i++)
			{
				var raw = objects[i];
				var x = JsonSettingsUtils.ReadFloat(raw, "x", float.NaN);
				var y = JsonSettingsUtils.ReadFloat(raw, "y", float.NaN);
				var z = JsonSettingsUtils.ReadFloat(raw, "z", float.NaN);
				if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(z)) continue;
				var rate = Mathf.Clamp(JsonSettingsUtils.ReadFloat(raw, "spawnRate", 2f), 0.01f, 20f);
				var prob = Mathf.Clamp(JsonSettingsUtils.ReadFloat(raw, "spawnProbability", 80f), 0f, 100f);
				var overrideOutputs = JsonSettingsUtils.ReadBool(raw, "overrideOutputs", false);
				var allowGems = JsonSettingsUtils.ReadBool(raw, "allowGems", true);
				var polishedGems = JsonSettingsUtils.ReadBool(raw, "polishedGems", false);
				var allowGeodes = JsonSettingsUtils.ReadBool(raw, "allowGeodes", true);
				var polishedGeodes = JsonSettingsUtils.ReadBool(raw, "polishedGeodes", false);
				var group = Mathf.Clamp(JsonSettingsUtils.ReadInt(raw, "group", 0), 0, Mathf.Max(0, _groupCount));
				var slotsRaw = ReadNestedRaw(raw, "slots", '[', ']');

				var entry = new MinerConfigEntry
				{
					x = x,
					y = y,
					z = z,
					overrideOutputs = overrideOutputs,
					spawnRate = rate,
					spawnProbability = prob,
					allowGems = allowGems,
					polishedGems = polishedGems,
					allowGeodes = allowGeodes,
					polishedGeodes = polishedGeodes,
					group = group,
					slots = ParseSlots(slotsRaw)
				};
				_savedEntries.Add(entry);
			}
		}
		catch (Exception ex)
		{
			Logger.LogWarning($"Failed to load save data: {ex.Message}");
		}
	}

	private void SaveSaveDataToDisk()
	{
		try
		{
			if (string.IsNullOrWhiteSpace(_saveDataFilePath))
			{
				TryResolveSavePaths(createFolderIfMissing: true);
			}
			if (string.IsNullOrWhiteSpace(_saveDataFilePath)) return;
			var dir = System.IO.Path.GetDirectoryName(_saveDataFilePath);
			if (!string.IsNullOrWhiteSpace(dir)) System.IO.Directory.CreateDirectory(dir);

			var lines = new List<string>
			{
				"{",
				"  \"miners\": ["
			};
			for (var i = 0; i < _savedEntries.Count; i++)
			{
				var e = _savedEntries[i];
				var comma = i < _savedEntries.Count - 1 ? "," : string.Empty;
				lines.Add($"    {{ \"x\": {F(e.x)}, \"y\": {F(e.y)}, \"z\": {F(e.z)}, \"overrideOutputs\": {e.overrideOutputs.ToString().ToLowerInvariant()}, \"spawnRate\": {F(e.spawnRate)}, \"spawnProbability\": {F(e.spawnProbability)}, \"allowGems\": {e.allowGems.ToString().ToLowerInvariant()}, \"polishedGems\": {e.polishedGems.ToString().ToLowerInvariant()}, \"allowGeodes\": {e.allowGeodes.ToString().ToLowerInvariant()}, \"polishedGeodes\": {e.polishedGeodes.ToString().ToLowerInvariant()}, \"group\": {e.group}, \"slots\": {BuildSlotsJson(e.slots)} }}{comma}");
			}
			lines.Add("  ]");
			lines.Add("}");
			System.IO.File.WriteAllText(_saveDataFilePath, string.Join(Environment.NewLine, lines));
		}
		catch (Exception ex)
		{
			Logger.LogWarning($"Failed to save save-scoped miner data: {ex.Message}");
		}
	}

	private static MinerSlotConfig[] ParseSlots(string raw)
	{
		var slots = CreateDefaultSlots();
		var matches = System.Text.RegularExpressions.Regex.Matches(raw ?? string.Empty, "\\{(.*?)\\}", System.Text.RegularExpressions.RegexOptions.Singleline);
		for (var i = 0; i < slots.Length && i < matches.Count; i++)
		{
			var inner = matches[i].Groups[1].Value;
			slots[i].enabled = JsonSettingsUtils.ReadBool(inner, "enabled", false);
			slots[i].resourceType = JsonSettingsUtils.ReadString(inner, "resourceType", string.Empty);
			slots[i].pieceType = JsonSettingsUtils.ReadString(inner, "pieceType", string.Empty);
			slots[i].polished = JsonSettingsUtils.ReadBool(inner, "polished", false);
			slots[i].locked = JsonSettingsUtils.ReadBool(inner, "locked", false);
			slots[i].percent = Mathf.Clamp(JsonSettingsUtils.ReadFloat(inner, "percent", 0f), 0f, 100f);
		}
		return slots;
	}

	private static string BuildSlotsJson(MinerSlotConfig[] slots)
	{
		var lines = new List<string> { "[" };
		for (var i = 0; i < slots.Length; i++)
		{
			var s = slots[i];
			var comma = i < slots.Length - 1 ? "," : string.Empty;
			lines.Add($"{{ \"enabled\": {s.enabled.ToString().ToLowerInvariant()}, \"resourceType\": \"{JsonSettingsUtils.EscapeJson(s.resourceType)}\", \"pieceType\": \"{JsonSettingsUtils.EscapeJson(s.pieceType)}\", \"polished\": {s.polished.ToString().ToLowerInvariant()}, \"locked\": {s.locked.ToString().ToLowerInvariant()}, \"percent\": {F(s.percent)} }}{comma}");
		}
		lines.Add("]");
		return string.Join(" ", lines);
	}

}

