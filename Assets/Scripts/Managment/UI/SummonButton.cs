// SummonButton.cs (full, drop-in)
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

    [Header("Spawn Areas (optional)")]
    [Tooltip("If set, spawn will be sampled from these areas (matching teamId). If empty, fallback logic is used.")]
    [SerializeField] private SpawnAreaBase[] spawnAreas;

    [HideInInspector] public int teamId = 0;
    [HideInInspector] public int levelOverride = 0;

    private Transform spawnPoint;

    [Tooltip("Random spawn radius around team's base when spawnPoint/areas are not set.")]
    [SerializeField] private float spawnRadius = 4f;

    // Injected:
    private UnitDeckSO.Entry _entry;
    private IArmyEconomy _army;
    private UnitSpawner _spawner;
    [Inject(Optional = true)] private ITeamBaseProvider _baseProvider;

    // Cached:
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
        SetActiveState(false);
    }

    private void OnDestroy()
    {
        button.onClick.RemoveListener(OnClick);
    }

    public void Configure(UnitDeckSO.Entry entry, IArmyEconomy army, UnitSpawner spawner, ITeamBaseProvider baseProvider = null,
                          Transform explicitSpawnPoint = null, int? forceTeamId = null)
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

        int lvlForStats = Mathf.Max(1, levelOverride > 0 ? levelOverride : (_army != null ? _army.State.level : 1));
        var stats = _unitData.GetStatsForLevel(lvlForStats);
        _spawnCost = stats.spawnCost;
        _cooldownSec = stats.spawnCooldown;

        if (costText)
        {
            costText.text = _spawnCost.ToString();
            costText.gameObject.SetActive(true);
            if (iconObject) iconObject.SetActive(true);
        }
        if (cooldownMask) cooldownMask.fillAmount = 0f;

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
        _cooldownSec = 0f;

        if (costText)
        {
            costText.text = string.Empty;
            costText.gameObject.SetActive(false);
            if (iconObject) iconObject.SetActive(false);
        }
        if (cooldownMask) cooldownMask.fillAmount = 0f;

        SetActiveState(false);
    }

    private void SetActiveState(bool enabledState)
    {
        if (button) button.interactable = enabledState && !_coolingDown;

        var cg = GetComponent<CanvasGroup>();
        if (!cg) cg = gameObject.AddComponent<CanvasGroup>();
        cg.alpha = enabledState ? 1f : 0.45f;
    }

    private void OnClick()
    {
        if (!_configured || _coolingDown) return;
        if (_army == null || _spawner == null || _entry.unitPrefab == null) return;

        // Reserve ONE slot here (only here)
        if (!_army.TryReserveSlot()) return;

        // Credits
        if (!_army.TrySpendCredits(_spawnCost))
        {
            _army.ReleaseSlot();
            return;
        }

        // Compute spawn pose (areas -> explicit point -> base-provider fallback)
        int levelToUse = Mathf.Max(1, levelOverride > 0 ? levelOverride : _army.State.level);
        if (!TryComputeSpawnPose(out Vector3 pos, out Quaternion rot))
        {
            // Failed to sample — rollback
            _army.AddCredits(_spawnCost);
            _army.ReleaseSlot();
            Debug.LogWarning("SummonButton: Failed to compute spawn pose. Rolled back.");
            return;
        }

        // Spawn; pass 'upstreamAlreadyReserved: true' — spawner must NOT reserve again.
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
            _army.AddCredits(_spawnCost);
            _army.ReleaseSlot();
            Debug.LogWarning("SummonButton: Spawn failed after reservation. Rolled back.");
            return;
        }

        bool isPistolier = unit is Units.Logic.PistolierLogic;
        if (!isPistolier)
        {
            if (unit.GetComponent<UnitDeathReporter>() == null)
                unit.gameObject.AddComponent<UnitDeathReporter>();

            var rel = unit.gameObject.AddComponent<ReleaseSlotOnDestroy>();
            rel.Init(_army);
        }

        if (_cooldownSec > 0.001f)
            RunCooldown(_cooldownSec).Forget();
    }

    private bool TryComputeSpawnPose(out Vector3 pos, out Quaternion rot)
    {
        // 1) Prefer areas for this team
        if (spawnAreas != null && spawnAreas.Length > 0)
        {
            // Try up to N attempts to get a valid point from any matching area
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

        // Collect matching or team-agnostic areas
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

    private async UniTask RunCooldown(float seconds)
    {
        _coolingDown = true;
        SetActiveState(true);
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
        if (button) button.interactable = _configured;
    }

    private sealed class ReleaseSlotOnDestroy : MonoBehaviour
    {
        private IArmyEconomy _army;
        public void Init(IArmyEconomy a) => _army = a;
        private void OnDestroy() => _army?.ReleaseSlot();
    }
}
