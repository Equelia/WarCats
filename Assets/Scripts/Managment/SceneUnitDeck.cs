using System;
using UnityEngine;

public interface IUnitDeckProvider
{
	int VisibleSlots { get; }
	bool TryGetArchetype(int index, out UnitArchetype archetype);
}

[DisallowMultipleComponent]
public class SceneUnitDeck : MonoBehaviour, IUnitDeckProvider
{
	[Serializable]
	public struct Entry
	{
		[Tooltip("Archetype (SO) of the unit")]
		public UnitArchetype archetype;
	}

	[Range(1, 6)]
	public int visibleSlots = 6;

	[Tooltip("Up to 6 unit entries that will be summonable via the UI.")]
	public Entry[] entries = new Entry[6];

	public int VisibleSlots => Mathf.Clamp(visibleSlots, 1, entries != null ? entries.Length : 0);

	public bool TryGetArchetype(int index, out UnitArchetype archetype)
	{
		archetype = null;
		if (entries == null) return false;
		if (index < 0 || index >= entries.Length) return false;
		archetype = entries[index].archetype;
		return archetype != null;
	}
}