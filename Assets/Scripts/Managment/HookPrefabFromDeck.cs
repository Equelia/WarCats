using UnityEngine;

[RequireComponent(typeof(PrefabIconRenderer))]
public class HookPrefabFromDeck : MonoBehaviour
{
	public UnitDeckSO deck;
	public int entryIndex = 0;

	private void Start()
	{
		var r = GetComponent<PrefabIconRenderer>();
		if (!deck || deck.entries == null || deck.entries.Length <= entryIndex) return;
		var prefab = deck.entries[entryIndex].unitPrefab ? deck.entries[entryIndex].unitPrefab.gameObject : null;
		if (prefab) r.RenderPrefab(prefab);
	}
}