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
	private void RefreshOutputOptionsForCurrentFilter()
	{
		_outputOptions.Clear();
		var manager = GetSingletonInstance("SavingLoadingManager");
		if (manager != null)
		{
			var allPrefabs = TryGetFieldValue(manager, "AllOrePiecePrefabs") as System.Collections.IEnumerable;
			if (allPrefabs != null)
			{
				CollectOutputOptionsFromPrefabs(allPrefabs, _outputOptions, _edit.allowGems, _edit.polishedGems, _edit.allowGeodes, _edit.polishedGeodes);
			}
		}

		if (_outputOptions.Count == 0)
		{
			CollectFallbackOutputOptions(_outputOptions, _edit.allowGems, _edit.polishedGems, _edit.allowGeodes, _edit.polishedGeodes);
		}

		_outputOptions.Sort((a, b) => string.Compare(a.displayName, b.displayName, StringComparison.OrdinalIgnoreCase));
	}

	private static void CollectOutputOptionsFromPrefabs(System.Collections.IEnumerable prefabs, List<OutputOption> options, bool includeGems, bool polishedGemsOnly, bool includeGeodes, bool polishedGeodesOnly)
	{
		foreach (var orePrefab in prefabs)
		{
			if (orePrefab == null) continue;
			var resourceValue = TryGetFieldValue(orePrefab, "ResourceType");
			var pieceValue = TryGetFieldValue(orePrefab, "PieceType");
			var resource = resourceValue?.ToString() ?? string.Empty;
			var piece = pieceValue?.ToString() ?? string.Empty;
			if (string.IsNullOrWhiteSpace(resource) || string.Equals(resource, "INVALID", StringComparison.OrdinalIgnoreCase)) continue;
			if (!IsSelectablePieceType(piece)) continue;
			var isGem = string.Equals(piece, "Gem", StringComparison.OrdinalIgnoreCase);
			var isGeode = IsGeodePiece(piece);
			if (isGem && !includeGems) continue;
			if (isGeode && !includeGeodes) continue;

			var polished = ReadBool(orePrefab, "IsPolished", false) || ReadFloatFromObject(orePrefab, "PolishedPercent", 0f) >= 0.95f;
			if (isGem && polished != polishedGemsOnly) continue;
			if (isGeode && polished != polishedGeodesOnly) continue;
			var key = $"{resource}|{piece}|{polished}";
			if (options.Any(o => string.Equals(o.key, key, StringComparison.OrdinalIgnoreCase))) continue;
			var display = TryGetOreManagerDisplayText(resourceValue, pieceValue, polished, out var formatted)
				? formatted
				: BuildFallbackOutputDisplayText(resource, piece, polished);
			display = ApplyResourceReadabilityOverrides(display, resource);
			options.Add(new OutputOption
			{
				key = key,
				displayName = display,
				resourceType = resource,
				pieceType = piece,
				polished = polished
			});
		}
	}

	private static bool IsSelectablePieceType(string pieceType)
	{
		if (string.IsNullOrWhiteSpace(pieceType)) return false;
		if (string.Equals(pieceType, "Ore", StringComparison.OrdinalIgnoreCase)) return true;
		if (string.Equals(pieceType, "Gem", StringComparison.OrdinalIgnoreCase)) return true;
		if (IsOreClusterPiece(pieceType)) return true;
		if (IsGeodePiece(pieceType)) return true;
		return false;
	}

	private static bool IsOreClusterPiece(string pieceType)
	{
		return string.Equals(pieceType, "OreCluster", StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsGeodePiece(string pieceType)
	{
		return string.Equals(pieceType, "Geode", StringComparison.OrdinalIgnoreCase);
	}

	private static string GetPieceDisplayName(string pieceType)
	{
		if (IsOreClusterPiece(pieceType)) return "Ore Cluster";
		if (string.Equals(pieceType, "Geode", StringComparison.OrdinalIgnoreCase)) return "Geode";
		return pieceType;
	}

	private static void CollectFallbackOutputOptions(List<OutputOption> options, bool includeGems, bool polishedGemsOnly, bool includeGeodes, bool polishedGeodesOnly)
	{
		options.Add(new OutputOption { key = "Coal|Ore|False", displayName = BuildFallbackOutputDisplayText("Coal", "Ore", polished: false), resourceType = "Coal", pieceType = "Ore", polished = false });
		options.Add(new OutputOption { key = "Iron|Ore|False", displayName = BuildFallbackOutputDisplayText("Iron", "Ore", polished: false), resourceType = "Iron", pieceType = "Ore", polished = false });
		options.Add(new OutputOption { key = "Gold|Ore|False", displayName = BuildFallbackOutputDisplayText("Gold", "Ore", polished: false), resourceType = "Gold", pieceType = "Ore", polished = false });
		options.Add(new OutputOption { key = "Quartz|OreCluster|False", displayName = BuildFallbackOutputDisplayText("Quartz", "OreCluster", polished: false), resourceType = "Quartz", pieceType = "OreCluster", polished = false });
		options.Add(new OutputOption { key = "Amethyst|OreCluster|False", displayName = BuildFallbackOutputDisplayText("Amethyst", "OreCluster", polished: false), resourceType = "Amethyst", pieceType = "OreCluster", polished = false });
		options.Add(new OutputOption { key = "Celestite|OreCluster|False", displayName = BuildFallbackOutputDisplayText("Celestite", "OreCluster", polished: false), resourceType = "Celestite", pieceType = "OreCluster", polished = false });
		options.Add(new OutputOption { key = "Quartz|OreCluster|True", displayName = BuildFallbackOutputDisplayText("Quartz", "OreCluster", polished: true), resourceType = "Quartz", pieceType = "OreCluster", polished = true });
		options.Add(new OutputOption { key = "Amethyst|OreCluster|True", displayName = BuildFallbackOutputDisplayText("Amethyst", "OreCluster", polished: true), resourceType = "Amethyst", pieceType = "OreCluster", polished = true });
		options.Add(new OutputOption { key = "Celestite|OreCluster|True", displayName = BuildFallbackOutputDisplayText("Celestite", "OreCluster", polished: true), resourceType = "Celestite", pieceType = "OreCluster", polished = true });
		if (includeGeodes)
		{
			options.Add(new OutputOption { key = $"Quartz|Geode|{polishedGeodesOnly}", displayName = BuildFallbackOutputDisplayText("Quartz", "Geode", polishedGeodesOnly), resourceType = "Quartz", pieceType = "Geode", polished = polishedGeodesOnly });
			options.Add(new OutputOption { key = $"Amethyst|Geode|{polishedGeodesOnly}", displayName = BuildFallbackOutputDisplayText("Amethyst", "Geode", polishedGeodesOnly), resourceType = "Amethyst", pieceType = "Geode", polished = polishedGeodesOnly });
			options.Add(new OutputOption { key = $"Celestite|Geode|{polishedGeodesOnly}", displayName = BuildFallbackOutputDisplayText("Celestite", "Geode", polishedGeodesOnly), resourceType = "Celestite", pieceType = "Geode", polished = polishedGeodesOnly });
		}
		if (!includeGems) return;
		options.Add(new OutputOption { key = $"Emerald|Gem|{polishedGemsOnly}", displayName = BuildFallbackOutputDisplayText("Emerald", "Gem", polishedGemsOnly), resourceType = "Emerald", pieceType = "Gem", polished = polishedGemsOnly });
		options.Add(new OutputOption { key = $"Diamond|Gem|{polishedGemsOnly}", displayName = BuildFallbackOutputDisplayText("Diamond", "Gem", polishedGemsOnly), resourceType = "Diamond", pieceType = "Gem", polished = polishedGemsOnly });
	}

	private static bool TryGetOreManagerDisplayText(object? resourceValue, object? pieceValue, bool polished, out string display)
	{
		display = string.Empty;
		if (resourceValue == null || pieceValue == null) return false;
		var oreManager = GetSingletonInstance("OreManager");
		if (oreManager == null) return false;
		var method = oreManager.GetType().GetMethod("GetColoredFormattedResourcePieceString", AnyInstance);
		if (method == null) return false;
		var parameters = method.GetParameters();
		if (parameters.Length < 3) return false;
		try
		{
			var result = method.Invoke(oreManager, new[] { resourceValue, pieceValue, polished }) as string;
			if (string.IsNullOrWhiteSpace(result)) return false;
			display = result!;
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static string BuildFallbackOutputDisplayText(string resource, string piece, bool polished)
	{
		var displayPiece = GetPieceDisplayName(piece);
		var display = polished ? $"Polished {resource} {displayPiece}" : $"{resource} {displayPiece}";
		return ApplyResourceReadabilityOverrides(display, resource);
	}

	private static string ApplyResourceReadabilityOverrides(string display, string resource)
	{
		if (string.IsNullOrWhiteSpace(display) || string.IsNullOrWhiteSpace(resource)) return display;
		if (!TryGetReadableResourceHex(resource, out var hex)) return display;

		const string colorTagPrefix = "<color=#";
		var tagIndex = display.IndexOf(colorTagPrefix, StringComparison.OrdinalIgnoreCase);
		if (tagIndex >= 0)
		{
			var hexStart = tagIndex + colorTagPrefix.Length;
			var hexEnd = display.IndexOf('>', hexStart);
			if (hexEnd > hexStart)
			{
				return display.Substring(0, hexStart) + hex + display.Substring(hexEnd);
			}
		}

		return $"<color=#{hex}>{display}</color>";
	}

	private static bool TryGetReadableResourceHex(string resource, out string hex)
	{
		hex = string.Empty;
		if (string.IsNullOrWhiteSpace(resource)) return false;
		switch (resource.Trim().ToUpperInvariant())
		{
			case "COAL":
				hex = "C7CFDC";
				return true;
			case "AMETHYST":
				hex = "C28BFF";
				return true;
			case "CELESTITE":
				hex = "8FE9FF";
				return true;
			case "COPPER":
				hex = "FF8A4D";
				return true;
			case "RUBY":
				hex = "FF5A5A";
				return true;
			default:
				return false;
		}
	}
}
