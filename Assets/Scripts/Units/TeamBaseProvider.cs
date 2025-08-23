using UnityEngine;

public interface ITeamBaseProvider
{
	/// <summary>
	/// Returns opposing base transform for given team id.
	/// Convention: 0 = player, 1 = enemy (adjust if you use other scheme).
	/// Returns null if not assigned.
	/// </summary>
	Transform GetOpposingBaseTransform(int teamId);
}


/// <summary>
/// Scene component that holds references to team bases.
/// Place this component on a manager GameObject in the scene and assign transforms in inspector.
/// </summary>
public class TeamBaseProvider : MonoBehaviour, ITeamBaseProvider
{
	[Tooltip("Transform of player base (team 0).")]
	public Transform playerBase;

	[Tooltip("Transform of enemy base (team 1).")]
	public Transform enemyBase;

	public Transform GetOpposingBaseTransform(int teamId)
	{
		if (teamId == 0) return enemyBase;
		if (teamId == 1) return playerBase;
		// fallback
		return enemyBase;
	}
}