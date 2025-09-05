using System;
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

    [HideInInspector] public int teamId = 0;
    [HideInInspector] public int levelOverride = 0;

    private Transform spawnPoint;

    [Tooltip("Random spawn radius around team's base when spawnPoint is not set.")]
    [SerializeField] private float spawnRadius = 4f;

    // Injected at runtime by manager:
    private UnitDeckSO.Entry _entry;
    private IArmyEconomy _army;
    private UnitSpawner _spawner;
    [Inject(Optional = true)] private ITeamBaseProvider _baseProvider;

    // Cached data:
    private bool _configured;
    private bool _coolingDown;
    private float _cooldownSec;
    private int _spawnCost;
    private UnitData _unitData;

    private void Awake()
    {
        if (!button) button = GetComponent<Button>();
        if (cooldownMask) cooldownMask.fillAmount = 0f;
        button.onClick.AddListener(OnClick);

        // Safe default visual state
        SetActiveState(false);
    }

    private void OnDestroy()
    {
        button.onClick.RemoveListener(OnClick);
    }

    /// <summary>
    /// Called by UI manager to wire everything. If entry has no prefab, the button will be disabled.
    /// </summary>
    public void Configure(UnitDeckSO.Entry entry, IArmyEconomy army, UnitSpawner spawner, ITeamBaseProvider baseProvider = null,
                          Transform explicitSpawnPoint = null, int? forceTeamId = null)
    {
        _entry = entry;
        _army = army;
        _spawner = spawner;
        if (baseProvider != null) _baseProvider = baseProvider;
        if (explicitSpawnPoint) spawnPoint = explicitSpawnPoint;
        if (forceTeamId.HasValue) teamId = forceTeamId.Value;

        // Validate entry
        var uc = _entry.unitPrefab ? _entry.unitPrefab.GetComponent<UnitController>() : null;
        if (uc == null)
        {
            // No prefab → disable button & price
            SetActiveState(false);
            _configured = false;
            return;
        }

        // Read UnitData from prefab's serialized field via reflection (safe for read)
        _unitData = uc.GetType()
                      .GetField("unitData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                      ?.GetValue(uc) as UnitData;

        if (_unitData == null)
        {
            SetActiveState(false);
            _configured = false;
            return;
        }

        // Pick level to compute cost/CD (if army not yet injected, assume level 1)
        int lvlForStats = Mathf.Max(1, levelOverride > 0 ? levelOverride : (_army != null ? _army.State.level : 1));
        var stats = _unitData.GetStatsForLevel(lvlForStats);
        _spawnCost = stats.spawnCost;
        _cooldownSec = stats.spawnCooldown;

        // UI visuals
        if (costText)
        {
            costText.text = _spawnCost.ToString(); 
            costText.gameObject.SetActive(true);
            iconObject.SetActive(true);
        }
        if (cooldownMask) cooldownMask.fillAmount = 0f;

        SetActiveState(true);
        _configured = true;
    }

    /// <summary>Clears configuration (used by manager for empty deck slots).</summary>
    public void Clear()
    {
        _configured = false;
        _entry = default;
        _army = null;
        _spawner = null;
        _unitData = null;
        _spawnCost = 0;
        _cooldownSec = 0f;

        if (costText)
        {
            costText.text = string.Empty; 
            costText.gameObject.SetActive(false);
            iconObject.SetActive(false);
        }
        if (cooldownMask) cooldownMask.fillAmount = 0f;

        SetActiveState(false);
    }

    private void SetActiveState(bool enabledState)
    {
        // Button interactability
        if (button) button.interactable = enabledState && !_coolingDown;

        // Whole GO stays active (чтобы layout не ломался), но визуально можно приглушить
        var cg = GetComponent<CanvasGroup>();
        if (!cg) cg = gameObject.AddComponent<CanvasGroup>();
        cg.alpha = enabledState ? 1f : 0.45f;

        // Цена скрывается там, где нет entry (см. Configure/Clear)
    }

    private void OnClick()
    {
        if (!_configured || _coolingDown) return;
        if (_army == null || _spawner == null || _entry.unitPrefab == null) return;

        // Capacity check first
        if (!_army.TryReserveSlot()) return;

        // Credits
        if (!_army.TrySpendCredits(_spawnCost))
        {
            _army.ReleaseSlot();
            return;
        }

        // Determine spawn transform/pose
        int levelToUse = Mathf.Max(1, levelOverride > 0 ? levelOverride : _army.State.level);
        Vector3 pos; Quaternion rot;

        if (spawnPoint != null)
        {
            pos = spawnPoint.position;
            rot = spawnPoint.rotation;
        }
        else
        {
            Vector3 p = _baseProvider != null
                ? _baseProvider.GetRandomSpawnPointNearBase(teamId, spawnRadius)
                : transform.position; // fallback

            pos = p;

            var enemyBase = _baseProvider?.GetOpposingBaseTransform(teamId);
            Vector3 fwd = enemyBase ? (enemyBase.position - pos).normalized : Vector3.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            rot = Quaternion.LookRotation(fwd, Vector3.up);
        }

        // Spawn
        var unit = _spawner.SpawnUnitAt(teamId, levelToUse, pos, rot, _entry.unitPrefab);

        if (unit.GetComponent<UnitDeathReporter>() == null)
            unit.gameObject.AddComponent<UnitDeathReporter>();
        unit.gameObject.AddComponent<ReleaseSlotOnDestroy>().Init(_army);

        // Cooldown if any
        if (_cooldownSec > 0.001f)
            RunCooldown(_cooldownSec).Forget();
    }

    private async UniTask RunCooldown(float seconds)
    {
        _coolingDown = true;
        SetActiveState(true);               // keep visible, just disable interaction
        if (button) button.interactable = false;

        float t = 0f;
        while (t < seconds)
        {
            await UniTask.Yield();
            t += Time.deltaTime;
            if (cooldownMask) cooldownMask.fillAmount = 1f - Mathf.Clamp01(t / seconds);
        }

        if (cooldownMask) cooldownMask.fillAmount = 0f;
        _coolingDown = false;
        if (button) button.interactable = _configured; // only if we still have data
    }

    private sealed class ReleaseSlotOnDestroy : MonoBehaviour
    {
        private IArmyEconomy _army;
        public void Init(IArmyEconomy a) => _army = a;
        private void OnDestroy() => _army?.ReleaseSlot();
    }
}
