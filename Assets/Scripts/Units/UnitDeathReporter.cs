using UnityEngine;

/// <summary>
/// Lightweight reporter that raises a global event when the hosting unit GameObject is destroyed.
/// Attach to unit prefabs (or add at runtime) alongside UnitController.
/// </summary>
[DisallowMultipleComponent]
public class UnitDeathReporter : MonoBehaviour
{
	private Units.Logic.UnitController _unit;

	private void Awake()
	{
		_unit = GetComponent<Units.Logic.UnitController>();
		if (_unit == null)
			Debug.LogWarning($"{name}: UnitDeathReporter requires UnitController on the same GameObject.");
	}

	private void OnDestroy()
	{
		// When the unit gets destroyed (usually ~1s after Die()), raise the death event.
		if (_unit != null)
			UnitEvents.RaiseUnitDied(_unit.TeamId);
	}
}