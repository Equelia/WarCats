using Cysharp.Threading.Tasks;
using TMPro;
using Units.Data;
using Units.Logic;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

[DisallowMultipleComponent]
public class SummonButton : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Button button;
    [SerializeField] private Image cooldownMask;  
    [SerializeField] private TMP_Text costText;
    [SerializeField] private GameObject iconObject;

    [Header("Stock UI")]
    [Tooltip("Shows only the current available amount (e.g., \"3\").")]
    [SerializeField] private TMP_Text stockText;

    [Header("Spawn Areas (optional)")]
    [Tooltip("Preferred areas to sample spawn poses from (team-filtered).")]
    [SerializeField] private SpawnAreaBase[] spawnAreas;

    [HideInInspector] public int teamId = 0;
    [HideInInspector] public int levelOverride = 0;

    private Transform spawnPoint;

    [Tooltip("Random spawn radius near base if spawnPoint/areas are not set.")]
    [SerializeField] private float spawnRadius = 4f;

    // Injected at runtime by manager:
    private UnitDeckSO.Entry _entry;
    private IArmyEconomy _army;
    private UnitSpawner _spawner;
    [Inject(Optional = true)] private ITeamBaseProvider _baseProvider;

    // Cached unit data / state
    private bool _configured;
    private UnitData _unitData;

    private int _spawnCost;

    // Stock state
    private bool _hasStockLimit;
    private UnitData.StockMode _stockMode;
    private int _stockCurrent;
    private int _stockMax;

    // BIG refill (LimitedWithRecharge)
    private bool _isRefillingToMax;
    private float _refillCooldownSec; // uses UnitData.rechargeSecondsPerUnit as "full refill" time

    private void Awake()
    {
        if (!button) button = GetComponent<Button>();
        if (cooldownMask) cooldownMask.fillAmount = 0f;

        button.onClick.AddListener(OnClick);

        SetActiveState(false);
        UpdateStockUI();
    }

    private void OnDestroy()
    {
        button.onClick.RemoveListener(OnClick);
    }

    public void Configure(
        UnitDeckSO.Entry entry,
        IArmyEconomy army,
        UnitSpawner spawner,
        ITeamBaseProvider baseProvider = null,
        Transform explicitSpawnPoint = null,
        int? forceTeamId = null)
    {
        _entry = entry;
        _army = army;
        _spawner = spawner;
        if (baseProvider != null) _baseProvider = baseProvider;
        if (explicitSpawnPoint) spawnPoint = explicitSpawnPoint;
        if (forceTeamId.HasValue) teamId = forceTeamId.Value;

        var uc = _entry.unitPrefab ? _entry.unitPrefab.GetComponent<UnitController>() : null;
        if (uc == null)
        {
            SetActiveState(false);
            _configured = false;
            return;
        }

        _unitData = uc.GetType()
            .GetField("unitData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(uc) as UnitData;

        if (_unitData == null)
        {
            SetActiveState(false);
            _configured = false;
            return;
        }

        // Cost (no per-press cooldown anymore)
        int lvlForStats = Mathf.Max(1, levelOverride > 0 ? levelOverride : (_army != null ? _army.State.level : 1));
        var stats = _unitData.GetStatsForLevel(lvlForStats);
        _spawnCost = stats.spawnCost;

        // Stock init
        _stockMode = _unitData.stockMode;
        switch (_stockMode)
        {
            case UnitData.StockMode.Unlimited:
                _hasStockLimit = false;
                _stockCurrent  = int.MaxValue;
                _stockMax      = int.MaxValue;
                _refillCooldownSec = 0f;
                _isRefillingToMax = false;
                break;

            case UnitData.StockMode.LimitedNoRecharge:
                _hasStockLimit = true;
                _stockCurrent  = Mathf.Max(0, _unitData.startingStock);
                _stockMax      = Mathf.Max(0, _unitData.maxStock);
                _refillCooldownSec = 0f;
                _isRefillingToMax = false;
                break;

            case UnitData.StockMode.LimitedWithRecharge:
                _hasStockLimit = true;
                _stockCurrent  = Mathf.Max(0, _unitData.startingStock);
                _stockMax      = Mathf.Max(_stockCurrent, _unitData.maxStock); // ensure max >= start
                _refillCooldownSec = Mathf.Max(0.01f, _unitData.rechargeCooldown); // treat as "full refill" time
                _isRefillingToMax = false;

                // If starting at zero, immediately begin initial refill
                if (_stockCurrent <= 0)
                    StartRefillToMaxIfNeeded().Forget();
                break;
        }

        // UI
        if (costText)
        {
            costText.text = _spawnCost.ToString();
            costText.gameObject.SetActive(true);
            if (iconObject) iconObject.SetActive(true);
        }
        if (cooldownMask) cooldownMask.fillAmount = 0f;

        UpdateStockUI();
        SetActiveState(true);
        _configured = true;
    }

    public void Clear()
    {
        _configured = false;
        _entry = default;
        _army = null;
        _spawner = null;
        _unitData = null;

        _spawnCost = 0;

        _hasStockLimit = false;
        _stockMode = UnitData.StockMode.Unlimited;
        _stockCurrent = 0;
        _stockMax = 0;

        _isRefillingToMax = false;
        _refillCooldownSec = 0f;

        if (costText)
        {
            costText.text = string.Empty;
            costText.gameObject.SetActive(false);
            if (iconObject) iconObject.SetActive(false);
        }
        if (cooldownMask) cooldownMask.fillAmount = 0f;

        UpdateStockUI();
        SetActiveState(false);
    }

    private void SetActiveState(bool enabledState)
    {
        bool canSummonByStock = !_hasStockLimit || _stockCurrent > 0;
        bool blockedByRefill  = _stockMode == UnitData.StockMode.LimitedWithRecharge && _isRefillingToMax;

        if (button) button.interactable = enabledState && canSummonByStock && !blockedByRefill;

        var cg = GetComponent<CanvasGroup>();
        if (!cg) cg = gameObject.AddComponent<CanvasGroup>();
        cg.alpha = enabledState ? 1f : 0.45f;
    }

    private void UpdateStockUI()
    {
        if (!stockText) return;

        if (!_hasStockLimit)
        {
            stockText.gameObject.SetActive(false);
        }
        else
        {
            stockText.gameObject.SetActive(true);
            stockText.text = Mathf.Max(0, _stockCurrent).ToString(); // show only current available
        }

        SetActiveState(true);
    }

    private async UniTaskVoid StartRefillToMaxIfNeeded()
    {
        if (_stockMode != UnitData.StockMode.LimitedWithRecharge) return;
        if (_isRefillingToMax) return;
        if (_stockCurrent > 0) return;

        _isRefillingToMax = true;
        SetActiveState(true); // disable button while refilling

        if (cooldownMask) cooldownMask.fillAmount = 1f;

        float t = 0f;
        while (t < _refillCooldownSec && enabled && gameObject.activeInHierarchy)
        {
            await UniTask.Yield();
            t += Time.deltaTime;
            if (cooldownMask) cooldownMask.fillAmount = 1f - Mathf.Clamp01(t / _refillCooldownSec);
        }

        // Finish refill to MAX
        _stockCurrent = _stockMax;
        _isRefillingToMax = false;

        if (cooldownMask) cooldownMask.fillAmount = 0f;

        UpdateStockUI();
        SetActiveState(true);
    }

    private void OnClick()
    {
        if (!_configured) return;
        if (_army == null || _spawner == null || _entry.unitPrefab == null) return;

        // Blocks
        bool noStock   = _hasStockLimit && _stockCurrent <= 0;
        bool refilling = _stockMode == UnitData.StockMode.LimitedWithRecharge && _isRefillingToMax;
        if (noStock || refilling) return;

        // 1) Reserve ONE slot here (only here)
        if (!_army.TryReserveSlot()) return;

        // 2) Credits
        if (!_army.TrySpendCredits(_spawnCost))
        {
            _army.ReleaseSlot();
            return;
        }

        // 3) Spend ONE stock (if limited)
        if (_hasStockLimit)
        {
            _stockCurrent = Mathf.Max(0, _stockCurrent - 1);
            UpdateStockUI();
        }

        // 4) Determine spawn pose
        int levelToUse = Mathf.Max(1, levelOverride > 0 ? levelOverride : _army.State.level);
        if (!TryComputeSpawnPose(out Vector3 pos, out Quaternion rot))
        {
            // Rollback all
            if (_hasStockLimit) { _stockCurrent++; UpdateStockUI(); }
            _army.AddCredits(_spawnCost);
            _army.ReleaseSlot();
            Debug.LogWarning("SummonButton: Failed to compute spawn pose. Rolled back.");
            return;
        }

        // 5) Spawn (spawner must NOT reserve slot): upstreamAlreadyReserved = true
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
            // Rollback on failure
            if (_hasStockLimit) { _stockCurrent++; UpdateStockUI(); }
            _army.AddCredits(_spawnCost);
            _army.ReleaseSlot();
            Debug.LogWarning("SummonButton: Spawn failed after reservation. Rolled back.");
            return;
        }

        // 6) Slot release strategy:
        bool isPistolier = unit is Units.Logic.PistolierLogic;
        if (!isPistolier)
        {
            if (unit.GetComponent<UnitDeathReporter>() == null)
                unit.gameObject.AddComponent<UnitDeathReporter>();
            var rel = unit.gameObject.AddComponent<ReleaseSlotOnDestroy>();
            rel.Init(_army);
        }

        // 7) LimitedWithRecharge: if stock hits ZERO → start BIG refill to MAX
        if (_stockMode == UnitData.StockMode.LimitedWithRecharge && _stockCurrent == 0)
        {
            StartRefillToMaxIfNeeded().Forget();
        }
    }

    private bool TryComputeSpawnPose(out Vector3 pos, out Quaternion rot)
    {
        // 1) Prefer Spawn Areas if provided (team-matched)
        if (spawnAreas != null && spawnAreas.Length > 0)
        {
            const int attempts = 16; // retries in case of NavMesh/ground failures
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

    // --- Optional external hooks (e.g., upgrades/rewards) ---

    /// <summary>Sets new MAX stock (e.g., upgrade from 3 to 5). Keeps current clamped.</summary>
    public void SetStockMax(int newMax)
    {
        if (_stockMode == UnitData.StockMode.Unlimited) return;

        _stockMax = Mathf.Max(0, newMax);
        _stockCurrent = Mathf.Min(_stockCurrent, _stockMax);
        UpdateStockUI();

        if (_stockMode == UnitData.StockMode.LimitedWithRecharge && _stockCurrent <= 0)
            StartRefillToMaxIfNeeded().Forget();
    }

    /// <summary>Adds to current stock (clamped to max). Useful for rewards.</summary>
    public void AddStock(int amount)
    {
        if (_stockMode == UnitData.StockMode.Unlimited) return;
        if (amount <= 0) return;

        _stockCurrent = Mathf.Min(_stockCurrent + amount, _stockMax);
        UpdateStockUI();
    }
}
