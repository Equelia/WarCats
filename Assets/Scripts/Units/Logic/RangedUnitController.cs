using Units.Logic.Core;
using Units.Logic.Services;
using UnityEngine;

namespace Units.Logic
{
	public abstract class RangedUnitController : UnitController
	{
		[Header("Common VFX / Sockets")] public GameObject projectilePrefab;
		public GameObject hitEnemyVfxPrefab;
		public GameObject hitEnvVfxPrefab;
		public GameObject spawnVfxPrefab;
		public GameObject muzzleFlashInstance;
		public GameObject muzzleSmokeInstance;
		public Transform firePoint;

        [Header("Common Targeting")] public Vector3 targetOffset = new Vector3(0f, 1.2f, 0f);
		public LayerMask hitMask = ~0;

		// Параметры «наглого» проталкивания
		[Header("Anti-Stall near Enemy Base")]
		[Tooltip("Если юнит близко к базе, но не двигается, толкнуть на эту дистанцию вперёд.")]
		public float nudgeDistance = 0.5f;

		[Tooltip("Минимальная пауза между толчками.")]
		public float nudgeCooldown = 0.5f;

		[Tooltip("Считаем «почти дошёл», если ближе этого множителя к атак-радиусу.")]
		public float softRangeMultiplier = 1.25f;

		[Tooltip("Порог скорости для детекции «стояния».")]
		public float stuckVelocitySqr = 0.0004f; // ~0.02 m/s

		protected ReusableEffect _muzzleFx;
		protected ReusableEffect _muzzleSmokeFx;

		protected override void Awake()
		{
			base.Awake();
			if (spawnVfxPrefab)
				VfxPlayer.SpawnOneShot(spawnVfxPrefab, transform.position, transform.rotation);
		}

		protected override void OnBuilt()
		{
			if (muzzleFlashInstance)
				_muzzleFx = muzzleFlashInstance.GetComponent<ReusableEffect>() ??
				            muzzleFlashInstance.AddComponent<ReusableEffect>();
			if (muzzleSmokeInstance)
				_muzzleSmokeFx = muzzleSmokeInstance.GetComponent<ReusableEffect>() ??
				                 muzzleSmokeInstance.AddComponent<ReusableEffect>();
			OnRangedBuilt();
		}

		protected virtual void OnRangedBuilt()
		{
		}

		internal void PlayMuzzleFx()
		{
			if (shootClip != null)
				_ctx.AudioSource.PlayOneShot(shootClip);
			_muzzleFx?.Play();
			_muzzleSmokeFx?.Play();
		}

		protected (Vector3 origin, Vector3 forward) GetOriginForward(UnitContext ctx)
		{
			var o = firePoint ? firePoint.position : ctx.Transform.position;
			var f = firePoint ? firePoint.forward : ctx.Transform.forward;
			return (o, f);
		}

		protected VisualBullet SpawnBullet(UnitContext ctx, float speed, Vector3 dirNormalized)
		{
			if (!projectilePrefab) return null;

			var (o, _) = GetOriginForward(ctx);
			var dir = dirNormalized.sqrMagnitude > 1e-6f ? dirNormalized : ctx.Transform.forward;
			dir.y = Mathf.Clamp(dir.y, -0.98f, 0.98f);

			var go = VfxPool.Get(projectilePrefab, o, Quaternion.LookRotation(dir, Vector3.up));
			var vb = go.GetComponent<VisualBullet>() ?? go.AddComponent<VisualBullet>();
			vb.ResetTrailIfAny();
			vb.SetSpeed(speed);
			vb.LaunchLinear(o, dir);
			return vb;
		}

		protected VisualBullet SpawnBullet(UnitContext ctx, float speed)
		{
			var (_, f) = GetOriginForward(ctx);
			return SpawnBullet(ctx, speed, f);
		}

		protected override void TryFallbackFireAtEnemyBase()
		{
			if (_ctx == null || _ctx.EnemyBase == null) return;

			if (_sensor != null)
			{
				var enemy = _sensor.FindNearestEnemy(_ctx, _ctx.Stats.attackRange);
				if (enemy != null && enemy != _ctx.EnemyBase) return;
			}

			var basePos = _ctx.EnemyBase.position;
			float range = Mathf.Max(0.1f, _ctx.Stats.attackRange);
			float dist = Vector3.Distance(_ctx.Transform.position, basePos);

			if (_fsm.Current is Units.Logic.States.AttackState && _ctx.CurrentTarget == _ctx.EnemyBase && dist <= range * 1.2f)
				return;

			if (dist > range * 0.98f)
			{
				_ctx.LockWalkAnim = false;
				var approach = GetApproachPointAroundBase(basePos, range);
				_move?.OverrideStoppingDistance(_ctx, 0.15f);
				if (_ctx.Agent != null) _ctx.Agent.isStopped = false;
				_move?.GoTo(_ctx, approach);
				_move?.SyncWalkingAnim(_ctx, true);
				return;
			}

			_move?.RestoreStoppingDistance(_ctx);
			_move?.ResetPath(_ctx);
			if (_ctx.Agent != null) _ctx.Agent.isStopped = true;

			_ctx.LockWalkAnim = true;
			_move?.SyncWalkingAnim(_ctx, false);

			var aim = basePos + targetOffset;
			var flat = aim - _ctx.Transform.position; flat.y = 0f;
			if (flat.sqrMagnitude > 0.001f)
				_ctx.Transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);

			_ctx.CurrentTarget = _ctx.EnemyBase;

			if (!_ctx.HasBaseRangeBoost)
			{
				_ctx.RangeBoostOriginal = _ctx.Stats.attackRange;
				_ctx.Stats.attackRange = _ctx.RangeBoostOriginal + 5f;
				_ctx.HasBaseRangeBoost = true;
				if (_ctx.Agent != null)
					_ctx.Agent.stoppingDistance = _ctx.Stats.attackRange;
			}

			if (!(_fsm.Current is Units.Logic.States.AttackState))
				_ = _fsm.SetStateAsync(new Units.Logic.States.AttackState(_ctx, _move, _sensor, _cover, _fsm, _combat));
		}

		private Vector3 GetApproachPointAroundBase(Vector3 basePos, float attackRange)
		{
			var toBase = basePos - _ctx.Transform.position;
			toBase.y = 0f;
			if (toBase.sqrMagnitude < 0.001f) toBase = _ctx.Transform.forward;
			toBase.Normalize();

			float desired = Mathf.Max(0.5f, attackRange * 0.92f);
			int seed = Mathf.Abs(GetInstanceID());
			float baseAngle = (seed % 360) * Mathf.Deg2Rad;

			Vector3 best = basePos - toBase * desired;
			if (UnityEngine.AI.NavMesh.SamplePosition(best, out var hit, 1.2f, UnityEngine.AI.NavMesh.AllAreas))
				return hit.position;

			const int tries = 10;
			for (int i = 0; i < tries; i++)
			{
				float ang = baseAngle + i * (Mathf.PI * 2f / tries);
				Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
				Vector3 candidate = basePos + dir * desired;
				if (UnityEngine.AI.NavMesh.SamplePosition(candidate, out var h, 1.8f, UnityEngine.AI.NavMesh.AllAreas))
					return h.position;
			}

			if (UnityEngine.AI.NavMesh.SamplePosition(basePos - toBase * desired, out var last, 3f,
				    UnityEngine.AI.NavMesh.AllAreas))
				return last.position;

			return _ctx.Transform.position;
		}
	}
}