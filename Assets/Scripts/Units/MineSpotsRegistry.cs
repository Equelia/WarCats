using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class MineSpotsRegistry : MonoBehaviour
{
	public static MineSpotsRegistry Instance { get; private set; }
	public List<MineSpot> Spots { get; } = new();

	private void Awake()
	{
		if (Instance && Instance != this) { Destroy(gameObject); return; }
		Instance = this;

		Spots.Clear();
		Spots.AddRange(FindObjectsOfType<MineSpot>(true));
	}

	public void RefreshAll()
	{
		foreach (var s in Spots) if (s) s.RefreshOccupation();
	}

	public void SetMarkersVisible(bool v)
	{
		foreach (var s in Spots) if (s) s.SetMarkerVisible(v);
	}
	
	public MineSpot GetRandomFreeSpotForTeam(int teamId)
	{
		var list = Spots.FindAll(s => s && s.IsForTeam(teamId) && !s.IsOccupied);
		if (list.Count == 0) return null;
		return list[UnityEngine.Random.Range(0, list.Count)];
	}

	public MineSpot GetBestFreeSpotForTeam(int teamId, Transform preferNear)
	{
		var list = Spots.FindAll(s => s && s.IsForTeam(teamId) && !s.IsOccupied);
		if (list.Count == 0) return null;
		if (!preferNear) return list[UnityEngine.Random.Range(0, list.Count)];
		list.Sort((a,b)=>
			((a.transform.position - preferNear.position).sqrMagnitude)
			.CompareTo((b.transform.position - preferNear.position).sqrMagnitude));
		return list[0];
	}
}