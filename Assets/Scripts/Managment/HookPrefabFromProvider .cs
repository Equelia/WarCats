using UnityEngine;

[RequireComponent(typeof(PrefabIconRenderer))]
public class HookPrefabFromProvider : MonoBehaviour
{
	public MonoBehaviour provider; // должен реализовывать IUnitDeckProvider
	public int entryIndex = 0;

	private void Start()
	{
		var r = GetComponent<PrefabIconRenderer>();
		if (!provider) return;

		var deck = provider as IUnitDeckProvider;
		if (deck == null) return;

		if (deck.TryGetPrefab(entryIndex, out var unitCtrl) && unitCtrl)
		{
			var prefab = unitCtrl.gameObject;
			if (prefab) r.RenderPrefab(prefab);
		}
	}
}