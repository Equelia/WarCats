using Units.Logic;
using Zenject;
using UnityEngine;

public class UnitSpawner : MonoBehaviour
{
	[Inject] private DiContainer _container;

	[Header("Prefabs")]
	[SerializeField] private UnitController allyPrefab;
	[SerializeField] private UnitController enemyPrefab;

	[Header("Spawn Points")]
	[SerializeField] private Transform alliesSpawnPoint;
	[SerializeField] private Transform enemySpawnPoint;

	[Header("Hierarchy")]
	[SerializeField] private Transform unitsRoot;

	public UnitController SpawnUnit(int teamId, int level, Transform spawnPoint, UnitController prefab, Transform explicitEnemyBase = null)
	{
		var unit = _container.InstantiatePrefabForComponent<UnitController>(
			prefab.gameObject,
			spawnPoint.position,
			spawnPoint.rotation,
			unitsRoot
		);
		unit.Initialize(teamId, initLevel: level, explicitEnemyBase: explicitEnemyBase);
		return unit;
	}

	/// <summary>Overload: spawn by position/rotation (no Transform needed).</summary>
	public UnitController SpawnUnitAt(int teamId, int level, Vector3 position, Quaternion rotation, UnitController prefab, Transform explicitEnemyBase = null)
	{
		var unit = _container.InstantiatePrefabForComponent<UnitController>(
			prefab.gameObject,
			position,
			rotation,
			unitsRoot
		);
		unit.Initialize(teamId, initLevel: level, explicitEnemyBase: explicitEnemyBase);
		return unit;
	}

	private void Update()
	{
		// test hotkeys
		if (Input.GetKeyDown(KeyCode.Q))
		{
			// spawn ally
			if (allyPrefab && alliesSpawnPoint)
				SpawnUnit(0, 1, alliesSpawnPoint, allyPrefab);
		}

		if (Input.GetKeyDown(KeyCode.E))
		{
			// spawn enemy
			if (enemyPrefab && enemySpawnPoint)
				SpawnUnit(1, 1, enemySpawnPoint, enemyPrefab);
		}
	}
}