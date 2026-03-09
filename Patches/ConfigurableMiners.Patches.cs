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
internal static class ConfigurableMinersPatches
{
	[HarmonyPatch]
	private static class AutoMinerUpdatePatch
	{
		private static MethodBase? TargetMethod()
		{
			var type = AccessTools.TypeByName("AutoMiner");
			return type == null ? null : AccessTools.Method(type, "Update");
		}

		private static void Prefix(object __instance)
		{
			ConfigurableMiners.Instance?.OnMinerUpdateTick(__instance);
		}
	}

	[HarmonyPatch]
	private static class AutoMinerConfigurePatch
	{
		private static MethodBase? TargetMethod()
		{
			var type = AccessTools.TypeByName("AutoMiner");
			return type == null ? null : AccessTools.Method(type, "ConfigureFromDefinition");
		}

		private static void Postfix(object __instance)
		{
			ConfigurableMiners.Instance?.OnMinerConfiguredByGame(__instance);
		}
	}

	[HarmonyPatch]
	private static class AutoMinerTrySpawnPatch
	{
		private static IEnumerable<MethodBase> TargetMethods()
		{
			var baseType = AccessTools.TypeByName("AutoMiner");
			if (baseType == null) yield break;
			var seen = new HashSet<MethodBase>();

			var baseMethod = AccessTools.Method(baseType, "TrySpawnOre");
			if (baseMethod != null && seen.Add(baseMethod)) yield return baseMethod;

			foreach (var t in AccessTools.AllTypes())
			{
				if (t == null || t == baseType) continue;
				if (!baseType.IsAssignableFrom(t)) continue;

				// Use direct reflection here so subclasses without an override do not emit warning logs.
				var declared = t.GetMethod(
					"TrySpawnOre",
					BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
				if (declared != null && seen.Add(declared)) yield return declared;
			}
		}

		private static bool Prefix(object __instance)
		{
			var plugin = ConfigurableMiners.Instance;
			if (plugin == null) return true;
			if (!plugin.ShouldInterceptTrySpawn(__instance)) return true;
			plugin.TracePatch($"TrySpawnOre Prefix hit on {__instance.GetType().Name} id={(__instance as UnityEngine.Object)?.GetInstanceID()}");
			plugin.BeginMinerSpawnContext(__instance);
			if (plugin.TryHandleCustomSpawn(__instance)) return false;
			return true;
		}

		private static void Postfix()
		{
			var plugin = ConfigurableMiners.Instance;
			if (plugin == null) return;
			if (plugin.HasActiveSpawnContext()) plugin.TracePatch("TrySpawnOre Postfix");
			plugin.EndMinerSpawnContext();
		}
	}

	[HarmonyPatch]
	private static class OrePiecePoolSpawnByPrefabPatch
	{
		private static MethodBase? TargetMethod()
		{
			var type = AccessTools.TypeByName("OrePiecePoolManager");
			if (type == null) return null;
			var orePieceType = AccessTools.TypeByName("OrePiece");
			if (orePieceType == null) return null;
			return type
				.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
				.FirstOrDefault(m =>
				{
					if (!string.Equals(m.Name, "SpawnPooledOre", StringComparison.Ordinal)) return false;
					var p = m.GetParameters();
					return p.Length >= 1 && p[0].ParameterType == orePieceType;
				});
		}

		private static void Prefix(object[] __args)
		{
			var plugin = ConfigurableMiners.Instance;
			if (plugin == null) return;
			if (__args == null || __args.Length == 0) return;
			var replacement = plugin.TryResolveSpawnOverridePrefabFromContext();
			if (replacement == null) return;
			plugin.TracePatch("OrePiecePoolManager.SpawnPooledOre(OrePiece,...) arg[0] replaced by override.");
			__args[0] = replacement;
		}
	}

	[HarmonyPatch]
	private static class UIManagerShowBuildingInfoPatch
	{
		private static MethodBase? TargetMethod()
		{
			var type = AccessTools.TypeByName("UIManager");
			return type == null ? null : AccessTools.Method(type, "ShowBuildingInfo", new[] { typeof(string) });
		}

		private static void Prefix(ref string description)
		{
			var plugin = ConfigurableMiners.Instance;
			if (plugin == null) return;
			description = plugin.DecorateBuildingInfoText(description);
		}
	}
}
