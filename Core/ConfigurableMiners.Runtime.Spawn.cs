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
	internal void OnMinerConfiguredByGame(object miner)
	{
		if (!IsObjectAlive(miner)) return;
		if (miner is MonoBehaviour guardedMb && _configureInvokeGuard.Contains(guardedMb.GetInstanceID()))
		{
			return;
		}
		var entry = TryGetConfigForMiner(miner);
		if (entry == null) return;
		ApplyConfigToMiner(miner, entry);
	}

	internal void OnMinerUpdateTick(object miner)
	{
		if (_savedEntries.Count == 0 && _activeConfigByMinerInstance.Count == 0) return;
		if (!IsObjectAlive(miner)) return;
		if (miner is MonoBehaviour minerMb)
		{
			var id = minerMb.GetInstanceID();
			if (_nextMinerTickReapplyById.TryGetValue(id, out var nextTime) && Time.time < nextTime) return;
			_nextMinerTickReapplyById[id] = Time.time + Mathf.Max(0.1f, _updateTickReapplyInterval);
		}

		var entry = TryGetConfigForMiner(miner);
		if (entry == null) return;
		TrySetFloat(miner, "SpawnRate", Mathf.Clamp(entry.spawnRate, 0.01f, 20f));
		TrySetFloat(miner, "SpawnProbability", Mathf.Clamp(entry.spawnProbability, 0f, 100f));
		TrySetBool(miner, "CanProduceGems", entry.allowGems);
	}

	internal bool TryHandleCustomSpawn(object miner)
	{
		var entry = TryGetConfigForMiner(miner);
		if (entry == null)
		{
			LogDebug("TryHandleCustomSpawn: no config for miner, let vanilla run.");
			return false;
		}
		if (!entry.overrideOutputs)
		{
			LogDebug("TryHandleCustomSpawn: override disabled, let vanilla run.");
			return false;
		}

		var chance = Mathf.Clamp(entry.spawnProbability, 0f, 100f);
		var chanceRoll = UnityEngine.Random.Range(0f, 100f);
		LogDebug($"TryHandleCustomSpawn: chance roll={chanceRoll:0.###} threshold={chance:0.###}");
		if (chanceRoll > chance)
		{
			LogDebug("TryHandleCustomSpawn: skipped spawn by probability.");
			return true;
		}

		if (!TryIsOreSpawningBlocked(out var oreSpawningBlocked))
		{
			// If we can't evaluate the vanilla ore limit gate, defer to vanilla logic.
			LogDebug("TryHandleCustomSpawn: unable to read OreLimitManager gate, falling back to vanilla.");
			return false;
		}

		if (oreSpawningBlocked)
		{
			LogDebug("TryHandleCustomSpawn: blocked by OreLimitManager.");
			return true;
		}

		var slot = PickWeightedSlot(entry);
		if (slot == null)
		{
			LogDebug("TryHandleCustomSpawn: no eligible slot selected.");
			return true;
		}
		LogDebug($"TryHandleCustomSpawn: selected slot resource={slot.resourceType} piece={slot.pieceType} polished={slot.polished} pct={slot.percent:0.###}");

		var orePrefab = ResolveOrePrefabForSlot(slot);
		if (orePrefab == null)
		{
			var fallback = TryGetFieldValue(miner, "FallbackOrePrefab");
			orePrefab = fallback;
			LogDebug("TryHandleCustomSpawn: selected prefab not found, using miner fallback prefab.");
		}

		if (orePrefab == null)
		{
			LogDebug("TryHandleCustomSpawn: no ore prefab available, suppressing spawn.");
			return true;
		}

		var spawnPoint = TryGetFieldValue(miner, "OreSpawnPoint") as Transform;
		if (spawnPoint == null)
		{
			LogDebug("TryHandleCustomSpawn: miner has no OreSpawnPoint.");
			return true;
		}

		var pool = GetSingletonInstance("OrePiecePoolManager");
		if (pool == null)
		{
			LogDebug("TryHandleCustomSpawn: OrePiecePoolManager instance missing.");
			return true;
		}

		var resourceType = TryGetFieldValue(orePrefab, "ResourceType");
		var pieceType = TryGetFieldValue(orePrefab, "PieceType");
		var isPolished = ReadBool(orePrefab, "IsPolished", false) || ReadFloatFromObject(orePrefab, "PolishedPercent", 0f) >= 0.95f;
		if (resourceType == null || pieceType == null)
		{
			Logger.LogWarning("[ConfigurableMiners] Failed to read selected ore prefab ResourceType/PieceType. Falling back to original miner spawn.");
			LogDebug("TryHandleCustomSpawn: selected prefab missing ResourceType/PieceType.");
			return false;
		}

		var spawnMethod = GetCachedPoolSpawnMethod(pool);
		if (spawnMethod == null)
		{
			Logger.LogWarning("[ConfigurableMiners] Could not find OrePiecePoolManager SpawnPooledOre(ResourceType, PieceType, bool, Vector3, Quaternion, Transform).");
			LogDebug("TryHandleCustomSpawn: target SpawnPooledOre method not found.");
			return false;
		}

		if (TryBuildEnumSpawnArgs(spawnMethod, slot, out var enumResourceType, out var enumPieceType))
		{
			spawnMethod.Invoke(pool, new object?[] { enumResourceType, enumPieceType, slot.polished, spawnPoint.position, spawnPoint.rotation, null });
			LogDebug($"TryHandleCustomSpawn: spawned override output resource={slot.resourceType} piece={slot.pieceType} polished={slot.polished}.");
			return true;
		}

		spawnMethod.Invoke(pool, new object?[] { resourceType, pieceType, isPolished, spawnPoint.position, spawnPoint.rotation, null });
		LogDebug($"TryHandleCustomSpawn: spawned override output resource={resourceType} piece={pieceType} polished={isPolished}.");
		return true;
	}

	internal bool ShouldInterceptTrySpawn(object miner)
	{
		if (_savedEntries.Count == 0 && _activeConfigByMinerInstance.Count == 0) return false;
		if (!IsObjectAlive(miner)) return false;
		if (miner is MonoBehaviour mb)
		{
			var id = mb.GetInstanceID();
			if (_activeConfigByMinerInstance.TryGetValue(id, out var cached))
			{
				return cached.overrideOutputs;
			}

			var state = mb.GetComponent<MinerInstanceState>();
			if (state != null && state.hasConfig)
			{
				return state.config.overrideOutputs;
			}

			var saved = FindSavedEntryForPosition(mb.transform.position);
			if (saved == null || !saved.overrideOutputs) return false;
			_activeConfigByMinerInstance[id] = CloneConfig(saved);
			SetStateConfig(mb, saved);
			return true;
		}

		var entry = TryGetConfigForMiner(miner);
		return entry != null && entry.overrideOutputs;
	}

	internal void BeginMinerSpawnContext(object miner)
	{
		_activeSpawnContextMiner = miner;
	}

	internal void EndMinerSpawnContext()
	{
		_activeSpawnContextMiner = null;
	}

	internal bool HasActiveSpawnContext()
	{
		return _activeSpawnContextMiner != null;
	}

	internal object? TryResolveSpawnOverridePrefabFromContext()
	{
		if (_activeSpawnContextMiner == null) return null;
		var entry = TryGetConfigForMiner(_activeSpawnContextMiner);
		if (entry == null || !entry.overrideOutputs) return null;
		var slot = PickWeightedSlot(entry);
		if (slot == null) return null;
		return ResolveOrePrefabForSlot(slot);
	}

	private MinerConfigEntry? TryGetConfigForMiner(object miner)
	{
		if (miner is MonoBehaviour mb)
		{
			var state = mb.GetComponent<MinerInstanceState>();
			if (state != null && state.hasConfig)
			{
				return state.config;
			}

			if (_activeConfigByMinerInstance.TryGetValue(mb.GetInstanceID(), out var cached))
			{
				return cached;
			}
		}
		return FindSavedEntryForPosition(ReadMinerPosition(miner));
	}

	private MinerConfigEntry? ReadConfigForMiner(MonoBehaviour miner)
	{
		var state = miner.GetComponent<MinerInstanceState>();
		if (state != null && state.hasConfig) return state.config;

		var saved = FindSavedEntryForPosition(miner.transform.position);
		if (saved != null)
		{
			SetStateConfig(miner, saved);
			return saved;
		}
		return null;
	}

	private static void SetStateConfig(MonoBehaviour miner, MinerConfigEntry source)
	{
		var state = miner.GetComponent<MinerInstanceState>();
		if (state == null) state = miner.gameObject.AddComponent<MinerInstanceState>();
		state.hasConfig = true;
		state.config = CloneConfig(source);
	}

	private static void ClearStateConfig(MonoBehaviour miner)
	{
		var state = miner.GetComponent<MinerInstanceState>();
		if (state == null) return;
		if (state.originalDefinition != null)
		{
			var field = miner.GetType().GetField("ResourceDefinition", AnyInstance);
			if (field != null) field.SetValue(miner, state.originalDefinition);
		}
		UnityEngine.Object.Destroy(state);
	}

	private static string BuildDefinitionSignature(MinerConfigEntry config)
	{
		var sb = new StringBuilder(256);
		sb.Append(config.overrideOutputs ? "1" : "0");
		sb.Append('|').Append(config.allowGems ? "1" : "0");
		sb.Append('|').Append(config.polishedGems ? "1" : "0");
		sb.Append('|').Append(config.allowGeodes ? "1" : "0");
		sb.Append('|').Append(config.polishedGeodes ? "1" : "0");
		sb.Append('|').Append(config.group);
		sb.Append('|').Append(config.spawnRate.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
		sb.Append('|').Append(config.spawnProbability.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
		for (var i = 0; i < config.slots.Length; i++)
		{
			var s = config.slots[i];
			sb.Append('|').Append(s.enabled ? "1" : "0");
			sb.Append(':').Append(s.resourceType ?? string.Empty);
			sb.Append(':').Append(s.pieceType ?? string.Empty);
			sb.Append(':').Append(s.polished ? "1" : "0");
			sb.Append(':').Append(s.locked ? "1" : "0");
			sb.Append(':').Append(s.percent.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
		}
		return sb.ToString();
	}

	private void ApplyCustomDefinitionIfNeeded(object miner, MinerConfigEntry config)
	{
		if (miner is not MonoBehaviour mb) return;
		if (mb == null || mb.gameObject == null) return;
		var requiresDefinitionDrivenRate = IsDynamicSpawnRateMiner(mb);
		var state = mb.GetComponent<MinerInstanceState>();
		if (!config.overrideOutputs && !requiresDefinitionDrivenRate && state == null) return;
		if (state == null) state = mb.gameObject.AddComponent<MinerInstanceState>();

		var resourceDefField = mb.GetType().GetField("ResourceDefinition", AnyInstance);
		if (resourceDefField == null) return;
		var currentDef = resourceDefField.GetValue(mb) as UnityEngine.Object;
		if (state.originalDefinition == null && currentDef != null)
		{
			state.originalDefinition = currentDef;
		}

		if (!config.overrideOutputs && !requiresDefinitionDrivenRate)
		{
			if (state.originalDefinition != null && !ReferenceEquals(currentDef, state.originalDefinition))
			{
				resourceDefField.SetValue(mb, state.originalDefinition);
				LogDebug("ApplyCustomDefinitionIfNeeded: restored original ResourceDefinition.");
			}
			state.lastDefinitionSignature = string.Empty;
			state.lastNoValidSlotsSignature = string.Empty;
			return;
		}

		var signature = BuildDefinitionSignature(config);
		if (!string.IsNullOrEmpty(state.lastDefinitionSignature)
			&& string.Equals(state.lastDefinitionSignature, signature, StringComparison.Ordinal)
			&& state.customDefinition != null
			&& ReferenceEquals(currentDef, state.customDefinition))
		{
			return;
		}

		var defType = FindGameTypeByName("AutoMinerResourceDefinition");
		var weightedType = FindGameTypeByName("WeightedOreChance");
		if (defType == null || weightedType == null)
		{
			LogDebug("ApplyCustomDefinitionIfNeeded: missing AutoMinerResourceDefinition/WeightedOreChance types.");
			return;
		}

		if (state.customDefinition == null || !defType.IsInstanceOfType(state.customDefinition))
		{
			state.customDefinition = ScriptableObject.CreateInstance(defType);
			state.customDefinition.hideFlags = HideFlags.HideAndDontSave;
			state.customDefinition.name = $"ConfigurableMiners_CustomDef_{mb.GetInstanceID()}";
			LogDebug("ApplyCustomDefinitionIfNeeded: created custom ResourceDefinition instance.");
		}

		var customDef = state.customDefinition;
		TrySetFloat(customDef, "SpawnRate", Mathf.Clamp(config.spawnRate, 0.01f, 20f));
		TrySetFloat(customDef, "SpawnProbability", Mathf.Clamp(config.spawnProbability, 0f, 100f));

		var listField = defType.GetField("_possibleOrePrefabs", AnyInstance);
		if (listField == null)
		{
			LogDebug("ApplyCustomDefinitionIfNeeded: _possibleOrePrefabs field missing.");
			return;
		}

		var listType = typeof(List<>).MakeGenericType(weightedType);
		var listObj = Activator.CreateInstance(listType);
		var addMethod = listType.GetMethod("Add");
		var oreField = weightedType.GetField("OrePrefab", AnyInstance);
		var weightField = weightedType.GetField("Weight", AnyInstance);
		if (listObj == null || addMethod == null || oreField == null || weightField == null)
		{
			LogDebug("ApplyCustomDefinitionIfNeeded: unable to build WeightedOreChance list.");
			return;
		}

		var count = 0;
		if (config.overrideOutputs)
		{
			for (var i = 0; i < config.slots.Length; i++)
			{
				var slot = config.slots[i];
				if (!slot.enabled || slot.percent <= 0.001f) continue;
				var ore = ResolveOrePrefabForSlot(slot);
				if (ore == null) continue;
				if (!config.allowGems && string.Equals(slot.pieceType, "Gem", StringComparison.OrdinalIgnoreCase)) continue;
				if (!config.allowGeodes && IsGeodePiece(slot.pieceType)) continue;
				if (IsGeodePiece(slot.pieceType) && slot.polished != config.polishedGeodes) continue;

				var weighted = Activator.CreateInstance(weightedType);
				if (weighted == null) continue;
				oreField.SetValue(weighted, ore);
				weightField.SetValue(weighted, slot.percent);
				addMethod.Invoke(listObj, new[] { weighted });
				count++;
			}
		}
		else
		{
			var sourceDefinition = state.originalDefinition ?? currentDef;
			count = CopyWeightedEntriesFromDefinition(sourceDefinition, listObj, addMethod, listField, oreField, weightField);
		}

		if (count <= 0)
		{
			if (!string.Equals(state.lastNoValidSlotsSignature, signature, StringComparison.Ordinal))
			{
				LogDebug("ApplyCustomDefinitionIfNeeded: no valid slots resolved for custom definition.");
			}
			state.lastNoValidSlotsSignature = signature;
			return;
		}

		listField.SetValue(customDef, listObj);
		resourceDefField.SetValue(mb, customDef);
		var configureMethod = mb.GetType().GetMethod("ConfigureFromDefinition", AnyInstance);
		if (configureMethod != null)
		{
			var id = mb.GetInstanceID();
			var added = _configureInvokeGuard.Add(id);
			try
			{
				configureMethod.Invoke(mb, null);
			}
			finally
			{
				if (added) _configureInvokeGuard.Remove(id);
			}
		}
		state.lastNoValidSlotsSignature = string.Empty;
		state.lastDefinitionSignature = signature;
		LogDebug($"ApplyCustomDefinitionIfNeeded: applied custom definition entries={count}.");
	}

	private static int CopyWeightedEntriesFromDefinition(
		UnityEngine.Object? sourceDefinition,
		object targetList,
		MethodInfo addMethod,
		FieldInfo listField,
		FieldInfo oreField,
		FieldInfo weightField)
	{
		if (sourceDefinition == null) return 0;
		if (listField.GetValue(sourceDefinition) is not System.Collections.IEnumerable sourceEntries) return 0;

		var copied = 0;
		foreach (var entry in sourceEntries)
		{
			if (entry == null) continue;
			var ore = oreField.GetValue(entry);
			if (ore == null) continue;
			var weight = weightField.GetValue(entry);
			if (weight is not float weightValue) continue;

			var clone = Activator.CreateInstance(entry.GetType());
			if (clone == null) continue;
			oreField.SetValue(clone, ore);
			weightField.SetValue(clone, weightValue);
			addMethod.Invoke(targetList, new[] { clone });
			copied++;
		}

		return copied;
	}

	private MethodInfo? GetCachedPoolSpawnMethod(object poolManager)
	{
		if (ReferenceEquals(_cachedOrePoolManager, poolManager) && _cachedOrePoolSpawnMethod != null)
		{
			return _cachedOrePoolSpawnMethod;
		}

		_cachedOrePoolManager = poolManager;
		_cachedOrePoolSpawnMethod = poolManager.GetType().GetMethods(AnyInstance)
			.FirstOrDefault(m =>
			{
				if (!string.Equals(m.Name, "SpawnPooledOre", StringComparison.Ordinal)) return false;
				var p = m.GetParameters();
				if (p.Length != 6) return false;
				return p[0].ParameterType.IsEnum &&
					p[1].ParameterType.IsEnum &&
					p[2].ParameterType == typeof(bool) &&
					p[3].ParameterType == typeof(Vector3) &&
					p[4].ParameterType == typeof(Quaternion) &&
					typeof(Transform).IsAssignableFrom(p[5].ParameterType);
			});
		return _cachedOrePoolSpawnMethod;
	}

	private static bool IsDynamicSpawnRateMiner(object miner)
	{
		var type = miner.GetType();
		while (type != null)
		{
			if (string.Equals(type.Name, "RapidAutoMiner", StringComparison.Ordinal))
			{
				return true;
			}
			type = type.BaseType;
		}
		return false;
	}

	private static bool TryBuildEnumSpawnArgs(MethodInfo spawnMethod, MinerSlotConfig slot, out object? resourceEnum, out object? pieceEnum)
	{
		resourceEnum = null;
		pieceEnum = null;
		var parameters = spawnMethod.GetParameters();
		if (parameters.Length < 2) return false;

		var resourceType = parameters[0].ParameterType;
		var pieceType = parameters[1].ParameterType;
		if (!resourceType.IsEnum || !pieceType.IsEnum) return false;
		try
		{
			resourceEnum = Enum.Parse(resourceType, slot.resourceType, ignoreCase: true);
			pieceEnum = Enum.Parse(pieceType, slot.pieceType, ignoreCase: true);
			return true;
		}
		catch
		{
			resourceEnum = null;
			pieceEnum = null;
			return false;
		}
	}

	private void RefreshOrePrefabLookupCache(bool forceRefresh)
	{
		var now = Time.time;
		if (!forceRefresh && now < _nextOrePrefabCacheRefreshTime && _orePrefabExactCache.Count > 0)
		{
			return;
		}

		var manager = GetSingletonInstance("SavingLoadingManager");
		if (manager == null)
		{
			InvalidateSpawnRuntimeCaches();
			return;
		}

		if (!forceRefresh && _orePrefabExactCache.Count > 0 && ReferenceEquals(_cachedOrePrefabSourceManager, manager))
		{
			_nextOrePrefabCacheRefreshTime = now + 5f;
			return;
		}

		_cachedOrePrefabSourceManager = manager;
		_orePrefabExactCache.Clear();
		_orePrefabFallbackCache.Clear();

		var allPrefabs = TryGetFieldValue(manager, "AllOrePiecePrefabs") as System.Collections.IEnumerable;
		if (allPrefabs == null)
		{
			_nextOrePrefabCacheRefreshTime = now + 2f;
			return;
		}

		foreach (var orePrefab in allPrefabs)
		{
			if (orePrefab == null) continue;
			var resourceText = TryGetFieldValue(orePrefab, "ResourceType")?.ToString() ?? string.Empty;
			var pieceText = TryGetFieldValue(orePrefab, "PieceType")?.ToString() ?? string.Empty;
			if (string.IsNullOrWhiteSpace(resourceText) || string.IsNullOrWhiteSpace(pieceText)) continue;

			var isPolished = ReadBool(orePrefab, "IsPolished", false) || ReadFloatFromObject(orePrefab, "PolishedPercent", 0f) >= 0.95f;
			var exactKey = BuildOrePrefabExactKey(resourceText, pieceText, isPolished);
			if (!_orePrefabExactCache.ContainsKey(exactKey))
			{
				_orePrefabExactCache[exactKey] = orePrefab;
			}

			if (!isPolished)
			{
				var fallbackKey = BuildOrePrefabFallbackKey(resourceText, pieceText);
				if (!_orePrefabFallbackCache.ContainsKey(fallbackKey))
				{
					_orePrefabFallbackCache[fallbackKey] = orePrefab;
				}
			}
		}

		_nextOrePrefabCacheRefreshTime = now + 5f;
	}

	private static string BuildOrePrefabExactKey(string resourceType, string pieceType, bool polished)
	{
		return $"{resourceType}|{pieceType}|{polished}";
	}

	private static string BuildOrePrefabFallbackKey(string resourceType, string pieceType)
	{
		return $"{resourceType}|{pieceType}";
	}

	private void InvalidateSpawnRuntimeCaches()
	{
		_orePrefabExactCache.Clear();
		_orePrefabFallbackCache.Clear();
		_cachedOrePrefabSourceManager = null;
		_nextOrePrefabCacheRefreshTime = 0f;
		_cachedOreLimitManager = null;
		_cachedOreLimitShouldBlockMethod = null;
		_cachedOrePoolManager = null;
		_cachedOrePoolSpawnMethod = null;
		_cachedSavingLoadingManager = null;
		_cachedIsCurrentlyLoadingGameProperty = null;
		_cachedIsCurrentlyLoadingGameField = null;
	}

	private object? ResolveOrePrefabForSlot(MinerSlotConfig slot)
	{
		if (string.IsNullOrWhiteSpace(slot.resourceType) || string.IsNullOrWhiteSpace(slot.pieceType)) return null;
		RefreshOrePrefabLookupCache(forceRefresh: false);

		var exactKey = BuildOrePrefabExactKey(slot.resourceType, slot.pieceType, slot.polished);
		if (_orePrefabExactCache.TryGetValue(exactKey, out var exact))
		{
			return exact;
		}

		var fallbackKey = BuildOrePrefabFallbackKey(slot.resourceType, slot.pieceType);
		if (_orePrefabFallbackCache.TryGetValue(fallbackKey, out var fallback))
		{
			return fallback;
		}

		LogDebug($"ResolveOrePrefabForSlot: no prefab found for {slot.resourceType}/{slot.pieceType}.");
		return null;
	}

	private bool TryIsOreSpawningBlocked(out bool blocked)
	{
		blocked = false;
		var mgr = GetSingletonInstance("OreLimitManager");
		if (mgr == null) return false;

		if (!ReferenceEquals(_cachedOreLimitManager, mgr))
		{
			_cachedOreLimitManager = mgr;
			_cachedOreLimitShouldBlockMethod = mgr.GetType().GetMethod("ShouldBlockOreSpawning", AnyInstance);
		}

		if (_cachedOreLimitShouldBlockMethod == null) return false;

		try
		{
			blocked = (bool?)_cachedOreLimitShouldBlockMethod.Invoke(mgr, null) ?? false;
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static MinerSlotConfig? PickWeightedSlot(MinerConfigEntry config)
	{
		var slots = config.slots;
		MinerSlotConfig? firstEligible = null;
		var total = 0f;
		for (var i = 0; i < slots.Length; i++)
		{
			var s = slots[i];
			if (!s.enabled) continue;
			if (s.percent <= 0.001f) continue;
			if (string.IsNullOrWhiteSpace(s.resourceType) || string.IsNullOrWhiteSpace(s.pieceType)) continue;
			if (!config.allowGems && string.Equals(s.pieceType, "Gem", StringComparison.OrdinalIgnoreCase)) continue;
			if (!config.allowGeodes && IsGeodePiece(s.pieceType)) continue;
			if (IsGeodePiece(s.pieceType) && s.polished != config.polishedGeodes) continue;

			firstEligible ??= s;
			total += Mathf.Max(0f, s.percent);
		}

		if (firstEligible == null) return null;
		if (total <= 0.001f) return firstEligible;

		var roll = UnityEngine.Random.value * total;
		var running = 0f;
		for (var i = 0; i < slots.Length; i++)
		{
			var s = slots[i];
			if (!s.enabled) continue;
			if (s.percent <= 0.001f) continue;
			if (string.IsNullOrWhiteSpace(s.resourceType) || string.IsNullOrWhiteSpace(s.pieceType)) continue;
			if (!config.allowGems && string.Equals(s.pieceType, "Gem", StringComparison.OrdinalIgnoreCase)) continue;
			if (!config.allowGeodes && IsGeodePiece(s.pieceType)) continue;
			if (IsGeodePiece(s.pieceType) && s.polished != config.polishedGeodes) continue;

			running += Mathf.Max(0f, s.percent);
			if (roll <= running) return s;
		}
		return firstEligible;
	}
}
