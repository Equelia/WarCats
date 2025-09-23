using System;

public static class UnitEvents
{
	public static event Action<int> OnUnitDied;         // уже было
	public static event Action<int> OnBaseDestroyed;    // teamId уничтоженной базы
	public static event Action<int> OnGameOver;         // teamId победителя

	public static void RaiseUnitDied(int teamId) => OnUnitDied?.Invoke(teamId);

	public static void RaiseBaseDestroyed(int destroyedTeamId)
		=> OnBaseDestroyed?.Invoke(destroyedTeamId);

	public static void RaiseGameOver(int winnerTeamId)
		=> OnGameOver?.Invoke(winnerTeamId);
}