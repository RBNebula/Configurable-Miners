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
	private string GetGroupDisplayName(int group)
	{
		if (group <= 0) return "None";
		EnsureGroupNamesInitialized();
		var index = Mathf.Clamp(group - 1, 0, Mathf.Max(0, _groupNames.Count - 1));
		if (_groupNames.Count == 0) return BuildDefaultGroupName(group);
		return _groupNames[index];
	}

	private void EnsureGroupNamesInitialized()
	{
		if (_groupCount < MinGroupCount) _groupCount = DefaultGroupCount;
		_groupCount = Mathf.Clamp(_groupCount, MinGroupCount, MaxGroupCount);
		while (_groupNames.Count < _groupCount)
		{
			_groupNames.Add(BuildDefaultGroupName(_groupNames.Count + 1));
		}
		if (_groupNames.Count > _groupCount)
		{
			_groupNames.RemoveRange(_groupCount, _groupNames.Count - _groupCount);
		}
		for (var i = 0; i < _groupNames.Count; i++)
		{
			if (string.IsNullOrWhiteSpace(_groupNames[i]))
			{
				_groupNames[i] = BuildDefaultGroupName(i + 1);
			}
			else
			{
				_groupNames[i] = _groupNames[i].Trim();
			}
		}
		SyncLegacyGroupNameFields();
	}

	private void SetGroupCount(int next)
	{
		_groupCount = Mathf.Clamp(next, MinGroupCount, MaxGroupCount);
		EnsureGroupNamesInitialized();
		_edit.group = Mathf.Clamp(_edit.group, 0, _groupCount);
	}

	private static string BuildDefaultGroupName(int groupIndex)
	{
		if (groupIndex <= 0) return "Group";
		var alphabetIndex = groupIndex - 1;
		if (alphabetIndex >= 0 && alphabetIndex < 26)
		{
			var suffix = (char)('A' + alphabetIndex);
			return $"Group {suffix}";
		}
		return $"Group {groupIndex}";
	}

	private void SyncLegacyGroupNameFields()
	{
		_groupNameA = _groupNames.Count >= 1 ? _groupNames[0] : DefaultGroupNameA;
		_groupNameB = _groupNames.Count >= 2 ? _groupNames[1] : DefaultGroupNameB;
		_groupNameC = _groupNames.Count >= 3 ? _groupNames[2] : DefaultGroupNameC;
		_groupNameD = _groupNames.Count >= 4 ? _groupNames[3] : DefaultGroupNameD;
	}

	private string[] GetConfiguredGroupNamesSnapshot()
	{
		EnsureGroupNamesInitialized();
		return _groupNames.Take(_groupCount).ToArray();
	}

	private void ToggleAllMiners(bool enabled)
	{
		var count = 0;
		foreach (var kv in _minerCache)
		{
			if (!TryToggleMinerEnabled(kv.Value.miner, enabled)) continue;
			count++;
		}
		PushToast($"{(enabled ? "Enabled" : "Disabled")} {count} miner(s)", ToastType.Info);
	}

	private void ToggleGroupMiners(int group, bool enabled)
	{
		var count = 0;
		foreach (var kv in _minerCache)
		{
			MinerConfigEntry? cfg = null;
			if (kv.Value.miner is MonoBehaviour mb)
			{
				cfg = ReadConfigForMiner(mb);
			}
			cfg ??= FindSavedEntryForPosition(kv.Value.position);
			if (cfg == null) continue;
			if (cfg.group != group) continue;
			if (!TryToggleMinerEnabled(kv.Value.miner, enabled)) continue;
			count++;
		}
		PushToast($"{(enabled ? "Enabled" : "Disabled")} {count} miner(s) in {GetGroupDisplayName(group)}", ToastType.Info);
	}

	private bool TryToggleMinerEnabled(object miner, bool enabled)
	{
		if (!IsObjectAlive(miner)) return false;
		var type = miner.GetType();
		var toggleMethod = type.GetMethod("Toggle", AnyInstance, null, new[] { typeof(bool) }, null);
		if (toggleMethod != null)
		{
			toggleMethod.Invoke(miner, new object[] { enabled });
			return true;
		}
		var turnOn = type.GetMethod("TurnOn", AnyInstance);
		var turnOff = type.GetMethod("TurnOff", AnyInstance);
		if (enabled && turnOn != null)
		{
			turnOn.Invoke(miner, null);
			return true;
		}
		if (!enabled && turnOff != null)
		{
			turnOff.Invoke(miner, null);
			return true;
		}
		var enabledField = type.GetField("Enabled", AnyInstance);
		if (enabledField != null && enabledField.FieldType == typeof(bool))
		{
			enabledField.SetValue(miner, enabled);
			return true;
		}
		return false;
	}
}
