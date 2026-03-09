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
	private static readonly BindingFlags AnyInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
	private object? _activeSpawnContextMiner;

	private sealed class MinerInstanceState : MonoBehaviour
	{
		public bool hasConfig;
		public MinerConfigEntry config = new();
		public UnityEngine.Object? originalDefinition;
		public UnityEngine.Object? customDefinition;
		public string lastDefinitionSignature = string.Empty;
		public string lastNoValidSlotsSignature = string.Empty;
	}

	private void RefreshMinerCache()
	{
		_minerCache.Clear();
		var all = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
		var seenIds = new HashSet<int>();
		for (var i = 0; i < all.Length; i++)
		{
			var mb = all[i];
			if (mb == null) continue;
			if (!IsAutoMinerType(mb.GetType())) continue;
			var id = mb.GetInstanceID();
			seenIds.Add(id);
			var existing = ReadConfigForMiner(mb);
			var state = new MinerRuntimeState
			{
				miner = mb,
				instanceId = id,
				position = mb.transform.position,
				hasCustomConfig = existing != null,
				config = CloneConfig(existing ?? BuildDefaultConfigFromMiner(mb))
			};
			_minerCache[id] = state;
			if (state.hasCustomConfig && state.config.overrideOutputs)
			{
				_activeConfigByMinerInstance[id] = CloneConfig(state.config);
			}
		}

		var stale = _activeConfigByMinerInstance.Keys.Where(id => !seenIds.Contains(id)).ToArray();
		for (var i = 0; i < stale.Length; i++)
		{
			_activeConfigByMinerInstance.Remove(stale[i]);
		}

		var staleTickKeys = _nextMinerTickReapplyById.Keys.Where(id => !seenIds.Contains(id)).ToArray();
		for (var i = 0; i < staleTickKeys.Length; i++)
		{
			_nextMinerTickReapplyById.Remove(staleTickKeys[i]);
		}
	}

	private void ApplyAllSavedConfigs()
	{
		if (_minerCache.Count == 0) return;
		var stale = new List<int>();
		foreach (var kv in _minerCache)
		{
			if (!kv.Value.hasCustomConfig) continue;
			if (!IsObjectAlive(kv.Value.miner))
			{
				stale.Add(kv.Key);
				continue;
			}
			try
			{
				ApplyConfigToMiner(kv.Value.miner, kv.Value.config);
			}
			catch (Exception ex)
			{
				Logger.LogWarning($"[ConfigurableMiners] ApplyAllSavedConfigs skipped miner {kv.Key}: {ex.Message}");
				stale.Add(kv.Key);
			}
		}
		for (var i = 0; i < stale.Count; i++)
		{
			_minerCache.Remove(stale[i]);
			_activeConfigByMinerInstance.Remove(stale[i]);
		}
	}

	private void ApplyConfigToMiner(object miner, MinerConfigEntry config)
	{
		if (!IsObjectAlive(miner)) return;
		_debugAppliedConfigsThisFrame++;
		TrySetFloat(miner, "SpawnRate", Mathf.Clamp(config.spawnRate, 0.01f, 20f));
		TrySetFloat(miner, "SpawnProbability", Mathf.Clamp(config.spawnProbability, 0f, 100f));
		TrySetBool(miner, "CanProduceGems", config.allowGems);
		ApplyCustomDefinitionIfNeeded(miner, config);
	}

	private void ApplySelectedConfig()
	{
		if (_selectedMiner == null) return;
		LogDebug("ApplySelectedConfig: begin");
		var pos = ReadMinerPosition(_selectedMiner);
		var existing = FindSavedEntryForPosition(pos);
		if (existing == null)
		{
			var entry = CloneConfig(_edit);
			entry.x = pos.x;
			entry.y = pos.y;
			entry.z = pos.z;
			_savedEntries.Add(entry);
		}
		else
		{
			CopyConfigInto(existing, _edit);
			existing.x = pos.x;
			existing.y = pos.y;
			existing.z = pos.z;
		}

		ApplyConfigToMiner(_selectedMiner, _edit);
		if (_selectedMiner is MonoBehaviour selectedMb)
		{
			if (_edit.overrideOutputs) _activeConfigByMinerInstance[selectedMb.GetInstanceID()] = CloneConfig(_edit);
			else _activeConfigByMinerInstance.Remove(selectedMb.GetInstanceID());
			SetStateConfig(selectedMb, _edit);
		}
		SaveSaveDataToDisk();
		PushToast("Miner settings applied", ToastType.Success);
		LogDebug($"ApplySelectedConfig: applied override={_edit.overrideOutputs} rate={_edit.spawnRate:0.###} prob={_edit.spawnProbability:0.###} allowGems={_edit.allowGems}");
		_editDirty = false;
		_nextApplyTime = 0f;
	}

	private void CopySelectedConfigToClipboard()
	{
		_clipboardConfig = CloneConfig(_edit);
		PushToast("Copied miner config", ToastType.Success);
	}

	private void PasteClipboardToSelectedConfig()
	{
		if (_clipboardConfig == null)
		{
			PushToast("Clipboard is empty", ToastType.Warning);
			return;
		}

		_edit = CloneConfig(_clipboardConfig);
		RefreshOutputOptionsForCurrentFilter();
		_editDirty = true;
		PushToast("Pasted config to current miner editor", ToastType.Info);
	}

	private void ApplyCurrentConfigAroundPlayer()
	{
		if (!TryGetPlayerPosition(out var playerPos))
		{
			PushToast("Player position not found", ToastType.Warning);
			return;
		}

		var source = CloneConfig(_edit);
		var applied = 0;
		foreach (var kv in _minerCache)
		{
			if (kv.Value.miner is not MonoBehaviour mb || mb == null) continue;
			if (Vector3.Distance(mb.transform.position, playerPos) > _areaApplyRadius) continue;

			var cfg = CloneConfig(source);
			var pos = mb.transform.position;
			cfg.x = pos.x;
			cfg.y = pos.y;
			cfg.z = pos.z;

			var existing = FindSavedEntryForPosition(pos);
			if (existing == null)
			{
				_savedEntries.Add(cfg);
			}
			else
			{
				CopyConfigInto(existing, cfg);
				existing.x = pos.x;
				existing.y = pos.y;
				existing.z = pos.z;
			}

			ApplyConfigToMiner(mb, cfg);
			if (cfg.overrideOutputs) _activeConfigByMinerInstance[mb.GetInstanceID()] = CloneConfig(cfg);
			else _activeConfigByMinerInstance.Remove(mb.GetInstanceID());
			SetStateConfig(mb, cfg);
			applied++;
		}

		if (applied <= 0)
		{
			PushToast("No miners found in radius", ToastType.Warning);
			return;
		}

		SaveSaveDataToDisk();
		_nextApplyTime = 0f;
		PushToast($"Applied to {applied} miner(s) within {_areaApplyRadius:0.#}m", ToastType.Success);
	}

	private void ApplyCurrentConfigToGroup()
	{
		var targetGroup = Mathf.Clamp(_edit.group, 0, _groupCount);
		var source = CloneConfig(_edit);
		var applied = 0;
		foreach (var kv in _minerCache)
		{
			if (kv.Value.miner is not MonoBehaviour mb || mb == null) continue;

			MinerConfigEntry? current = ReadConfigForMiner(mb);
			current ??= FindSavedEntryForPosition(mb.transform.position);
			var isSelectedMiner = _selectedMiner is MonoBehaviour selectedMb && ReferenceEquals(selectedMb, mb);
			var currentGroup = current?.group ?? (isSelectedMiner ? targetGroup : 0);
			if (currentGroup != targetGroup) continue;

			var cfg = CloneConfig(source);
			var pos = mb.transform.position;
			cfg.x = pos.x;
			cfg.y = pos.y;
			cfg.z = pos.z;

			var existing = FindSavedEntryForPosition(pos);
			if (existing == null)
			{
				_savedEntries.Add(cfg);
			}
			else
			{
				CopyConfigInto(existing, cfg);
				existing.x = pos.x;
				existing.y = pos.y;
				existing.z = pos.z;
			}

			ApplyConfigToMiner(mb, cfg);
			if (cfg.overrideOutputs) _activeConfigByMinerInstance[mb.GetInstanceID()] = CloneConfig(cfg);
			else _activeConfigByMinerInstance.Remove(mb.GetInstanceID());
			SetStateConfig(mb, cfg);
			applied++;
		}

		if (applied <= 0)
		{
			PushToast($"No miners found in {GetGroupDisplayName(targetGroup)}", ToastType.Warning);
			return;
		}

		SaveSaveDataToDisk();
		_nextApplyTime = 0f;
		PushToast($"Applied to {applied} miner(s) in {GetGroupDisplayName(targetGroup)}", ToastType.Success);
	}

	private static bool TryGetPlayerPosition(out Vector3 playerPos)
	{
		playerPos = Vector3.zero;
		var player = GameObject.FindWithTag("Player");
		if (player != null)
		{
			playerPos = player.transform.position;
			return true;
		}

		if (Camera.main != null)
		{
			playerPos = Camera.main.transform.position;
			return true;
		}

		return false;
	}

	private void RevertSelectedMiner()
	{
		if (_selectedMiner == null) return;
		var pos = ReadMinerPosition(_selectedMiner);
		if (_selectedMiner is MonoBehaviour selectedMb)
		{
			RestoreMinerFromOriginalDefinition(selectedMb);
		}
		for (var i = _savedEntries.Count - 1; i >= 0; i--)
		{
			if (Vector3.Distance(new Vector3(_savedEntries[i].x, _savedEntries[i].y, _savedEntries[i].z), pos) <= _positionMatchEpsilon)
			{
				_savedEntries.RemoveAt(i);
			}
		}
		_edit = BuildDefaultConfigFromMiner(_selectedMiner);
		ApplyConfigToMiner(_selectedMiner, _edit);
		if (_selectedMiner is MonoBehaviour selectedMbAfterApply)
		{
			_activeConfigByMinerInstance.Remove(selectedMbAfterApply.GetInstanceID());
			ClearStateConfig(selectedMbAfterApply);
		}
		SaveSaveDataToDisk();
		PushToast("Miner reverted to default behavior", ToastType.Warning);
		_editDirty = false;
	}

	private void RestoreMinerFromOriginalDefinition(MonoBehaviour miner)
	{
		var state = miner.GetComponent<MinerInstanceState>();
		if (state == null || state.originalDefinition == null) return;
		var resourceDefField = miner.GetType().GetField("ResourceDefinition", AnyInstance);
		if (resourceDefField == null) return;

		resourceDefField.SetValue(miner, state.originalDefinition);
		var configureMethod = miner.GetType().GetMethod("ConfigureFromDefinition", AnyInstance);
		if (configureMethod == null) return;

		var id = miner.GetInstanceID();
		var added = _configureInvokeGuard.Add(id);
		try
		{
			configureMethod.Invoke(miner, null);
		}
		catch
		{
			// If configure invoke fails, keep revert flow going and fallback to runtime field reset path.
		}
		finally
		{
			if (added) _configureInvokeGuard.Remove(id);
		}
	}

	private void PruneStaleSavedEntries()
	{
		if (_savedEntries.Count == 0)
		{
			_staleSavedEntryFirstMissingAt.Clear();
			return;
		}
		const float staleGraceSeconds = 5f;
		var now = Time.time;
		var removed = 0;
		for (var i = _savedEntries.Count - 1; i >= 0; i--)
		{
			var entry = _savedEntries[i];
			var p = new Vector3(entry.x, entry.y, entry.z);
			var key = BuildSavedEntryPositionKey(entry);
			var found = _minerCache.Values.Any(m => Vector3.Distance(m.position, p) <= _positionMatchEpsilon);
			if (found)
			{
				_staleSavedEntryFirstMissingAt.Remove(key);
				continue;
			}

			if (!_staleSavedEntryFirstMissingAt.TryGetValue(key, out var firstMissingAt))
			{
				_staleSavedEntryFirstMissingAt[key] = now;
				continue;
			}

			if (now - firstMissingAt < staleGraceSeconds) continue;
			_staleSavedEntryFirstMissingAt.Remove(key);
			_savedEntries.RemoveAt(i);
			removed++;
		}

		if (_staleSavedEntryFirstMissingAt.Count > 0)
		{
			var validKeys = new HashSet<string>(_savedEntries.Select(BuildSavedEntryPositionKey));
			var staleKeys = _staleSavedEntryFirstMissingAt.Keys.Where(k => !validKeys.Contains(k)).ToArray();
			for (var i = 0; i < staleKeys.Length; i++)
			{
				_staleSavedEntryFirstMissingAt.Remove(staleKeys[i]);
			}
		}

		if (removed > 0)
		{
			SaveSaveDataToDisk();
			LogDebug($"Pruned {removed} stale miner entries.");
		}
	}

	private static string BuildSavedEntryPositionKey(MinerConfigEntry entry)
	{
		return $"{entry.x:0.###}|{entry.y:0.###}|{entry.z:0.###}";
	}

	private MinerConfigEntry? FindSavedEntryForPosition(Vector3 pos)
	{
		for (var i = 0; i < _savedEntries.Count; i++)
		{
			var p = new Vector3(_savedEntries[i].x, _savedEntries[i].y, _savedEntries[i].z);
			if (Vector3.Distance(p, pos) <= _positionMatchEpsilon)
			{
				return _savedEntries[i];
			}
		}
		return null;
	}

	private MinerConfigEntry BuildDefaultConfigFromMiner(object miner)
	{
		return new MinerConfigEntry
		{
			overrideOutputs = false,
			spawnRate = Mathf.Clamp(ReadFloat(miner, "SpawnRate", 2f), 0.01f, 20f),
			spawnProbability = Mathf.Clamp(ReadFloat(miner, "SpawnProbability", 80f), 0f, 100f),
			allowGems = ReadBool(miner, "CanProduceGems", true),
			polishedGems = false,
			allowGeodes = true,
			polishedGeodes = false,
			group = (int)MinerGroup.None,
			slots = CreateDefaultSlots()
		};
	}

	private static MinerConfigEntry CloneConfig(MinerConfigEntry source)
	{
		var clone = new MinerConfigEntry
		{
			x = source.x,
			y = source.y,
			z = source.z,
			overrideOutputs = source.overrideOutputs,
			spawnRate = source.spawnRate,
			spawnProbability = source.spawnProbability,
			allowGems = source.allowGems,
			polishedGems = source.polishedGems,
			allowGeodes = source.allowGeodes,
			polishedGeodes = source.polishedGeodes,
			group = source.group,
			slots = CreateDefaultSlots()
		};
		for (var i = 0; i < clone.slots.Length && i < source.slots.Length; i++)
		{
			clone.slots[i].enabled = source.slots[i].enabled;
			clone.slots[i].resourceType = source.slots[i].resourceType;
			clone.slots[i].pieceType = source.slots[i].pieceType;
			clone.slots[i].polished = source.slots[i].polished;
			clone.slots[i].locked = source.slots[i].locked;
			clone.slots[i].percent = source.slots[i].percent;
		}
		return clone;
	}

	private static void CopyConfigInto(MinerConfigEntry target, MinerConfigEntry source)
	{
		target.overrideOutputs = source.overrideOutputs;
		target.spawnRate = source.spawnRate;
		target.spawnProbability = source.spawnProbability;
		target.allowGems = source.allowGems;
		target.polishedGems = source.polishedGems;
		target.allowGeodes = source.allowGeodes;
		target.polishedGeodes = source.polishedGeodes;
		target.group = source.group;
		if (target.slots == null || target.slots.Length != 4) target.slots = CreateDefaultSlots();
		for (var i = 0; i < target.slots.Length && i < source.slots.Length; i++)
		{
			target.slots[i].enabled = source.slots[i].enabled;
			target.slots[i].resourceType = source.slots[i].resourceType;
			target.slots[i].pieceType = source.slots[i].pieceType;
			target.slots[i].polished = source.slots[i].polished;
			target.slots[i].locked = source.slots[i].locked;
			target.slots[i].percent = source.slots[i].percent;
		}
	}

	private bool IsSaveSystemLoading()
	{
		var manager = GetSingletonInstance("SavingLoadingManager");
		if (manager == null) return false;

		if (!ReferenceEquals(manager, _cachedSavingLoadingManager))
		{
			_cachedSavingLoadingManager = manager;
			var type = manager.GetType();
			_cachedIsCurrentlyLoadingGameProperty = type.GetProperty("IsCurrentlyLoadingGame", AnyInstance);
			_cachedIsCurrentlyLoadingGameField = type.GetField("IsCurrentlyLoadingGame", AnyInstance);
		}

		try
		{
			if (_cachedIsCurrentlyLoadingGameProperty != null && _cachedIsCurrentlyLoadingGameProperty.CanRead)
			{
				return (bool?)_cachedIsCurrentlyLoadingGameProperty.GetValue(manager, null) ?? false;
			}

			if (_cachedIsCurrentlyLoadingGameField != null)
			{
				return (bool?)_cachedIsCurrentlyLoadingGameField.GetValue(manager) ?? false;
			}
		}
		catch
		{
			return false;
		}

		return false;
	}

	internal string DecorateBuildingInfoText(string original)
	{
		if (!_showBuildingInfoDetails) return original;
		if (!TrySelectMinerFromLook(out var miner) || miner == null) return original;
		var entry = FindSavedEntryForPosition(ReadMinerPosition(miner));
		if (entry == null) return original;

		var lines = new List<string>
		{
			original,
			"",
			"<size=110%><b>Configurable Miners</b></size>",
			$"Spawn Rate: {entry.spawnRate:0.###}",
			$"Spawn Probability: {entry.spawnProbability:0.#}%",
			$"Allow Gems: {(entry.allowGems ? "Yes" : "No")}",
			$"Polished Gems: {(entry.polishedGems ? "Yes" : "No")}",
			$"Allow Geodes: {(entry.allowGeodes ? "Yes" : "No")}",
			$"Polished Geodes: {(entry.polishedGeodes ? "Yes" : "No")}",
			$"Group: {GetGroupDisplayName(entry.group)}",
			$"Override Outputs: {(entry.overrideOutputs ? "Yes" : "No")}" 
		};
		if (entry.overrideOutputs)
		{
			for (var i = 0; i < entry.slots.Length; i++)
			{
				var slot = entry.slots[i];
				if (!slot.enabled || string.IsNullOrWhiteSpace(slot.resourceType)) continue;
				lines.Add($"S{i + 1}: {slot.resourceType} {slot.pieceType} ({slot.percent:0.#}%)");
			}
		}
		return string.Join("\n", lines);
	}

	private static Vector3 ReadMinerPosition(object miner)
	{
		if (miner is MonoBehaviour mb) return mb.transform.position;
		return Vector3.zero;
	}

	private static float ReadFloat(object source, string name, float fallback)
	{
		var v = TryGetFieldValue(source, name);
		if (v is float f) return f;
		if (v is int i) return i;
		return fallback;
	}

	private static float ReadFloatFromObject(object source, string name, float fallback)
	{
		return ReadFloat(source, name, fallback);
	}

	private static bool ReadBool(object source, string name, bool fallback)
	{
		var v = TryGetFieldValue(source, name);
		if (v is bool b) return b;
		return fallback;
	}

	private static void TrySetFloat(object source, string name, float value)
	{
		var type = source.GetType();
		var field = type.GetField(name, AnyInstance);
		if (field != null && field.FieldType == typeof(float))
		{
			field.SetValue(source, value);
			return;
		}
		var prop = type.GetProperty(name, AnyInstance);
		if (prop != null && prop.CanWrite && prop.PropertyType == typeof(float))
		{
			prop.SetValue(source, value, null);
		}
	}

	private static void TrySetBool(object source, string name, bool value)
	{
		var type = source.GetType();
		var field = type.GetField(name, AnyInstance);
		if (field != null && field.FieldType == typeof(bool))
		{
			field.SetValue(source, value);
			return;
		}
		var prop = type.GetProperty(name, AnyInstance);
		if (prop != null && prop.CanWrite && prop.PropertyType == typeof(bool))
		{
			prop.SetValue(source, value, null);
		}
	}

	private static object? TryGetFieldValue(object source, string name)
	{
		var type = source.GetType();
		var field = type.GetField(name, AnyInstance);
		if (field != null) return field.GetValue(source);
		var prop = type.GetProperty(name, AnyInstance);
		if (prop != null && prop.CanRead) return prop.GetValue(source, null);
		return null;
	}

	private static Type? FindGameTypeByName(string fullName)
	{
		return GameReflection.FindType(fullName);
	}

	private static object? GetSingletonInstance(string typeName)
	{
		return GameReflection.TryGetSingletonInstance(typeName);
	}

	private static bool IsObjectAlive(object? obj)
	{
		if (obj == null) return false;
		if (obj is UnityEngine.Object unityObj) return unityObj != null;
		return true;
	}

	private void LogDebug(string message)
	{
		if (!_debugLogging) return;
		var line = $"[{Time.frameCount}] {message}";
		Logger.LogInfo($"[ConfigurableMiners] {line}");
		_debugOverlayLines.Add(line);
		if (_debugOverlayLines.Count > 22) _debugOverlayLines.RemoveAt(0);
		PushDebugToast(message);
	}

	internal void TracePatch(string message)
	{
		LogDebug($"PATCH: {message}");
	}
}
