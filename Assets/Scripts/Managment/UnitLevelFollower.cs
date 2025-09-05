using UnityEngine;
using Units.Logic;

[RequireComponent(typeof(UnitController))]
public class UnitLevelFollower : MonoBehaviour
{
	private UnitController _uc;
	private IArmyEconomy _army;

	private void Awake()
	{
		_uc = GetComponent<UnitController>();
	}

	private void Start()
	{
		// Find army by this unit's team
		_army = ArmyDirectory.Get(_uc.TeamId);
		if (_army == null)
		{
			Debug.LogWarning($"{name}: Army not found for team {_uc.TeamId}. Level won't auto-sync.");
			return;
		}

		// Set initial level to army level
		var lvl = Mathf.Max(1, _army.State.level);
		_uc.SetLevel(lvl);

		// Subscribe to future level changes
		_army.OnLevelChanged += HandleArmyLevelChanged;
	}

	private void OnDestroy()
	{
		if (_army != null)
			_army.OnLevelChanged -= HandleArmyLevelChanged;
	}

	private void HandleArmyLevelChanged(int newLevel)
	{
		_uc.SetLevel(Mathf.Max(1, newLevel));
	}
}