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
	private const string RebindPluginGuid = "com.rebind";
	private const string RebindApiTypeName = "Rebind.RebindApi";
	private const string RebindSectionTitle = "Configurable Miners";

	private readonly List<IDisposable> _rebindHandles = new();
	private bool _rebindRegistered;
	private float _nextRebindAttemptTime;

	private void InitializeRebindIntegration()
	{
		_rebindRegistered = TryRegisterRebindKeybinds();
		_nextRebindAttemptTime = Time.unscaledTime + 1f;
	}

	private void TickRebindIntegration()
	{
		if (_rebindRegistered) return;
		if (!IsRebindInstalled()) return;
		if (Time.unscaledTime < _nextRebindAttemptTime) return;

		_rebindRegistered = TryRegisterRebindKeybinds();
		_nextRebindAttemptTime = Time.unscaledTime + 1f;
	}

	private void DisposeRebindHandles()
	{
		for (var i = 0; i < _rebindHandles.Count; i++)
		{
			try
			{
				_rebindHandles[i].Dispose();
			}
			catch
			{
				// Ignore external API cleanup failures.
			}
		}

		_rebindHandles.Clear();
		_rebindRegistered = false;
	}

	private bool ShouldUseInlineKeybindUi()
	{
		return !IsRebindInstalled();
	}

	private bool TryRegisterRebindKeybinds()
	{
		if (_rebindRegistered || _rebindHandles.Count > 0) return true;
		if (!IsRebindInstalled()) return false;

		var apiType = AppDomain.CurrentDomain
			.GetAssemblies()
			.Select(a => a.GetType(RebindApiTypeName, throwOnError: false))
			.FirstOrDefault(t => t != null);
		if (apiType == null) return false;

		var registerShortcut = apiType.GetMethod(
			"RegisterKeybind",
			BindingFlags.Public | BindingFlags.Static,
			null,
			new[]
			{
				typeof(string),
				typeof(string),
				typeof(Func<KeyboardShortcut>),
				typeof(Action<KeyboardShortcut>),
				typeof(KeyboardShortcut),
				typeof(Action<KeyboardShortcut>)
			},
			null);

		if (registerShortcut != null)
		{
			var registered = 0;
			registered += RegisterShortcutRebindRow(registerShortcut, "Open UI", () => _toggleKey, key => _toggleKey = key, DefaultToggleKey);
			registered += RegisterShortcutRebindRow(registerShortcut, "Close UI", () => _closeKey, key => _closeKey = key, DefaultCloseKey);

			if (registered > 0)
			{
				Logger.LogInfo($"[ConfigurableMiners] Registered {registered} keybind(s) with Rebind (KeyboardShortcut).");
				return true;
			}
		}

		// Backward compatibility with older Rebind versions.
		var registerLegacy = apiType.GetMethod(
			"RegisterKeybind",
			BindingFlags.Public | BindingFlags.Static,
			null,
			new[]
			{
				typeof(string),
				typeof(string),
				typeof(Func<KeyCode>),
				typeof(Action<KeyCode>),
				typeof(KeyCode),
				typeof(Action<KeyCode>)
			},
			null);

		if (registerLegacy == null)
		{
			Logger.LogWarning("[ConfigurableMiners] Rebind API found but no supported RegisterKeybind signature was available.");
			return false;
		}

		var legacyRegistered = 0;
		legacyRegistered += RegisterLegacyRebindRow(registerLegacy, "Open UI", () => _toggleKey, key => _toggleKey = key, DefaultToggleKey);
		legacyRegistered += RegisterLegacyRebindRow(registerLegacy, "Close UI", () => _closeKey, key => _closeKey = key, DefaultCloseKey);

		if (legacyRegistered > 0)
		{
			Logger.LogInfo($"[ConfigurableMiners] Registered {legacyRegistered} keybind(s) with Rebind (legacy KeyCode fallback).");
		}

		return legacyRegistered > 0;
	}

	private int RegisterShortcutRebindRow(
		MethodInfo registerMethod,
		string keybindTitle,
		Func<KeyCode> getter,
		Action<KeyCode> setter,
		KeyCode defaultKey)
	{
		try
		{
			Func<KeyboardShortcut> getShortcut = () => ToShortcut(getter());
			Action<KeyboardShortcut> setShortcut = shortcut => setter(shortcut.MainKey);
			Action<KeyboardShortcut> onChanged = shortcut =>
			{
				SaveSettingsToDisk();
				var keyName = shortcut.MainKey == KeyCode.None ? "None" : shortcut.MainKey.ToString();
				PushToast($"{keybindTitle} set to {keyName}", ToastType.Info);
			};

			var handle = registerMethod.Invoke(
				null,
				new object?[]
				{
					RebindSectionTitle,
					keybindTitle,
					getShortcut,
					setShortcut,
					ToShortcut(defaultKey),
					onChanged
				});

			if (handle is IDisposable disposableHandle)
			{
				_rebindHandles.Add(disposableHandle);
				return 1;
			}
		}
		catch (Exception ex)
		{
			Logger.LogWarning($"[ConfigurableMiners] Failed to register Rebind row '{keybindTitle}': {ex.Message}");
		}

		return 0;
	}

	private int RegisterLegacyRebindRow(
		MethodInfo registerMethod,
		string keybindTitle,
		Func<KeyCode> getter,
		Action<KeyCode> setter,
		KeyCode defaultKey)
	{
		try
		{
			Action<KeyCode> onChanged = key =>
			{
				SaveSettingsToDisk();
				var keyName = key == KeyCode.None ? "None" : key.ToString();
				PushToast($"{keybindTitle} set to {keyName}", ToastType.Info);
			};

			var handle = registerMethod.Invoke(
				null,
				new object?[]
				{
					RebindSectionTitle,
					keybindTitle,
					getter,
					setter,
					defaultKey,
					onChanged
				});

			if (handle is IDisposable disposableHandle)
			{
				_rebindHandles.Add(disposableHandle);
				return 1;
			}
		}
		catch (Exception ex)
		{
			Logger.LogWarning($"[ConfigurableMiners] Failed to register Rebind row '{keybindTitle}': {ex.Message}");
		}

		return 0;
	}

	private static KeyboardShortcut ToShortcut(KeyCode key)
	{
		return key == KeyCode.None ? KeyboardShortcut.Empty : new KeyboardShortcut(key);
	}

	private static bool IsRebindInstalled()
	{
		return BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(RebindPluginGuid);
	}
}
