using System;
using UnityEngine;
using Units.Logic;

[CreateAssetMenu(menuName = "Game/Unit Deck", fileName = "UnitDeck")]
public class UnitDeckSO : ScriptableObject
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
}