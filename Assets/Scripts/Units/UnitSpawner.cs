using System;
using Units.Logic;
using Zenject;
using UnityEngine;
using UnityEngine.InputSystem;

public class UnitSpawner : MonoBehaviour
{
	[Inject] DiContainer _container;

	[SerializeField] GameObject  aliePrefab; 
	[SerializeField] GameObject  enemyPrefab; 

	[SerializeField] private Transform aliesSpawnPoint;
	[SerializeField] private Transform enemySpawnPoint;

	public void SpawnUnit(int teamId, int level, Transform spawnPos, GameObject unitPrefab)
	{
		// Instantiate via DiContainer so Zenject can inject dependencies into the component on the prefab.
		var unit = _container.InstantiatePrefabForComponent<UnitLogic>(unitPrefab, spawnPos.position, Quaternion.identity,  spawnPos);
		// we rely on DI to inject baseProvider; Initialize will read baseProvider
		unit.Initialize(teamId, initLevel: level);
	}

	private void Update()
	{
		if (Input.GetKeyDown(KeyCode.Q))
		{
			SpawnUnit(0,1, aliesSpawnPoint, aliePrefab);
		}

		if (Input.GetKeyDown(KeyCode.E))
		{
			SpawnUnit(1,1, enemySpawnPoint, enemyPrefab);
		}
	}
}