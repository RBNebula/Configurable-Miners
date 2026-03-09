using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using RBN.Modlib.UI;
using UnityEngine;

namespace ConfigurableMiners;

public sealed partial class ConfigurableMiners
{
    internal static ConfigurableMiners? Instance;

    private bool _uiOpen;
    private ActivePanel _activePanel = ActivePanel.MinerConfig;
    private bool _configPanelOpen;
    private bool _clearFocusNextGui;
    private CursorLockMode _prevLockMode;
    private bool _prevCursorVisible;
    private readonly List<MonoBehaviour> _disabledScripts = new();
    private MonoBehaviour? _suppressedUiManagerBehaviour;
    private bool _suppressedUiManagerWasEnabled;

    private KeyCode _toggleKey = DefaultToggleKey;
    private KeyCode _closeKey = DefaultCloseKey;
    private float _selectDistance = DefaultSelectDistance;
    private float _hoverDistance = DefaultHoverDistance;
    private float _applyInterval = DefaultApplyInterval;
    private float _rescanInterval = DefaultRescanInterval;
    private float _saveResolveInterval = DefaultSaveResolveInterval;
    private float _positionMatchEpsilon = DefaultPositionMatchEpsilon;
    private float _areaApplyRadius = DefaultAreaApplyRadius;
    private string _stylePreset = DefaultStylePreset;
    private bool _showHoverPrompt = DefaultShowHoverPrompt;
    private HoverPromptPosition _hoverPromptPosition = HoverPromptPosition.Bottom;
    private bool _showBuildingInfoDetails = DefaultShowBuildingInfoDetails;
    private bool _rgbBorderEnabled = DefaultRgbBorderEnabled;
    private float _rgbBorderWidth = DefaultRgbBorderWidth;
    private float _rgbBorderSegment = DefaultRgbBorderSegment;
    private float _rgbBorderSpeed = DefaultRgbBorderSpeed;
    private string _groupNameA = DefaultGroupNameA;
    private string _groupNameB = DefaultGroupNameB;
    private string _groupNameC = DefaultGroupNameC;
    private string _groupNameD = DefaultGroupNameD;
    private int _groupCount = DefaultGroupCount;
    private int _presetCount = DefaultPresetCount;
    private readonly List<string> _groupNames = new();
    private bool _debugLogging;
    private bool _debugToasts = true;
    private int _debugAppliedConfigsThisFrame;
    private bool _debugSavePathResolved;
    private string _debugActiveSaveName = "N/A";

    private object? _selectedMiner;
    private string _selectedMinerLabel = string.Empty;
    private Vector3 _selectedMinerPos;

    private string? _settingsFilePath;
    private string? _saveDataFilePath;
    private string? _presetsFilePath;
    private float _nextSaveResolveTime;
    private float _nextApplyTime;
    private float _nextRescanTime;
    private float _postOreClearCooldownUntilTime;
    private float _nextDebugManagerHookAttemptTime;
    private object? _debugManagerInstance;
    private EventInfo? _debugManagerClearedEvent;
    private Delegate? _debugManagerClearedDelegate;
    private object? _cachedSavingLoadingManager;
    private PropertyInfo? _cachedIsCurrentlyLoadingGameProperty;
    private FieldInfo? _cachedIsCurrentlyLoadingGameField;
    private bool _saveLoadingWasActive;
    private string _lastObservedScene = string.Empty;

    private readonly Dictionary<int, MinerRuntimeState> _minerCache = new();
    private readonly Dictionary<int, MinerConfigEntry> _activeConfigByMinerInstance = new();
    private readonly Dictionary<string, float> _staleSavedEntryFirstMissingAt = new();
    private readonly Dictionary<int, float> _nextMinerTickReapplyById = new();
    private readonly HashSet<int> _configureInvokeGuard = new();
    private readonly List<MinerConfigEntry> _savedEntries = new();
    private readonly Dictionary<string, object> _orePrefabExactCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, object> _orePrefabFallbackCache = new(StringComparer.OrdinalIgnoreCase);
    private float _nextOrePrefabCacheRefreshTime;
    private object? _cachedOrePrefabSourceManager;
    private object? _cachedOreLimitManager;
    private MethodInfo? _cachedOreLimitShouldBlockMethod;
    private object? _cachedOrePoolManager;
    private MethodInfo? _cachedOrePoolSpawnMethod;
    private readonly List<PresetEntry> _presets = Enumerable.Range(0, DefaultPresetCount).Select(_ => new PresetEntry()).ToList();
    private readonly List<OutputOption> _outputOptions = new();
    private float _updateTickReapplyInterval = DefaultUpdateTickReapplyInterval;

    private MinerConfigEntry _edit = new();
    private MinerConfigEntry? _clipboardConfig;
    private bool _editDirty;
    private int _outputPickerSlot = -1;
    private bool _groupPickerOpen;
    private Vector2 _mainScroll;
    private Vector2 _pickerScroll;
    private Vector2 _configScroll;
    private bool _configUiErrorLogged;

    private GUIStyle? _windowStyle;
    private GUIStyle? _labelStyle;
    private GUIStyle? _textFieldStyle;
    private GUIStyle? _richButtonStyle;
    private bool _stylesInitialized;
    private string _appliedStylePreset = string.Empty;

    private readonly UiToastQueue _toasts = new();
    private readonly UiKeybindCaptureState _keybindCapture = new();
    private readonly List<string> _debugOverlayLines = new();
    private readonly Dictionary<string, string> _sliderTextCache = new();
    private GameObject? _hoverPromptHotbarPanel;
    private float _nextHoverPromptHotbarResolveTime;
}
