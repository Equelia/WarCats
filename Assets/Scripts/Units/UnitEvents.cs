using System;

public static class UnitEvents
{
	/// <summary>
	/// Fired when any unit GameObject is destroyed (after death animation).
	/// Argument: teamId of the dead unit.
	/// </summary>
	public static event Action<int> OnUnitDied;

	public static void RaiseUnitDied(int teamId) => OnUnitDied?.Invoke(teamId);
}