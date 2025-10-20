using System;
using Cysharp.Threading.Tasks;
using TMPro;
using Units.Data;
using Units.Logic;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

[DisallowMultipleComponent]
public class UnitSummonController : MonoBehaviour
{
	private int slotLevel = 1;
	[SerializeField] private Button upgradeButton;
	[SerializeField] private TMP_Text levelText;
	[SerializeField] private int maxLevel = 3;
	
	public int teamId = 0;
	public int levelOverride = 0;



	[Header("Spawn config")] public Transform spawnPoint;
	public SpawnAreaBase[] spawnAreas;
	public float spawnRadius = 4f;

	// --- зависимости/данные ---
	private UnitArchetype _archetype; // << вместо UnitController prefab
	private IArmyEconomy _army;
	private UnitSpawner _spawner;
	private ITeamBaseProvider _baseProvider;

	// вытаскиваем из archetype.unitData
	private UnitData _unitData;

	// Cost / stock
	public int SpawnCost { get; private set; }
	public bool HasStockLimit { get; private set; }
	public UnitData.StockMode StockMode { get; private set; }
	public int StockCurrent { get; private set; }
	public int StockMax { get; private set; }

	public bool IsRefillingToMax { get; private set; }
	private float _refillCooldownSec;

	// UI events
	public event Action<int, int> OnStockChanged;
	public event Action<bool> OnRefillActiveChanged;
	public event Action<float> OnRefillProgress;

	private bool _configured;

	// -------------------- Public API --------------------

	private void Awake()
	{
		if (_baseProvider == null)
			_baseProvider = FindObjectOfType<TeamBaseProvider>();
	}

	public void Configure(
		UnitArchetype archetype,
		IArmyEconomy army,
		UnitSpawner spawner,
		ITeamBaseProvider baseProvider = null,
		Transform explicitSpawnPoint = null,
		SpawnAreaBase[] areas = null,
		int? forceTeamId = null,
		int? forceLevel = null,
		float? forceSpawnRadius = null)
	{
		_archetype = archetype;
		_army = army;
		_spawner = spawner;
		_baseProvider = baseProvider ?? _baseProvider;

		if (explicitSpawnPoint) spawnPoint = explicitSpawnPoint;
		if (areas != null) spawnAreas = areas;
		if (forceTeamId.HasValue) teamId = forceTeamId.Value;
		if (forceLevel.HasValue) levelOverride = forceLevel.Value;
		if (forceSpawnRadius.HasValue) spawnRadius = forceSpawnRadius.Value;

		if (_archetype == null || _archetype.unitData == null || _army == null || _spawner == null)
		{
			_configured = false;
			return;
		}

		_unitData = _archetype.unitData;

		// Cost
		int lvlForStats = Mathf.Max(1, levelOverride > 0 ? levelOverride : _army.State.level);
		var stats = _unitData.GetStatsForLevel(lvlForStats);
		SpawnCost = stats.spawnCost;

		// Stock init
		StockMode = _unitData.stockMode;
		switch (StockMode)
		{
			case UnitData.StockMode.Unlimited:
				HasStockLimit = false;
				StockCurrent = int.MaxValue;
				StockMax = int.MaxValue;
				_refillCooldownSec = 0f;
				IsRefillingToMax = false;
				break;

			case UnitData.StockMode.LimitedNoRecharge:
				HasStockLimit = true;
				StockCurrent = Mathf.Max(0, _unitData.startingStock);
				StockMax = Mathf.Max(0, _unitData.maxStock);
				_refillCooldownSec = 0f;
				IsRefillingToMax = false;
				break;

			case UnitData.StockMode.LimitedWithRecharge:
				HasStockLimit = true;
				StockCurrent = Mathf.Max(0, _unitData.startingStock);
				StockMax = Mathf.Max(StockCurrent, _unitData.maxStock);
				_refillCooldownSec = Mathf.Max(0.01f, _unitData.rechargeCooldown);
				IsRefillingToMax = false;

				if (StockCurrent <= 0)
					StartRefillToMaxIfNeeded().Forget();
				break;
		}

		_configured = true;
		RaiseStock();
		
		if(upgradeButton != null)
		{
			upgradeButton.onClick.AddListener(HandleUpgradeBtnClick);
			ChangeLevelText();
		}
	}

	private void HandleUpgradeBtnClick()
	{
		if(slotLevel < maxLevel && _army.TrySpendPoints(1))
		{
			slotLevel++;
			ChangeLevelText();
		}
	}

	private void ChangeLevelText()
	{
		levelText.text = slotLevel.ToString();
	}

	public bool CanSummon =>
		_configured &&
		(!HasStockLimit || StockCurrent > 0) &&
		!(StockMode == UnitData.StockMode.LimitedWithRecharge && IsRefillingToMax);

	/// <summary>Try to summon one unit or a squad. Handles slots, credits, stock, pose, spawn.</summary>
	public bool TrySummon()
	{
		if (!CanSummon) return false;

		if (!_army.TryReserveSlot()) return false;

		if (!_army.TrySpendCredits(SpawnCost))
		{
			_army.ReleaseSlot();
			return false;
		}

		if (HasStockLimit)
		{
			StockCurrent = Mathf.Max(0, StockCurrent - 1);
			RaiseStock();
		}

		if (!TryComputeSpawnPose(out var pos, out var rot))
		{
			if (HasStockLimit)
			{
				StockCurrent++;
				RaiseStock();
			}

			_army.AddCredits(SpawnCost);
			_army.ReleaseSlot();
			Debug.LogWarning("UnitSummonController: failed to compute spawn pose. Rolled back.");
			return false;
		}

		int levelToUse = Mathf.Max(1, levelOverride > 0 ? levelOverride : _army.State.level); 
		if (teamId == 0)
			levelToUse = slotLevel;
		
		int count = Mathf.Max(1, _unitData.GetSpawnCountForLevel(levelToUse));

		var enemyBase = (_baseProvider != null) ? _baseProvider.GetOpposingBaseTransform(teamId) : null;

		var members = new UnitController[count];
		const float spread = 0.6f;

		for (int i = 0; i < count; i++)
		{
			float ang = (count == 1) ? 0f : (Mathf.PI * 2f * i) / count;
			Vector3 offset = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * spread;
			
			members[i] = _spawner.Spawn(_archetype, teamId, levelToUse, // УРОВЕНРЬ ТУТА
				pos + offset, rot, enemyBase);

			if (!members[i])
			{
				for (int j = 0; j < i; j++)
					if (members[j])
						Destroy(members[j].gameObject);

				if (HasStockLimit)
				{
					StockCurrent++;
					RaiseStock();
				}

				_army.AddCredits(SpawnCost);
				_army.ReleaseSlot();
				Debug.LogWarning("UnitSummonController: spawn failed after reservation. Rolled back.");
				return false;
			}
		}

		for (int i = 0; i < members.Length; i++)
			if (members[i] && !members[i].GetComponent<UnitDeathReporter>())
				members[i].gameObject.AddComponent<UnitDeathReporter>();

		var releaserGo = new GameObject("[SlotReleaserTemp]");
		var releaser = releaserGo.AddComponent<ReleaseSlotWhenAllDead>();
		releaser.Init(_army, members.Length);

		foreach (var m in members)
		{
			var hook = m.gameObject.AddComponent<MemberDeathHook>();
			hook.Bind(releaser);
		}

		if (StockMode == Units.Data.UnitData.StockMode.LimitedWithRecharge && StockCurrent == 0)
			StartRefillToMaxIfNeeded().Forget();

		return true;
	}


	private void RaiseStock()
		=> OnStockChanged?.Invoke(HasStockLimit ? Mathf.Max(0, StockCurrent) : -1,
			HasStockLimit ? Mathf.Max(0, StockMax) : -1);

	private async UniTaskVoid StartRefillToMaxIfNeeded()
	{
		if (StockMode != UnitData.StockMode.LimitedWithRecharge) return;
		if (IsRefillingToMax || StockCurrent > 0) return;

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

		if (spawnPoint != null)
		{
			pos = spawnPoint.position;
			rot = spawnPoint.rotation;
			return true;
		}

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

public sealed class ReleaseSlotWhenAllDead : MonoBehaviour
{
	private IArmyEconomy _army;
	private int _remaining;

	public void Init(IArmyEconomy army, int membersCount)
	{
		_army = army;
		_remaining = membersCount;
	}

	public void NotifyMemberDead()
	{
		_remaining--;
		if (_remaining <= 0)
		{
			_army?.ReleaseSlot();
			Destroy(gameObject);
		}
	}
}

public sealed class MemberDeathHook : MonoBehaviour
{
	private ReleaseSlotWhenAllDead _group;
	public void Bind(ReleaseSlotWhenAllDead group) => _group = group;
	private void OnDestroy() => _group?.NotifyMemberDead();
}