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
	private void InitializePlugin()
	{
		Instance = this;
		LoadSettingsFromDisk();
		InitializeRebindIntegration();
		TryResolveSavePaths(createFolderIfMissing: false);
		LoadSaveDataFromDisk();
		LoadPresetsFromDisk();
		_harmony.PatchAll();
		_nextApplyTime = 1f;
		_nextRescanTime = 1f;
		_nextSaveResolveTime = 1f;
		_nextDebugManagerHookAttemptTime = 0f;
		TryHookDebugManagerClearEvent();
		var patchedCount = _harmony.GetPatchedMethods().Count();
		LogDebug($"Awake complete. Harmony patched methods={patchedCount}");
	}

	private void ShutdownPlugin()
	{
		DestroyUnityUi();
		DisposeRebindHandles();
		UnhookDebugManagerClearEvent();
		EndUiManagerSuppression();
		_harmony.UnpatchSelf();
		SaveSettingsToDisk();
		SaveSaveDataToDisk();
		SavePresetsToDisk();
		if (ReferenceEquals(Instance, this)) Instance = null;
	}

	private void Update()
	{
		TickRebindIntegration();
		var capturingKeybind = _keybindCapture.IsCapturing;
		var hotkeysEnabled = HotkeyGate.IsHotkeyInputEnabled();
		var hotkeysBlockedByTextInput = IsHotkeyBlockedByTextInput();
		_debugAppliedConfigsThisFrame = 0;
		TrackSceneChangeDirty();

		if (Time.time >= _nextSaveResolveTime)
		{
			_nextSaveResolveTime = Time.time + Mathf.Max(0.5f, _saveResolveInterval);
			TryResolveSavePaths(createFolderIfMissing: false);
		}

		if (Time.time >= _nextDebugManagerHookAttemptTime && _debugManagerClearedDelegate == null)
		{
			_nextDebugManagerHookAttemptTime = Time.time + 2f;
			TryHookDebugManagerClearEvent();
		}

		var saveSystemLoading = IsSaveSystemLoading();
		if (saveSystemLoading)
		{
			_saveLoadingWasActive = true;
		}
		else if (_saveLoadingWasActive)
		{
			_saveLoadingWasActive = false;
			_nextRescanTime = 0f;
			_nextApplyTime = 0f;
		}

		if (Time.time >= _nextRescanTime)
		{
			if (saveSystemLoading || Time.time < _postOreClearCooldownUntilTime)
			{
				_nextRescanTime = Mathf.Max(_nextRescanTime, Time.time + 0.5f);
			}
			else
			{
				_nextRescanTime = Time.time + Mathf.Max(0.2f, _rescanInterval);
				RefreshMinerCache();
				PruneStaleSavedEntries();
			}
		}

		if (Time.time >= _nextApplyTime)
		{
			if (saveSystemLoading || Time.time < _postOreClearCooldownUntilTime)
			{
				_nextApplyTime = Mathf.Max(_nextApplyTime, Time.time + 0.25f);
			}
			else
			{
				_nextApplyTime = Time.time + Mathf.Max(0.05f, _applyInterval);
				ApplyAllSavedConfigs();
			}
		}

		if (hotkeysEnabled && !capturingKeybind && !hotkeysBlockedByTextInput && Input.GetKeyDown(_toggleKey))
		{
			if (_uiOpen)
			{
				_closeFocusAndClose();
			}
			else
			{
				if (TrySelectMinerForInteraction(out var miner))
				{
					BindSelectedMiner(miner!);
					_activePanel = ActivePanel.MinerConfig;
					OpenUi();
				}
				else
				{
					_activePanel = ActivePanel.ToggleMenu;
					OpenUi();
				}
			}
		}

		if (hotkeysEnabled && !capturingKeybind && !hotkeysBlockedByTextInput && _uiOpen && Input.GetKeyDown(_closeKey))
		{
			_closeFocusAndClose();
		}

		if (_uiOpen)
		{
			EnsureCursor();
			RefreshUnityUi();
		}
	}

	private void LateUpdate()
	{
		if (_uiOpen) EnsureCursor();
	}

	private void OnGUI()
	{
		InitStyles();
		if (_clearFocusNextGui)
		{
			GUI.FocusControl(null);
			GUIUtility.keyboardControl = 0;
			_clearFocusNextGui = false;
		}

		if (_uiOpen) EnsureCursor();

		if (!_uiOpen)
		{
			DrawHoverPrompt();
			DrawToasts();
			DrawDebugOverlay();
			return;
		}

		if (UseUnityUi)
		{
			DrawToasts();
			DrawDebugOverlay();
			return;
		}

		var rightReserve = _configPanelOpen ? 448f : 0f; // panel width (440) + gap (8)
		var rect = GetCenteredRectWithRightReserve(DefaultWindowWidth, GetWindowHeight(), rightReserve);
		if (_activePanel == ActivePanel.ToggleMenu)
		{
			GUI.Window(601124, rect, DrawToggleMenuWindow, string.Empty, _windowStyle ?? GUI.skin.window);
		}
		else
		{
			GUI.Window(601123, rect, DrawWindow, string.Empty, _windowStyle ?? GUI.skin.window);
		}
		if (_configPanelOpen)
		{
			DrawConfigPanel(rect);
		}
		DrawToasts();
		DrawDebugOverlay();
	}

	private void _closeFocusAndClose()
	{
		_clearFocusNextGui = true;
		CloseUi();
	}

	private void TrackSceneChangeDirty()
	{
		var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
		if (string.Equals(scene, _lastObservedScene, StringComparison.Ordinal)) return;
		_lastObservedScene = scene;
		InvalidateSpawnRuntimeCaches();
		_nextRescanTime = 0f;
		_nextApplyTime = 0f;
	}

	private static Rect GetCenteredRect(int width, int height)
	{
		return new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
	}

	private static Rect GetCenteredRectWithRightReserve(int width, int height, float rightReserve, float margin = 8f)
	{
		var x = (Screen.width - width) * 0.5f;
		var y = (Screen.height - height) * 0.5f;
		var reserve = Mathf.Max(0f, rightReserve);

		var minX = margin;
		var maxX = Screen.width - width - reserve - margin;
		if (maxX >= minX)
		{
			x = Mathf.Clamp(x, minX, maxX);
		}
		else
		{
			x = Mathf.Clamp(x, 0f, Mathf.Max(0f, Screen.width - width));
		}

		var minY = margin;
		var maxY = Screen.height - height - margin;
		if (maxY >= minY)
		{
			y = Mathf.Clamp(y, minY, maxY);
		}

		return new Rect(x, y, width, height);
	}

	private int GetWindowHeight()
	{
		if (_activePanel == ActivePanel.ToggleMenu)
		{
			var toggleHeight = 230f + (2f * DefaultWindowRowHeight);
			return Mathf.CeilToInt(Mathf.Clamp(toggleHeight, 220f, Screen.height - 24f));
		}

		var height = DefaultWindowBaseHeight + (16f * DefaultWindowRowHeight);
		return Mathf.CeilToInt(Mathf.Clamp(height, 520f, Screen.height - 24f));
	}

	private bool IsHotkeyBlockedByTextInput()
	{
		if (!IsUnityTextInputFocused()) return false;
		return true;
	}

	private static bool IsUnityTextInputFocused()
	{
		var eventSystem = UnityEngine.EventSystems.EventSystem.current;
		if (eventSystem == null) return false;
		var selected = eventSystem.currentSelectedGameObject;
		if (selected == null) return false;

		var unityInput = selected.GetComponentInParent<UnityEngine.UI.InputField>();
		if (unityInput != null)
		{
			return unityInput.isFocused;
		}

		return HasTmpInputFieldInParents(selected.transform);
	}

	private static bool HasTmpInputFieldInParents(Transform? transform)
	{
		var current = transform;
		while (current != null)
		{
			if (HasFocusedTmpInputField(current.gameObject))
			{
				return true;
			}
			current = current.parent;
		}
		return false;
	}

	private static bool HasFocusedTmpInputField(GameObject gameObject)
	{
		var behaviours = gameObject.GetComponents<MonoBehaviour>();
		for (var i = 0; i < behaviours.Length; i++)
		{
			var behaviour = behaviours[i];
			if (behaviour == null) continue;
			var type = behaviour.GetType();
			if (!string.Equals(type.Name, "TMP_InputField", StringComparison.Ordinal)) continue;
			var focusedProperty = type.GetProperty("isFocused", AnyInstance);
			if (focusedProperty == null) return true;
			var focusedValue = focusedProperty.GetValue(behaviour, null);
			if (focusedValue is bool isFocused)
			{
				return isFocused;
			}
			return true;
		}
		return false;
	}
}
