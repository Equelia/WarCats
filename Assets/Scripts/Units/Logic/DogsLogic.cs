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

		[Header("Animation (optional)")]
		[Tooltip("Animator trigger to play on bite (optional)")]
		public string biteAnimTrigger = "";

		private DogsData _data;

		protected override void OnRangedBuilt()
		{
			_data = Context.UnitData as DogsData;
		}

		/// <summary>
		/// Use a custom combat service that drives the same hit/accuracy flow
		/// but renders melee FX instead of projectiles.
		/// </summary>
		protected override ICombatService CreateCombatService() => new DogsMeleeCombatService(this);

		private sealed class DogsMeleeCombatService : RangedCombatServiceBase
		{
			public DogsMeleeCombatService(DogsLogic owner) : base(owner) { }

			/// <summary>
			/// Called by base after hit resolution. We do not spawn bullets;
			/// we only play bite VFX/SFX, and optionally push the target a bit.
			/// Damage application remains in the base combat flow.
			/// </summary>
			protected override void OnShotResolvedFx(UnitContext ctx, Transform target, bool didHit)
			{
				var owner = (DogsLogic)Owner;
				var data = owner._data;

				// Play bite animation if provided
				if (!string.IsNullOrEmpty(owner.biteAnimTrigger) && ctx.Animator != null)
					ctx.Animator.SetTrigger(owner.biteAnimTrigger);

				// Only do FX on hit vs a target
				if (!didHit || !target) return;

				// Compute hit point on target collider or fallback near chest (targetOffset)
				Vector3 hitPoint = target.position + owner.targetOffset;
				Vector3 hitNormal = Vector3.up;

				var col = target.GetComponentInChildren<Collider>();
				if (col != null)
				{
					hitPoint = col.ClosestPoint(hitPoint);
					var dir = (hitPoint - ctx.Transform.position);
					if (dir.sqrMagnitude > 0.0001f) hitNormal = -dir.normalized;
				}

				// Bite SFX
				if (data && data.biteSfx && ctx.AudioSource)
					ctx.AudioSource.PlayOneShot(data.biteSfx);

				// Bite VFX
				if (data && data.biteVfx)
					VfxPlayer.SpawnOneShot(data.biteVfx, hitPoint, Quaternion.LookRotation(hitNormal, Vector3.up));

				// Optional small push to emphasize "aggro grab"
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
