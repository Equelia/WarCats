using System;
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
	[SerializeField] private Transform unitsRoot; // optional: keeps hierarchy clean

	public UnitController SpawnUnit(int teamId, int level, Transform spawnPoint, UnitController prefab, Transform explicitEnemyBase = null)
	{
		// Instantiate via Zenject so injected services are available in Awake()
		var unit = _container.InstantiatePrefabForComponent<UnitController>(
			prefab.gameObject,
			spawnPoint.position,
			spawnPoint.rotation,
			unitsRoot // prefer a neutral root instead of making the spawnPoint the parent
		);

		// Configure gameplay parameters AFTER Awake ran
		unit.Initialize(teamId, initLevel: level, explicitEnemyBase: explicitEnemyBase);
		return unit;
	}

	private void Update()
	{
		if (Input.GetKeyDown(KeyCode.Q))
			SpawnUnit(0, 1, alliesSpawnPoint, allyPrefab);

		if (Input.GetKeyDown(KeyCode.E))
			SpawnUnit(1, 1, enemySpawnPoint, enemyPrefab);
	}
}