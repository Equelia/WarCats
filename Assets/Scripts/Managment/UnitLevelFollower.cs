using Cysharp.Threading.Tasks;
using UnityEngine;
using Units.Logic;
using Zenject;

public class UnitLevelFollower : MonoBehaviour
{
	private UnitController _unit;
	private IArmyEconomy _army;

	private async void Start()
	{
		// Wait one frame so Bootstrapper can finish Setup/Initialize
		await UniTask.Yield();

		_unit = GetComponent<UnitController>() ?? GetComponentInChildren<UnitController>();
		if (_unit == null) { Debug.LogWarning($"{name}: UnitController not found."); return; }

		// Wait until ArmyDirectory has the army for this team (up to 1s)
		float t = 0f;
		while ((_army = ArmyDirectory.Get(_unit.TeamId)) == null && t < 1f)
		{
			await UniTask.Yield();
			t += Time.deltaTime;
		}
		if (_army == null) { Debug.LogWarning($"{name}: Army not found for team {_unit.TeamId}."); return; }

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