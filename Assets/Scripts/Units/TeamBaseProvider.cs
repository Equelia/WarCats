using UnityEngine;
using UnityEngine.AI;

public interface ITeamBaseProvider
{
	/// <summary>Returns opposing base transform for given team id (0 = player, 1 = enemy).</summary>
	Transform GetOpposingBaseTransform(int teamId);

	/// <summary>Returns own base transform for given team id (0 = player, 1 = enemy).</summary>
	Transform GetOwnBaseTransform(int teamId);

	/// <summary>
	/// Returns a random NavMesh position near the team's own base.
	/// </summary>
	Vector3 GetRandomSpawnPointNearBase(int teamId, float radius, int maxTries = 8);
}

/// <summary>
/// Scene component that holds references to team bases.
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
		return enemyBase;
	}

	public Transform GetOwnBaseTransform(int teamId)
	{
		if (teamId == 0) return playerBase;
		if (teamId == 1) return enemyBase;
		return playerBase;
	}

	public Vector3 GetRandomSpawnPointNearBase(int teamId, float radius, int maxTries = 8)
	{
		var baseTf = GetOwnBaseTransform(teamId);
		if (!baseTf) return Vector3.zero;

		// Try sampling NavMesh around base position
		for (int i = 0; i < maxTries; i++)
		{
			var offset = Random.insideUnitCircle * radius;
			var candidate = baseTf.position + new Vector3(offset.x, 0f, offset.y);
			if (NavMesh.SamplePosition(candidate, out var hit, 1.2f, NavMesh.AllAreas))
				return hit.position;
		}
		// Fallback to base position if sampling failed
		return baseTf.position;
	}
}