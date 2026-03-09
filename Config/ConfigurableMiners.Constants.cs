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
	private enum HoverPromptPosition
	{
		Center,
		Top,
		Bottom
	}

	private enum MinerGroup
	{
		None = 0,
		A = 1,
		B = 2,
		C = 3,
		D = 4
	}

	private enum ActivePanel
	{
		MinerConfig,
		ToggleMenu
	}

	private const string ConfigFolderName = "ConfigurableMiners";
	private const string SettingsFileName = "configurable_miners_settings.json";
	private const string SaveDataFileName = "miners.json";
	private const string PresetsFileName = "presets.json";

	private const KeyCode DefaultToggleKey = KeyCode.M;
	private const KeyCode DefaultCloseKey = KeyCode.Escape;

	private const float DefaultSelectDistance = 10f;
	private const float DefaultHoverDistance = 10f;
	private const float DefaultApplyInterval = 0.35f;
	private const float DefaultUpdateTickReapplyInterval = 0.5f;
	private const float DefaultRescanInterval = 3f;
	private const float DefaultSaveResolveInterval = 2f;
	private const float DefaultPostOreClearCooldown = 1.5f;
	private const float DefaultPositionMatchEpsilon = 0.12f;
	private const float DefaultAreaApplyRadius = 25f;

	private const int DefaultWindowWidth = 630;
	private const float DefaultWindowOpacity = 1f;
	private const float DefaultWindowRowHeight = 27f;
	private const float DefaultWindowBaseHeight = 390f;
	private const string DefaultStylePreset = UiStylePresets.DefaultPresetId;
	private const bool DefaultShowHoverPrompt = true;
	private const string DefaultHoverPromptPosition = "bottom";
	private const bool DefaultShowBuildingInfoDetails = false;
	private const bool DefaultRgbBorderEnabled = true;
	private const float DefaultRgbBorderWidth = 2f;
	private const float DefaultRgbBorderSegment = 6f;
	private const float DefaultRgbBorderSpeed = 0.45f;
	private const int DefaultGroupCount = 4;
	private const int MinGroupCount = 1;
	private const int MaxGroupCount = 12;
	private const int DefaultPresetCount = 8;
	private const int MinPresetCount = 1;
	private const int MaxPresetCount = 32;
	private const string DefaultGroupNameA = "Group A";
	private const string DefaultGroupNameB = "Group B";
	private const string DefaultGroupNameC = "Group C";
	private const string DefaultGroupNameD = "Group D";

	private static readonly string[] HoverPromptPositionReference =
	{
		"Valid values for hoverPromptPosition:",
		"top",
		"center",
		"bottom"
	};

	private static readonly string[] StylePresetReference = UiStylePresets.BuildSettingsReference();
}
