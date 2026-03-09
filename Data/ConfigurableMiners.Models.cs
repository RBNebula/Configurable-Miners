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
	[Serializable]
	private sealed class MinerSlotConfig
	{
		public bool enabled;
		public string resourceType = string.Empty;
		public string pieceType = string.Empty;
		public bool polished;
		public bool locked;
		public float percent = 25f;
	}

	[Serializable]
	private sealed class MinerConfigEntry
	{
		public float x;
		public float y;
		public float z;
		public bool overrideOutputs;
		public float spawnRate = 2f;
		public float spawnProbability = 80f;
		public bool allowGems = true;
		public bool polishedGems;
		public bool allowGeodes = true;
		public bool polishedGeodes;
		public int group = (int)MinerGroup.None;
		public MinerSlotConfig[] slots = CreateDefaultSlots();
	}

	[Serializable]
	private sealed class PresetEntry
	{
		public string name = string.Empty;
		public bool hasValue;
		public bool overrideOutputs;
		public float spawnRate = 2f;
		public float spawnProbability = 80f;
		public bool allowGems = true;
		public bool polishedGems;
		public bool allowGeodes = true;
		public bool polishedGeodes;
		public int group = (int)MinerGroup.None;
		public MinerSlotConfig[] slots = CreateDefaultSlots();
	}

	[Serializable]
	private sealed class SettingsData
	{
		public string toggleKey = DefaultToggleKey.ToString();
		public string closeKey = DefaultCloseKey.ToString();
		public float selectDistance = DefaultSelectDistance;
		public float hoverDistance = DefaultHoverDistance;
		public float applyInterval = DefaultApplyInterval;
		public float rescanInterval = DefaultRescanInterval;
		public float positionMatchEpsilon = DefaultPositionMatchEpsilon;
		public float areaApplyRadius = DefaultAreaApplyRadius;
		public string stylePreset = DefaultStylePreset;
		public bool showHoverPrompt = DefaultShowHoverPrompt;
		public string hoverPromptPosition = DefaultHoverPromptPosition;
		public bool showBuildingInfoDetails = DefaultShowBuildingInfoDetails;
		public bool rgbBorderEnabled = DefaultRgbBorderEnabled;
		public float rgbBorderWidth = DefaultRgbBorderWidth;
		public float rgbBorderSegment = DefaultRgbBorderSegment;
		public float rgbBorderSpeed = DefaultRgbBorderSpeed;
		public int groupCount = DefaultGroupCount;
		public int presetCount = DefaultPresetCount;
		public string[] groupNames = Array.Empty<string>();
		public string groupNameA = DefaultGroupNameA;
		public string groupNameB = DefaultGroupNameB;
		public string groupNameC = DefaultGroupNameC;
		public string groupNameD = DefaultGroupNameD;
		public bool debugLogging;
		public bool debugToasts = true;
	}

	private sealed class MinerRuntimeState
	{
		public object miner = null!;
		public int instanceId;
		public Vector3 position;
		public bool hasCustomConfig;
		public MinerConfigEntry config = new();
	}

	private sealed class OutputOption
	{
		public string key = string.Empty;
		public string displayName = string.Empty;
		public string resourceType = string.Empty;
		public string pieceType = string.Empty;
		public bool polished;
	}

	private static MinerSlotConfig[] CreateDefaultSlots()
	{
		return new[]
		{
			new MinerSlotConfig(),
			new MinerSlotConfig(),
			new MinerSlotConfig(),
			new MinerSlotConfig()
		};
	}
}
