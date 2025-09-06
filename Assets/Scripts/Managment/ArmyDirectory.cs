using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-200)]
public class ArmyDirectory : MonoBehaviour
{
	private static ArmyDirectory _instance;
	private readonly Dictionary<int, IArmyEconomy> _byTeam = new();

	private void Awake()
	{
		if (_instance && _instance != this)
		{
			Destroy(gameObject);
			return;
		}
		_instance = this;
	}

	public static void Register(IArmyEconomy army)
	{
		if (_instance == null) return;
		_instance._byTeam[army.StateLevelOwnerTeamId] = army;
	}

	public static void Unregister(IArmyEconomy army)
	{
		if (_instance == null) return;
		if (_instance._byTeam.TryGetValue(army.StateLevelOwnerTeamId, out var a) && a == army)
			_instance._byTeam.Remove(army.StateLevelOwnerTeamId);
	}

	public static IArmyEconomy Get(int teamId)
	{
		if (_instance == null) return null;
		_instance._byTeam.TryGetValue(teamId, out var a);
		return a;
	}
}