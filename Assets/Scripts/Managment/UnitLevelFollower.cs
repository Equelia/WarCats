using UnityEngine;
using Units.Logic;
using Zenject;

[RequireComponent(typeof(UnitController))]
public class UnitLevelFollower : MonoBehaviour
{
	private UnitController _unit;
	private IArmyEconomy _army;

	private void Awake()
	{
		_unit = GetComponent<UnitController>();
	}

	private void Start()
	{
		if (_army == null)
		{
			Debug.LogWarning($"{name}: Army not found for team {_unit.TeamId}. Level won't auto-sync.");
			return;
		}

		_unit.SetLevel(Mathf.Max(1, _army.State.level));

		_army.OnLevelChanged += HandleLevelChanged;
	}

	private void OnDestroy()
	{
		if (_army != null)
			_army.OnLevelChanged -= HandleLevelChanged;
	}

	private void HandleLevelChanged(int newLevel)
	{
		Debug.Log("Set level to unit" + newLevel);
		_unit.SetLevel(Mathf.Max(1, newLevel));
	}

	public void BindArmy(int teamId)
	{
		_army = ArmyDirectory.Get(teamId);
	}
}