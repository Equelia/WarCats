using System.Reflection;
using UnityEngine;
using Units.Data;
using Units.Logic;

[DisallowMultipleComponent]
public class UnitBootstrapper : MonoBehaviour
{
	[Header("References on prefab")] public UnitAppearanceToggle appearance; // Body/Head/Weapon toggle

	[Header("Bones")] [SerializeField] private Transform bonesRoot; // UnitVisuals/Bones
	[SerializeField] private string muzzleBoneName = "Gun_end";

	[Header("Shared VFX (global defaults)")]
	public GameObject defaultProjectilePrefab;

	public GameObject rocketProjectilePrefab;
	public GameObject vfxImpactUnit;
	public GameObject vfxImpactObjects;
	public GameObject vfxMuzzleFlash;
	public GameObject vfxMuzzleSmoke;
	public GameObject vfxSpawn;

	[Header("Rocket-specific VFX overrides")]
	public GameObject rocketExplosionPrefab;

	[Header("Sockets (auto-filled from bones)")] [HideInInspector]
	public Transform firePoint;

	[HideInInspector] public GameObject muzzleFlashInstance;
	[HideInInspector] public GameObject muzzleSmokeInstance;

	private UnitController _logic;

	public UnitController Setup(UnitArchetype archetype, int teamId, int level, Transform explicitEnemyBase = null)
	{
		if (!archetype)
		{
			Debug.LogError("[UnitBootstrapper] Archetype is null.", this);
			return null;
		}

		// 1) Visuals
		var team = (teamId == 0) ? UnitAppearanceToggle.Team.Ally : UnitAppearanceToggle.Team.Enemy;
		if (appearance)
		{
			appearance.useEnemyDefaultHead = archetype.enemyUsesFixedHead;
			if (!string.IsNullOrEmpty(archetype.enemyHeadKey))
				appearance.enemyHeadKey = archetype.enemyHeadKey;

			appearance.ApplyLoadout(archetype.appearanceKind, team);

			AutoWireWeaponPoints();
		}

		// 2) Animator
		ApplyAnimatorFrom(archetype);

		// 3) Logic
		_logic = AttachLogicComponent(archetype.logicKind);
		if (_logic == null) return null;

        if (archetype.shootClip != null)
            _logic.shootClip = archetype.shootClip;

		// 4) Data
		AssignUnitData(_logic, archetype.unitData);

		// 5) VFX / sockets
		AutoWireVFX(_logic, archetype.logicKind);

        _logic.coverSearchRadius = 6f;
		_logic.coverSeekDistance = 10f;
		_logic.coverExcludeAngleDeg = 100f;

		// 6) finish boot
		_logic.Initialize(teamId, initLevel: level, explicitEnemyBase: explicitEnemyBase);
		_logic.BootstrapFinalize();


		return _logic;
	}

	// ----------------- helpers -----------------

	private UnitController AttachLogicComponent(UnitArchetype.LogicKind kind)
	{
		DisableAllKnownLogics();

		UnitController logic = null;
		switch (kind)
		{
			case UnitArchetype.LogicKind.Sniper:
				logic = GetComponent<SniperLogic>() ?? gameObject.AddComponent<SniperLogic>();
				break;
			case UnitArchetype.LogicKind.Pistol:
				logic = GetComponent<PistolierLogic>() ?? gameObject.AddComponent<PistolierLogic>();
				break;
			case UnitArchetype.LogicKind.Automata:
				logic = GetComponent<AutomaticLogic>() ?? gameObject.AddComponent<AutomaticLogic>();
				break;
			case UnitArchetype.LogicKind.Rocket:
				logic = GetComponent<RocketLogic>() ?? gameObject.AddComponent<RocketLogic>();
				break;
		}

		if (logic) logic.enabled = true;
		return logic;
	}

	private void DisableAllKnownLogics()
	{
		var s = GetComponent<SniperLogic>();
		if (s) s.enabled = false;
		var p = GetComponent<PistolierLogic>();
		if (p) p.enabled = false;
		var a = GetComponent<AutomaticLogic>();
		if (a) a.enabled = false;
		var r = GetComponent<RocketLogic>();
		if (r) r.enabled = false;
	}

	private static void AssignUnitData(UnitController ctrl, UnitData data)
	{
		if (!ctrl || !data) return;
		var field = typeof(UnitController).GetField("unitData",
			BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
		if (field != null) field.SetValue(ctrl, data);
	}

	// Finds firePoint / muzzle sockets
	private void AutoWireWeaponPoints()
	{
		if (!bonesRoot)
		{
			Debug.LogWarning("bonesRoot is not assigned.", this);
			return;
		}

		// 1) animated bone that follows hand
		Transform muzzleBone = FindBoneByName(bonesRoot, muzzleBoneName);
		if (!muzzleBone)
		{
			Debug.LogWarning($"Muzzle bone '{muzzleBoneName}' not found under bonesRoot.", this);
			return;
		}

		// 2) placeholders are placed under bones (not under weapon variant)
		// try exact names first
		Transform firePlExact = FindBoneByName(bonesRoot, "firePoint");
		Transform flashPlExact = FindBoneByName(bonesRoot, "muzzleFlashInstance");
		Transform smokePlExact = FindBoneByName(bonesRoot, "muzzleSmokeInstance");

		// fallback: loose search by tokens
		Transform firePl = firePlExact ? firePlExact : FindFirstTransformByNames(bonesRoot, "firePoint", "fire");
		Transform flashPl = flashPlExact
			? flashPlExact
			: FindFirstTransformByNames(bonesRoot, "muzzleFlashInstance", "flash", "muzzle");
		Transform smokePl = smokePlExact
			? smokePlExact
			: FindFirstTransformByNames(bonesRoot, "muzzleSmokeInstance", "smoke");

		// 3) ensure runtime sockets exist (create lightweight ones if missing)
		if (!firePoint) firePoint = new GameObject("firePoint").transform;
		if (!muzzleFlashInstance) muzzleFlashInstance = new GameObject("muzzleFlashInstance");
		if (!muzzleSmokeInstance) muzzleSmokeInstance = new GameObject("muzzleSmokeInstance");

		// parent under animated bone so they follow animation
		firePoint.SetParent(muzzleBone, false);
		muzzleFlashInstance.transform.SetParent(muzzleBone, false);
		muzzleSmokeInstance.transform.SetParent(muzzleBone, false);

		// 4) copy offsets from placeholders (world → local to muzzleBone)
		if (firePl)
		{
			firePoint.localPosition = muzzleBone.InverseTransformPoint(firePl.position);
			firePoint.localRotation = Quaternion.Inverse(muzzleBone.rotation) * firePl.rotation;
		}

		if (flashPl)
		{
			muzzleFlashInstance.transform.localPosition = muzzleBone.InverseTransformPoint(flashPl.position);
			muzzleFlashInstance.transform.localRotation = Quaternion.Inverse(muzzleBone.rotation) * flashPl.rotation;
		}

		if (smokePl)
		{
			muzzleSmokeInstance.transform.localPosition = muzzleBone.InverseTransformPoint(smokePl.position);
			muzzleSmokeInstance.transform.localRotation = Quaternion.Inverse(muzzleBone.rotation) * smokePl.rotation;
		}
	}

	private void AutoWireVFX(UnitController ctrl, UnitArchetype.LogicKind kind)
	{
		GameObject FindGO(string token) =>
			FindFirstTransformByNames(transform, token)?.gameObject;

		var goProjectile = FindGO("vfxProjectile") ?? defaultProjectilePrefab;
		var goHitEnemy = FindGO("vfxImpactUnit") ?? vfxImpactUnit;
		var goHitEnv = FindGO("vfxImpactObjects") ?? vfxImpactObjects;
		var goMuzzleFlash = FindGO("muzzleFlashInstance") ?? vfxMuzzleFlash;
		var goMuzzleSmoke = FindGO("muzzleSmokeInstance") ?? vfxMuzzleSmoke;
		var goSpawn = FindGO("vfxSpawn") ?? vfxSpawn;

		// --- Rocket overrides ---
		if (kind == UnitArchetype.LogicKind.Rocket)
		{
			if (rocketProjectilePrefab) goProjectile = rocketProjectilePrefab;

			TryAssign(ctrl, "explosionVfxPrefab", rocketExplosionPrefab);

			goHitEnemy = null;
			goHitEnv = null;
		}

		TryAssign(ctrl, "projectilePrefab", goProjectile);
		if (goHitEnemy) TryAssign(ctrl, "hitEnemyVfxPrefab", goHitEnemy);
		if (goHitEnv) TryAssign(ctrl, "hitEnvVfxPrefab", goHitEnv);
		TryAssign(ctrl, "muzzleFlashInstance", goMuzzleFlash);
		TryAssign(ctrl, "muzzleSmokeInstance", goMuzzleSmoke);
		TryAssign(ctrl, "spawnVfxPrefab", goSpawn);

		TryAssign(ctrl, "firePoint", firePoint);
	}


	private static void TryAssign(object target, string memberName, object value)
	{
		if (target == null || value == null) return;

		var t = target.GetType();
		const BindingFlags flags =
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase;

		// Field first
		var f = t.GetField(memberName, flags);
		if (f != null)
		{
			f.SetValue(target, value);
			return;
		}

		// Property fallback (in case you switch to properties later)
		var p = t.GetProperty(memberName, flags);
		if (p != null && p.CanWrite)
		{
			p.SetValue(target, value);
		}
	}

	private static Transform FindBoneByName(Transform root, string exactName)
	{
		if (!root || string.IsNullOrEmpty(exactName)) return null;
		var stack = new System.Collections.Generic.Stack<Transform>();
		stack.Push(root);
		while (stack.Count > 0)
		{
			var t = stack.Pop();
			if (t.name == exactName) return t;
			for (int i = 0; i < t.childCount; i++) stack.Push(t.GetChild(i));
		}

		return null;
	}

	private static Transform GetActiveChild(Transform root)
	{
		if (!root) return null;
		for (int i = 0; i < root.childCount; i++)
		{
			var c = root.GetChild(i);
			if (c.gameObject.activeSelf) return c;
		}

		return root.childCount > 0 ? root.GetChild(0) : null;
	}

	private static Transform FindFirstTransformByNames(Transform root, params string[] names)
	{
		if (!root) return null;
		var stack = new System.Collections.Generic.Stack<Transform>();
		stack.Push(root);
		while (stack.Count > 0)
		{
			var t = stack.Pop();
			foreach (var n in names)
				if (t.name.ToLower().Contains(n.ToLower()))
					return t;
			for (int i = 0; i < t.childCount; i++) stack.Push(t.GetChild(i));
		}

		return null;
	}

	private void ApplyAnimatorFrom(UnitArchetype archetype)
	{
		var animator = GetComponentInChildren<Animator>();
		if (!animator) return;

		// Prefer override if assigned; otherwise use base; if both null — keep current.
		var selected = (RuntimeAnimatorController)(archetype.overrideController ?? archetype.baseController);
		if (selected) animator.runtimeAnimatorController = selected;
	}

	public void SetupVisualOnly(UnitArchetype archetype, int teamId)
	{
		if (!archetype) return;

		// visuals (same as in Setup but without logic/data/init)
		var team = (teamId == 0) ? UnitAppearanceToggle.Team.Ally : UnitAppearanceToggle.Team.Enemy;
		if (appearance)
		{
			appearance.useEnemyDefaultHead = archetype.enemyUsesFixedHead;
			if (!string.IsNullOrEmpty(archetype.enemyHeadKey))
				appearance.enemyHeadKey = archetype.enemyHeadKey;

			appearance.ApplyLoadout(archetype.appearanceKind, team);

			// sockets from bones (firePoint / muzzle…)
			AutoWireWeaponPoints();
		}

		ApplyAnimatorFrom(archetype);

		// disable heavy runtime components for preview render
		var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
		if (agent) agent.enabled = false;
		var rb = GetComponent<Rigidbody>();
		if (rb) rb.isKinematic = true;
		foreach (var col in GetComponentsInChildren<Collider>(true)) col.enabled = false;

		foreach (var mb in GetComponentsInChildren<MonoBehaviour>(true))
		{
			if (!mb) continue;
			// keep only the minimal set needed for visuals
			if (mb == this) continue;
			if (mb is UnitAppearanceToggle) continue;
			if (mb is Animator) continue;
			mb.enabled = false;
		}
	}
}