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

    // Injectables
    private UnitDeckSO.Entry _entry;
    private IArmyEconomy _army;
    private UnitSpawner _spawner;
    private ITeamBaseProvider _baseProvider;

    // Data
    private UnitData _unitData;

    // Cost
    public int SpawnCost { get; private set; }

    // Stock state
    public bool HasStockLimit { get; private set; }
    public UnitData.StockMode StockMode { get; private set; }
    public int StockCurrent { get; private set; }
    public int StockMax { get; private set; }

    // BIG refill (LimitedWithRecharge)
    public bool IsRefillingToMax { get; private set; }
    private float _refillCooldownSec;

    // Events for UI
    public event Action<int, int> OnStockChanged;           // (current, max)
    public event Action<bool> OnRefillActiveChanged;        // true when refill running
    public event Action<float> OnRefillProgress;            // 1..0 fill (mask)

    // Helpers
    [Inject(Optional = true)] private DiContainer _container; // for safety (not required)
    private bool _configured;

    // -------------------- Public API --------------------

    public void Configure(
        UnitDeckSO.Entry entry,
        IArmyEconomy army,
        UnitSpawner spawner,
        ITeamBaseProvider baseProvider = null,
        Transform explicitSpawnPoint = null,
        SpawnAreaBase[] areas = null,
        int? forceTeamId = null,
        int? forceLevel = null,
        float? forceSpawnRadius = null)
    {
        _entry        = entry;
        _army         = army;
        _spawner      = spawner;
        _baseProvider = baseProvider ?? _baseProvider;

        if (explicitSpawnPoint) spawnPoint = explicitSpawnPoint;
        if (areas != null)      spawnAreas = areas;
        if (forceTeamId.HasValue) teamId = forceTeamId.Value;
        if (forceLevel.HasValue)  levelOverride = forceLevel.Value;
        if (forceSpawnRadius.HasValue) spawnRadius = forceSpawnRadius.Value;

        var prefabGo = _entry.unitPrefab;
        if (prefabGo == null || _army == null || _spawner == null)
        {
            _configured = false;
            return;
        }

        var uc = prefabGo.GetComponent<UnitController>();
        if (uc == null)
        {
            _configured = false;
            return;
        }

        _unitData = uc.GetType()
            .GetField("unitData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(uc) as UnitData;

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
                HasStockLimit   = false;
                StockCurrent    = int.MaxValue;
                StockMax        = int.MaxValue;
                _refillCooldownSec = 0f;
                IsRefillingToMax   = false;
                break;

            case UnitData.StockMode.LimitedNoRecharge:
                HasStockLimit   = true;
                StockCurrent    = Mathf.Max(0, _unitData.startingStock);
                StockMax        = Mathf.Max(0, _unitData.maxStock);
                _refillCooldownSec = 0f;
                IsRefillingToMax   = false;
                break;

            case UnitData.StockMode.LimitedWithRecharge:
                HasStockLimit   = true;
                StockCurrent    = Mathf.Max(0, _unitData.startingStock);
                StockMax        = Mathf.Max(StockCurrent, _unitData.maxStock);
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

    /// <summary>Try to summon one unit/squad. Handles slots, credits, stock, pose, spawn.</summary>
    public bool TrySummon()
    {
        if (!CanSummon) return false;

        // 1) Reserve slot (ONLY here)
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
            // rollback
            if (HasStockLimit) { StockCurrent++; RaiseStock(); }
            _army.AddCredits(SpawnCost);
            _army.ReleaseSlot();
            Debug.LogWarning("UnitSummonController: failed to compute spawn pose. Rolled back.");
            return false;
        }

        // 5) Spawn (spawner must NOT reserve slot): upstreamAlreadyReserved = true
        int levelToUse = Mathf.Max(1, levelOverride > 0 ? levelOverride : _army.State.level);
        var unit = _spawner.SpawnUnitAt(
            teamId: teamId,
            level: levelToUse,
            position: pos,
            rotation: rot,
            prefab: _entry.unitPrefab,
            explicitEnemyBase: null,
            upstreamAlreadyReserved: true
        );

        if (!unit)
        {
            // rollback
            if (HasStockLimit) { StockCurrent++; RaiseStock(); }
            _army.AddCredits(SpawnCost);
            _army.ReleaseSlot();
            Debug.LogWarning("UnitSummonController: spawn failed after reservation. Rolled back.");
            return false;
        }

        // 6) Slot release strategy: squads (e.g., pistolier) держат слот тикетом; одиночные — через компонент ниже
        bool isPistolier = unit is Units.Logic.PistolierLogic;
        if (!isPistolier)
        {
            if (unit.GetComponent<UnitDeathReporter>() == null)
                unit.gameObject.AddComponent<UnitDeathReporter>();
            var rel = unit.gameObject.AddComponent<ReleaseSlotOnDestroy>();
            rel.Init(_army);
        }

        // 7) BIG refill if ушли в ноль
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
        OnStockChanged?.Invoke(HasStockLimit ? Mathf.Max(0, StockCurrent) : -1, HasStockLimit ? Mathf.Max(0, StockMax) : -1);
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

    private sealed class ReleaseSlotOnDestroy : MonoBehaviour
    {
        private IArmyEconomy _army;
        public void Init(IArmyEconomy a) => _army = a;
        private void OnDestroy() => _army?.ReleaseSlot();
    }
}
