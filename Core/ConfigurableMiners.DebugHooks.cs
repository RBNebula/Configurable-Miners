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
	private void TryHookDebugManagerClearEvent()
	{
		try
		{
			if (_debugManagerClearedDelegate != null && _debugManagerInstance != null) return;
			var type = FindGameTypeByName("DebugManager");
			if (type == null) return;

			var instance = GetSingletonInstance("DebugManager");
			if (instance == null) return;

			var evt = type.GetEvent("ClearedAllPhysicsOrePieces", AnyInstance);
			if (evt == null || evt.EventHandlerType == null) return;

			var callback = GetType().GetMethod(
				nameof(OnDebugManagerClearedAllPhysicsOrePieces),
				BindingFlags.Instance | BindingFlags.NonPublic);
			if (callback == null) return;

			var del = Delegate.CreateDelegate(evt.EventHandlerType, this, callback, throwOnBindFailure: false);
			if (del == null) return;

			evt.AddEventHandler(instance, del);
			_debugManagerInstance = instance;
			_debugManagerClearedEvent = evt;
			_debugManagerClearedDelegate = del;
			LogDebug("Subscribed to DebugManager.ClearedAllPhysicsOrePieces.");
		}
		catch (Exception ex)
		{
			Logger.LogWarning($"[ConfigurableMiners] Failed to hook DebugManager clear event: {ex.Message}");
		}
	}

	private void UnhookDebugManagerClearEvent()
	{
		try
		{
			if (_debugManagerClearedEvent != null &&
				_debugManagerInstance != null &&
				_debugManagerClearedDelegate != null)
			{
				_debugManagerClearedEvent.RemoveEventHandler(_debugManagerInstance, _debugManagerClearedDelegate);
			}
		}
		catch (Exception ex)
		{
			Logger.LogWarning($"[ConfigurableMiners] Failed to unhook DebugManager clear event: {ex.Message}");
		}
		finally
		{
			_debugManagerInstance = null;
			_debugManagerClearedEvent = null;
			_debugManagerClearedDelegate = null;
		}
	}

	private void OnDebugManagerClearedAllPhysicsOrePieces()
	{
		_postOreClearCooldownUntilTime = Mathf.Max(_postOreClearCooldownUntilTime, Time.time + DefaultPostOreClearCooldown);
		_nextRescanTime = _postOreClearCooldownUntilTime;
		_nextApplyTime = _postOreClearCooldownUntilTime;
		InvalidateSpawnRuntimeCaches();
		_minerCache.Clear();
		_activeConfigByMinerInstance.Clear();
		_nextMinerTickReapplyById.Clear();
		PushDebugToast("Detected physics ore clear. Deferred miner apply briefly.");
		LogDebug("Handled DebugManager.ClearedAllPhysicsOrePieces.");
	}
}
