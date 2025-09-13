using System;
using UnityEngine;
using Units.Logic;

public interface IUnitDeckProvider
{
	int VisibleSlots { get; }
	bool TryGetPrefab(int index, out UnitController prefab);
}

[DisallowMultipleComponent]
public class SceneUnitDeck : MonoBehaviour, IUnitDeckProvider
{
	[Serializable]
	public struct Entry
	{
		[Tooltip("Prefab with UnitController (ally variant for the player).")]
		public UnitController unitPrefab;
	}

	[Range(1, 6)]
	public int visibleSlots = 6;

	[Tooltip("Up to 6 unit entries that will be summonable via the UI.")]
	public Entry[] entries = new Entry[6];

	public int VisibleSlots => Mathf.Clamp(visibleSlots, 1, entries != null ? entries.Length : 0);

	public bool TryGetPrefab(int index, out UnitController prefab)
	{
		prefab = null;
		if (entries == null) return false;
		if (index < 0 || index >= entries.Length) return false;
		prefab = entries[index].unitPrefab;
		return prefab != null;
	}
}