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
public UnitController SpawnUnitAt(int teamId, int level, Vector3 position, Quaternion rotation, UnitController prefab, Transform explicitEnemyBase = null, bool upstreamAlreadyReserved = true) 
{
    // Normal units: single spawn, no slot ops here.
    if (prefab is not Units.Logic.PistolierLogic)
    {
        var unit = _container.InstantiatePrefabForComponent<UnitController>(
            prefab.gameObject, position, rotation, unitsRoot);
        unit.Initialize(teamId, initLevel: level, explicitEnemyBase: explicitEnemyBase);

        var follower = unit.GetComponent<UnitLevelFollower>();
        if (follower) follower.BindArmy(teamId);

        return unit;
    }

    // --- Pistolier squad path (1 slot for the whole squad;) ---
    var economy = ArmyDirectory.Get(teamId);
    if (economy == null)
    {
        Debug.LogWarning($"SpawnUnitAt: ArmyEconomy not found for team {teamId}. Spawning single pistolier without squad ticket.");
        var fallback = _container.InstantiatePrefabForComponent<UnitController>(
            prefab.gameObject, position, rotation, unitsRoot);
        fallback.Initialize(teamId, initLevel: Mathf.Clamp(level, 1, 3), explicitEnemyBase: explicitEnemyBase);

        var followerFallback = fallback.GetComponent<UnitLevelFollower>();
        if (followerFallback) followerFallback.BindArmy(teamId);
        return fallback;
    }

    // Require that the caller (SummonButton) has already reserved a slot
    if (!upstreamAlreadyReserved)
    {
        Debug.LogWarning("SpawnUnitAt: expected upstream reservation (SummonButton). Proceeding without reserving here.");
    }

    int armyLevel = Mathf.Clamp(economy.State.level, 1, 3);
    var pistolierData = prefab.UnitDataAsset as Units.Data.PistolierData;
    int count = pistolierData != null ? pistolierData.GetSpawnCountForLevel(armyLevel) : 1;

    // Per-squad ticket (no reservation here): only tracks members and releases once all dead
    var ticketGO = new GameObject($"SquadTicket_T{teamId}_{System.Guid.NewGuid()}");
    ticketGO.transform.SetParent(unitsRoot != null ? unitsRoot : transform, false);
    var ticket = ticketGO.AddComponent<SquadSlotTicket>();
    ticket.Init(economy);
    ticket.ForceMarkReserved(); // mark as already reserved upstream

    static Vector3 Offset(int i)
    {
        return i switch
        {
            0 => Vector3.zero,
            1 => new Vector3(0.5f, 0f, 0.4f),
            2 => new Vector3(-0.5f, 0f, 0.4f),
            _ => Quaternion.Euler(0, (i - 1) * (360f / 6f), 0) * new Vector3(0.6f, 0f, 0.35f)
        };
    }

    UnitController leader = null;

    for (int i = 0; i < count; i++)
    {
        var spawnPos = position + rotation * Offset(i);
        var unit = _container.InstantiatePrefabForComponent<UnitController>(
            prefab.gameObject, spawnPos, rotation, unitsRoot);

        unit.Initialize(teamId, initLevel: armyLevel, explicitEnemyBase: explicitEnemyBase);

        var follower = unit.GetComponent<UnitLevelFollower>();
        if (follower) follower.BindArmy(teamId);

        ticket.RegisterMember(unit.gameObject, this);
        if (leader == null) leader = unit;
    }

    return leader;
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