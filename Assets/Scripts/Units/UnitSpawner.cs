using System;
using Units.Logic;
using Unity.VisualScripting;
using UnityEngine;
using Zenject;

public class UnitSpawner : MonoBehaviour
{
	private TeamBaseProvider _bases;

	[Header("One universal prefab")]
	[SerializeField] private UnitBootstrapper modularPrefab;

	[Header("Hierarchy")]
	[SerializeField] private Transform unitsRoot;
	
	[Header("Shared spawn areas (like UI)")]
	[SerializeField] private SpawnAreaBase[] spawnAreas;
	[SerializeField] private float fallbackSpawnRadius = 4f;
	

	private void Awake()
	{
		_bases = FindObjectOfType<TeamBaseProvider>();
		if (!_bases) Debug.LogWarning("TeamBaseProvider not found in scene.");
		
	}

	public UnitController Spawn(UnitArchetype arch, int teamId, int level,
		Vector3 pos, Quaternion rot, Transform explicitEnemyBase)
	{
		var go = Instantiate(modularPrefab, pos, rot);
		var boot = go.GetComponent<UnitBootstrapper>();
		var ctrl = boot.Setup(arch, teamId, level, explicitEnemyBase); // ⚡
		return ctrl;
	}

	public UnitArchetype enemySniperArchetype;
	public UnitArchetype enemyPistolArchetype;
	public UnitArchetype enemyAutomataArchetype;
	public UnitArchetype enemyRocketerArchetype;

	private void Update()
	{
		if (Input.GetKeyDown(KeyCode.Q)) 
		{
			if (!TryComputeSpawnPose(0, out var pos, out var rot)) return;
			var enemyBase = _bases ? _bases.GetOpposingBaseTransform(0) : null;

			Spawn(enemyPistolArchetype, teamId: 0, level: 1,
				pos: pos, rot: rot, explicitEnemyBase: enemyBase);
		}

		if (Input.GetKeyDown(KeyCode.E)) 
		{
			if (!TryComputeSpawnPose(1, out var pos, out var rot)) return;
			var enemyBase = _bases ? _bases.GetOpposingBaseTransform(1) : null;

			Spawn(enemyPistolArchetype, teamId: 1, level: 1,
				pos: pos, rot: rot, explicitEnemyBase: enemyBase);
		}
	}
	
	public bool TryComputeSpawnPose(int teamId, out Vector3 pos, out Quaternion rot)
	{
		if (spawnAreas != null && spawnAreas.Length > 0)
		{
			const int attempts = 16;
			for (int i = 0; i < attempts; i++)
			{
				var area = spawnAreas[teamId];
				if (area != null && area.TryGetRandomPose(out pos, out rot))
					return true;
			}
		}

		Vector3 p = _bases ? _bases.GetRandomSpawnPointNearBase(teamId, fallbackSpawnRadius) : transform.position;
		pos = p;

		var enemyBase = _bases ? _bases.GetOpposingBaseTransform(teamId) : null;
		Vector3 fwd = enemyBase ? (enemyBase.position - pos).normalized : Vector3.forward;
		fwd.y = 0f;
		if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
		rot = Quaternion.LookRotation(fwd, Vector3.up);
		return true;
	}
}