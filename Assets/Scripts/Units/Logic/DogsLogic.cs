using Units.Data;
using Units.Logic.Core;
using Units.Logic.Services;
using UnityEngine;
using UnityEngine.AI;

namespace Units.Logic
{
	/// <summary>
	/// Single dog from the pack: fast melee chaser that bites nearby enemies.
	/// Reuses the ranged attack cycle so it integrates with your FSM/States.
	/// We only override the shot FX callback to play bite VFX/SFX (no projectile).
	/// </summary>
	[RequireComponent(typeof(NavMeshAgent))]
	public class DogsLogic : RangedUnitController
	{
		[Header("Melee")]
		[Tooltip("Extra padding added to attackRange when checking bite reach (visual only)")]
		public float biteRangePadding = 0.1f;
		
		[Header("Aggro")]
		[Tooltip("How far dog scans for first target (in multiples of attackRange).")]
		public float aggroRangeMultiplier = 7f;
		[Tooltip("How often to retarget (seconds).")]
		public float retargetInterval = 0.2f;
		
		private float _retargetTimer;


		[Header("Animation (optional)")]
		[Tooltip("Animator trigger to play on bite (optional)")]
		public string biteAnimTrigger = "";

		private DogsData _data;

		protected override void OnRangedBuilt()
		{
			_data = Context.UnitData as DogsData;
			
			if (string.IsNullOrEmpty(biteAnimTrigger))
				biteAnimTrigger = UnitContext.AnimShoot.ToString(); 

			var a = Context.Agent;
			if (a != null)
			{
				a.autoBraking = true;
				a.stoppingDistance = Mathf.Max(0.15f, Context.Stats.attackRange * 0.95f);
				a.acceleration = Mathf.Max(a.acceleration, 12f);
				a.angularSpeed = Mathf.Max(a.angularSpeed, 240f);
			}	
		}

		private void LateUpdate()
		{
			if (_ctx == null || !_ctx.IsInitialized || _ctx.CurrentHealth <= 0) return;

			// 1) Retarget wider than attackRange
			_retargetTimer -= Time.deltaTime;
			if (_retargetTimer <= 0f)
			{
				_retargetTimer = retargetInterval;
				float searchR = Mathf.Max(_ctx.Stats.attackRange * aggroRangeMultiplier, _ctx.Stats.attackRange + 2f);
				var t = _sensor?.FindNearestEnemy(_ctx, searchR);
				if (t) _ctx.CurrentTarget = t;
			}

			// 2) Anchor when in bite reach
			var a = _ctx.Agent;
			var tgt = _ctx.CurrentTarget;
			if (a != null && tgt != null)
			{
				float biteReach = Mathf.Max(0.05f, _ctx.Stats.attackRange + biteRangePadding);
				if (Vector3.Distance(_ctx.Transform.position, tgt.position) <= biteReach)
				{
					a.isStopped = true;
					a.velocity = Vector3.zero;
					_move?.SyncWalkingAnim(_ctx, false);

					var to = tgt.position - _ctx.Transform.position;
					to.y = 0f;
					if (to.sqrMagnitude > 0.0001f)
						_ctx.Transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);

					_combat?.TryAttack(_ctx);
				}
			}
		}

		/// <summary>
		/// Use a custom combat service that drives the same hit/accuracy flow
		/// but renders melee FX instead of projectiles.
		/// </summary>
		protected override ICombatService CreateCombatService() => new DogsMeleeCombatService(this);

		private sealed class DogsMeleeCombatService : RangedCombatServiceBase
		{
			public DogsMeleeCombatService(DogsLogic owner) : base(owner) { }

			protected override void OnBeforeAttackFx(UnitContext ctx)
			{
				var owner = (DogsLogic)Owner;

				// stop walk blend if it was moving
				if (ctx.Animator != null)
				{
					ctx.Animator.SetBool(UnitContext.AnimIsWalking, false);

					var trig = string.IsNullOrEmpty(owner.biteAnimTrigger)
						? UnitContext.AnimShoot.ToString()
						: owner.biteAnimTrigger;

					// retrigger safely
					ctx.Animator.SetTrigger(trig);
				}
			}

			/// <summary>
			/// Called by base after hit resolution. We do not spawn bullets;
			/// we only play bite VFX/SFX, and optionally push the target a bit.
			/// Damage application remains in the base combat flow.
			/// </summary>
			protected override void OnShotResolvedFx(UnitContext ctx, Transform target, bool didHit)
			{
				var owner = (DogsLogic)Owner;
				var data = owner._data;

				// NOTE: keep this (your bite SFX/VFX + optional push) as-is
				if (!string.IsNullOrEmpty(owner.biteAnimTrigger) && ctx.Animator != null)
					ctx.Animator.SetTrigger(owner.biteAnimTrigger);

				if (!didHit || !target) return;

				Vector3 hitPoint = target.position + owner.targetOffset;
				Vector3 hitNormal = Vector3.up;

				var col = target.GetComponentInChildren<Collider>();
				if (col != null)
				{
					hitPoint = col.ClosestPoint(hitPoint);
					var dir = (hitPoint - ctx.Transform.position);
					if (dir.sqrMagnitude > 0.0001f) hitNormal = -dir.normalized;
				}

				if (data && data.biteSfx && ctx.AudioSource)
					ctx.AudioSource.PlayOneShot(data.biteSfx);

				if (data && data.biteVfx)
					VfxPlayer.SpawnOneShot(data.biteVfx, hitPoint, Quaternion.LookRotation(hitNormal, Vector3.up));

				if (data != null && data.biteNudgeForce > 0.01f)
				{
					var rb = target.GetComponentInParent<Rigidbody>();
					if (rb)
					{
						var push = (target.position - ctx.Transform.position);
						push.y = 0f;
						if (push.sqrMagnitude < 0.0001f) push = ctx.Transform.forward;
						rb.AddForce(push.normalized * data.biteNudgeForce, ForceMode.Impulse);
					}
				}
			}
		}
	}
}
