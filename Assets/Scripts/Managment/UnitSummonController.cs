using System;
using Cysharp.Threading.Tasks;
using Units.Data;
using Units.Logic;
using UnityEngine;
using Zenject;

[DisallowMultipleComponent]
public class UnitSummonController : MonoBehaviour
{
    public int teamId = 0;
    public int levelOverride = 0;

    [Header("Spawn config")]
    [Tooltip("Explicit spawn point (optional).")]
    public Transform spawnPoint;

    [Tooltip("Preferred areas to sample spawn poses from (team-filtered).")]
    public SpawnAreaBase[] spawnAreas;

    [Tooltip("Random spawn radius near base if spawnPoint/areas are not set.")]
    public float spawnRadius = 4f;

    // Injectables / data
    private UnitController _prefab;   // передаём напрямую префаб UnitController
    private IArmyEconomy _army;
    private UnitSpawner _spawner;
    private ITeamBaseProvider _baseProvider;
    private UnitData _unitData;

    // Cost / stock
    public int SpawnCost { get; private set; }
    public bool HasStockLimit { get; private set; }
    public UnitData.StockMode StockMode { get; private set; }
    public int StockCurrent { get; private set; }
    public int StockMax { get; private set; }

    public bool IsRefillingToMax { get; private set; }
    private float _refillCooldownSec;

    // Events for UI
    public event Action<int, int> OnStockChanged;    // (current, max)
    public event Action<bool> OnRefillActiveChanged; // true when refill running
    public event Action<float> OnRefillProgress;     // 1..0 fill (mask)

    [Inject(Optional = true)] private DiContainer _container;
    private bool _configured;

    // -------------------- Public API --------------------

    public void Configure(
        UnitController prefab,
        IArmyEconomy army,
        UnitSpawner spawner,
        ITeamBaseProvider baseProvider = null,
        Transform explicitSpawnPoint = null,
        SpawnAreaBase[] areas = null,
        int? forceTeamId = null,
        int? forceLevel = null,
        float? forceSpawnRadius = null)
    {
        _prefab       = prefab;
        _army         = army;
        _spawner      = spawner;
        _baseProvider = baseProvider ?? _baseProvider;

        if (explicitSpawnPoint)   spawnPoint = explicitSpawnPoint;
        if (areas != null)        spawnAreas = areas;
        if (forceTeamId.HasValue) teamId     = forceTeamId.Value;
        if (forceLevel.HasValue)  levelOverride = forceLevel.Value;
        if (forceSpawnRadius.HasValue) spawnRadius = forceSpawnRadius.Value;

        if (_prefab == null || _army == null || _spawner == null)
        {
            _configured = false;
            return;
        }

        var uc = _prefab.GetComponent<UnitController>();
        if (uc == null)
        {
            _configured = false;
            return;
        }

        // Берём данные из публичного геттера
        _unitData = uc.UnitDataAsset;

        if (_unitData == null)
        {
            _configured = false;
            return;
        }

        // Cost
        int lvlForStats = Mathf.Max(1, levelOverride > 0 ? levelOverride : _army.State.level);
        var stats = _unitData.GetStatsForLevel(lvlForStats);
        SpawnCost = stats.spawnCost;

        // Stock init
        StockMode = _unitData.stockMode;
        switch (StockMode)
        {
            case UnitData.StockMode.Unlimited:
                HasStockLimit      = false;
                StockCurrent       = int.MaxValue;
                StockMax           = int.MaxValue;
                _refillCooldownSec = 0f;
                IsRefillingToMax   = false;
                break;

            case UnitData.StockMode.LimitedNoRecharge:
                HasStockLimit      = true;
                StockCurrent       = Mathf.Max(0, _unitData.startingStock);
                StockMax           = Mathf.Max(0, _unitData.maxStock);
                _refillCooldownSec = 0f;
                IsRefillingToMax   = false;
                break;

            case UnitData.StockMode.LimitedWithRecharge:
                HasStockLimit      = true;
                StockCurrent       = Mathf.Max(0, _unitData.startingStock);
                StockMax           = Mathf.Max(StockCurrent, _unitData.maxStock);
                _refillCooldownSec = Mathf.Max(0.01f, _unitData.rechargeCooldown);
                IsRefillingToMax   = false;

                if (StockCurrent <= 0)
                    StartRefillToMaxIfNeeded().Forget();
                break;
        }

        _configured = true;
        RaiseStock();
    }

    public bool CanSummon =>
        _configured &&
        (!HasStockLimit || StockCurrent > 0) &&
        !(StockMode == UnitData.StockMode.LimitedWithRecharge && IsRefillingToMax);

    /// <summary>Try to summon one unit or a group. Handles slots, credits, stock, pose, spawn.</summary>
    public bool TrySummon()
    {
        if (!CanSummon) return false;

        // 1) Reserve slot
        if (!_army.TryReserveSlot()) return false;

        // 2) Credits
        if (!_army.TrySpendCredits(SpawnCost))
        {
            _army.ReleaseSlot();
            return false;
        }

        // 3) Stock
        if (HasStockLimit)
        {
            StockCurrent = Mathf.Max(0, StockCurrent - 1);
            RaiseStock();
        }

        // 4) Pose
        if (!TryComputeSpawnPose(out var pos, out var rot))
        {
            if (HasStockLimit) { StockCurrent++; RaiseStock(); }
            _army.AddCredits(SpawnCost);
            _army.ReleaseSlot();
            Debug.LogWarning("UnitSummonController: failed to compute spawn pose. Rolled back.");
            return false;
        }

        // 5) Spawn
        int levelToUse = Mathf.Max(1, levelOverride > 0 ? levelOverride : _army.State.level);
        int spawnCount = GetSpawnCountForLevelFromData(levelToUse);

        // одиночный спавн (как раньше)
        if (spawnCount <= 1)
        {
            var unit = _spawner.SpawnUnitAt(
                teamId: teamId,
                level: levelToUse,
                position: pos,
                rotation: rot,
                prefab: _prefab,
                explicitEnemyBase: null,
                upstreamAlreadyReserved: true
            );

            if (!unit)
            {
                if (HasStockLimit) { StockCurrent++; RaiseStock(); }
                _army.AddCredits(SpawnCost);
                _army.ReleaseSlot();
                Debug.LogWarning("UnitSummonController: spawn failed after reservation. Rolled back.");
                return false;
            }

            // Пистолетчик сам держит слот (сквод); одиночные — через ReleaseSlotOnDestroy
            bool isPistolier = unit is Units.Logic.PistolierLogic;
            if (!isPistolier)
            {
                if (unit.GetComponent<UnitDeathReporter>() == null)
                    unit.gameObject.AddComponent<UnitDeathReporter>();
                var rel = unit.gameObject.AddComponent<ReleaseSlotOnDestroy>();
                rel.Init(_army);
            }
        }
        else
        {
            // Группа: один "тикет" на всю группу, слот освобождается,
            // когда умрёт последний участник
            var ticketGO = new GameObject($"SquadTicket_{_prefab.name}");
            var ticket = ticketGO.AddComponent<GroupSlotTicket>();
            ticket.Init(_army);

            int spawned = 0;
            float radius = Mathf.Max(0.3f, spawnRadius * 0.5f);

            for (int k = 0; k < spawnCount; k++)
            {
                // небольшое разведение вокруг базовой точки
                Vector2 c = UnityEngine.Random.insideUnitCircle * radius;
                Vector3 p = new Vector3(pos.x + c.x, pos.y, pos.z + c.y);

                var unit = _spawner.SpawnUnitAt(
                    teamId: teamId,
                    level: levelToUse,
                    position: p,
                    rotation: rot,
                    prefab: _prefab,
                    explicitEnemyBase: null,
                    upstreamAlreadyReserved: true
                );

                if (!unit) continue;

                // каждый участник сообщает тикету о своей смерти
                if (unit.GetComponent<UnitDeathReporter>() == null)
                    unit.gameObject.AddComponent<UnitDeathReporter>();
                var n = unit.gameObject.AddComponent<NotifyGroupOnDestroy>();
                n.Init(ticket);

                ticket.RegisterMember();
                spawned++;
            }

            // если никого не заспавнили — полный откат
            if (spawned == 0)
            {
                if (HasStockLimit) { StockCurrent++; RaiseStock(); }
                _army.AddCredits(SpawnCost);
                _army.ReleaseSlot();
                Destroy(ticketGO);
                Debug.LogWarning("UnitSummonController: group spawn failed. Rolled back.");
                return false;
            }
        }

        // 6) Start BIG refill if needed
        if (StockMode == UnitData.StockMode.LimitedWithRecharge && StockCurrent == 0)
            StartRefillToMaxIfNeeded().Forget();

        return true;
    }

    public void SetStockMax(int newMax)
    {
        if (!HasStockLimit) return;
        StockMax = Mathf.Max(0, newMax);
        StockCurrent = Mathf.Min(StockCurrent, StockMax);
        RaiseStock();
        if (StockMode == UnitData.StockMode.LimitedWithRecharge && StockCurrent <= 0)
            StartRefillToMaxIfNeeded().Forget();
    }

    public void AddStock(int amount)
    {
        if (!HasStockLimit || amount <= 0) return;
        StockCurrent = Mathf.Min(StockCurrent + amount, StockMax);
        RaiseStock();
    }

    // -------------------- Internals --------------------

    private void RaiseStock()
    {
        OnStockChanged?.Invoke(HasStockLimit ? Mathf.Max(0, StockCurrent) : -1,
                               HasStockLimit ? Mathf.Max(0, StockMax)    : -1);
    }

    private async UniTaskVoid StartRefillToMaxIfNeeded()
    {
        if (StockMode != UnitData.StockMode.LimitedWithRecharge) return;
        if (IsRefillingToMax) return;
        if (StockCurrent > 0) return;

        IsRefillingToMax = true;
        OnRefillActiveChanged?.Invoke(true);
        OnRefillProgress?.Invoke(1f);

        float t = 0f;
        while (t < _refillCooldownSec && enabled && gameObject.activeInHierarchy)
        {
            await UniTask.Yield();
            t += Time.deltaTime;
            OnRefillProgress?.Invoke(1f - Mathf.Clamp01(t / _refillCooldownSec));
        }

        StockCurrent = StockMax;
        IsRefillingToMax = false;

        OnRefillProgress?.Invoke(0f);
        OnRefillActiveChanged?.Invoke(false);
        RaiseStock();
    }

    private bool TryComputeSpawnPose(out Vector3 pos, out Quaternion rot)
    {
        // 1) Prefer Spawn Areas if provided (team-matched)
        if (spawnAreas != null && spawnAreas.Length > 0)
        {
            const int attempts = 16;
            for (int i = 0; i < attempts; i++)
            {
                var area = PickRandomAreaForTeam(teamId);
                if (area != null && area.TryGetRandomPose(out pos, out rot))
                    return true;
            }
        }

        // 2) Explicit spawn transform
        if (spawnPoint != null)
        {
            pos = spawnPoint.position;
            rot = spawnPoint.rotation;
            return true;
        }

        // 3) Base-provider fallback
        Vector3 p = _baseProvider != null
            ? _baseProvider.GetRandomSpawnPointNearBase(teamId, spawnRadius)
            : transform.position;

        pos = p;

        var enemyBase = _baseProvider?.GetOpposingBaseTransform(teamId);
        Vector3 fwd = enemyBase ? (enemyBase.position - pos).normalized : Vector3.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
        rot = Quaternion.LookRotation(fwd, Vector3.up);
        return true;
    }

    private SpawnAreaBase PickRandomAreaForTeam(int tid)
    {
        if (spawnAreas == null || spawnAreas.Length == 0) return null;

        int count = 0;
        for (int i = 0; i < spawnAreas.Length; i++)
        {
            var a = spawnAreas[i];
            if (!a) continue;
            if (a.teamId == -1 || a.teamId == tid) count++;
        }
        if (count == 0) return null;

        int pick = UnityEngine.Random.Range(0, count);
        for (int i = 0, seen = 0; i < spawnAreas.Length; i++)
        {
            var a = spawnAreas[i];
            if (!a) continue;
            if (a.teamId == -1 || a.teamId == tid)
            {
                if (seen == pick) return a;
                seen++;
            }
        }
        return null;
    }

    // --- helpers for multi-spawn ---

    private int GetSpawnCountForLevelFromData(int level)
    {
        if (_prefab is Units.Logic.PistolierLogic)
            return 1;

        if (_unitData is Units.Data.RocketData rd)
            return Mathf.Max(1, rd.GetSpawnCountForLevel(level));

        return 1;
    }

    private sealed class ReleaseSlotOnDestroy : MonoBehaviour
    {
        private IArmyEconomy _army;
        public void Init(IArmyEconomy a) => _army = a;
        private void OnDestroy() => _army?.ReleaseSlot();
    }

    private sealed class GroupSlotTicket : MonoBehaviour
    {
        private IArmyEconomy _army;
        private int _alive;

        public void Init(IArmyEconomy a) => _army = a;

        public void RegisterMember() => _alive++;

        public void OnMemberDestroyed()
        {
            _alive--;
            if (_alive <= 0)
            {
                _army?.ReleaseSlot();
                Destroy(gameObject);
            }
        }
    }

    private sealed class NotifyGroupOnDestroy : MonoBehaviour
    {
        private GroupSlotTicket _ticket;
        public void Init(GroupSlotTicket t) => _ticket = t;
        private void OnDestroy() => _ticket?.OnMemberDestroyed();
    }
}
