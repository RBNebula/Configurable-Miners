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
	private bool TrySelectMinerFromLook(out object? miner)
	{
		return TrySelectMinerFromLook(_selectDistance, out miner);
	}

	private bool TrySelectMinerForInteraction(out object? miner)
	{
		if (TrySelectMinerFromLook(_selectDistance, out miner)) return true;
		if (_hoverDistance > _selectDistance + 0.001f)
		{
			return TrySelectMinerFromLook(_hoverDistance, out miner);
		}
		miner = null;
		return false;
	}

	private bool TrySelectMinerFromLook(float distance, out object? miner)
	{
		miner = null;
		var cam = Camera.main;
		if (cam == null) return false;

		if (!Physics.Raycast(cam.transform.position, cam.transform.forward, out var hit, distance, ~0, QueryTriggerInteraction.Ignore))
		{
			return false;
		}

		var current = hit.transform;
		while (current != null)
		{
			var mb = current.GetComponent<MonoBehaviour>();
			if (mb != null && IsAutoMinerType(mb.GetType()))
			{
				if (!IsMinerWithinSelectionCone(cam, mb.transform.position, distance))
				{
					current = current.parent;
					continue;
				}
				miner = mb;
				return true;
			}

			var all = current.GetComponents<MonoBehaviour>();
			for (var i = 0; i < all.Length; i++)
			{
				if (all[i] == null) continue;
				if (!IsAutoMinerType(all[i].GetType())) continue;
				if (!IsMinerWithinSelectionCone(cam, all[i].transform.position, distance)) continue;
				miner = all[i];
				return true;
			}

			current = current.parent;
		}

		return false;
	}

	private static bool IsMinerWithinSelectionCone(Camera cam, Vector3 minerPosition, float maxDistance)
	{
		var toMiner = minerPosition - cam.transform.position;
		var sqrDistance = toMiner.sqrMagnitude;
		if (sqrDistance <= 0.0001f) return true;
		if (sqrDistance > (maxDistance * maxDistance)) return false;
		var direction = toMiner.normalized;
		var dot = Vector3.Dot(cam.transform.forward, direction);
		// Require miner to be reasonably centered in view to avoid side selections.
		return dot >= 0.9f;
	}

	private static bool IsAutoMinerType(Type type)
	{
		var t = type;
		while (t != null)
		{
			var typeName = t.Name;
			if (string.Equals(typeName, "AutoMiner", StringComparison.Ordinal)) return true;
			if (string.Equals(typeName, "RapidAutoMiner", StringComparison.Ordinal)) return true;
			if (typeName.IndexOf("AutoMiner", StringComparison.OrdinalIgnoreCase) >= 0) return true;
			t = t.BaseType;
		}
		return false;
	}

	private void BindSelectedMiner(object miner)
	{
		TryResolveSavePaths(createFolderIfMissing: false);
		if (_savedEntries.Count == 0 && !string.IsNullOrWhiteSpace(_saveDataFilePath) && System.IO.File.Exists(_saveDataFilePath))
		{
			LoadSaveDataFromDisk();
		}

		_selectedMiner = miner;
		_selectedMinerLabel = ReadMinerLabel(miner);
		_selectedMinerPos = ReadMinerPosition(miner);
		LogDebug($"BindSelectedMiner: name={_selectedMinerLabel} pos={_selectedMinerPos}");

		var existing = FindSavedEntryForPosition(_selectedMinerPos) ?? TryGetConfigForMiner(miner);
		if (existing != null)
		{
			_edit = CloneConfig(existing!);
		}
		else
		{
			_edit = BuildDefaultConfigFromMiner(miner);
		}
		RefreshOutputOptionsForCurrentFilter();
		_editDirty = false;
	}

	private string ReadMinerLabel(object miner)
	{
		var go = miner as MonoBehaviour;
		if (go != null) return go.name;
		return miner.GetType().Name;
	}
}
