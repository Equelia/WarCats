using System.Threading;
using Cysharp.Threading.Tasks;
using Units.Logic.Core;
using UnityEngine;

namespace Units.Logic.Services
{
	public class CombatService : ICombatService
	{ 
		/// <summary>Hook for subclasses (e.g., pistol muzzle flash).</summary>
		protected virtual void OnBeforeAttackFx(UnitContext ctx) { }

		public void TryAttack(UnitContext ctx)
		{
			if (Time.time - ctx.LastAttackTime < ctx.Stats.attackCooldown) return;
			ctx.LastAttackTime = Time.time;

			if (ctx.Animator != null)
				ctx.Animator.SetTrigger(UnitContext.AnimShoot);

			_ = PerformAttackAsync(ctx, ctx.CurrentTarget, ctx.Cts != null ? ctx.Cts.Token : CancellationToken.None);
		}

		public virtual async UniTask PerformAttackAsync(UnitContext ctx, Transform target, CancellationToken ct)
		{
			OnBeforeAttackFx(ctx); // FX hook
			await UniTask.Yield(ct);

			if (target == null) return;

			var targetLogic = target.GetComponentInParent<UnitController>();
			float targetVul = targetLogic != null ? targetLogic.Context.GetEffectiveVulnerability() : 0f;

			float finalHitChance = ctx.Stats.accuracy * (1f - Mathf.Clamp01(targetVul));
			float roll = Random.value;
			if (roll <= finalHitChance)
			{
				if (targetLogic != null)
					targetLogic.ReceiveDamage(ctx.Stats.damage);
			}
		}
	}
}