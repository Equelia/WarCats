// CombatService.cs (замени на эту версию)
using System.Threading;
using Cysharp.Threading.Tasks;
using Units.Logic.Core;
using UnityEngine;

namespace Units.Logic.Services
{
	public class CombatService : ICombatService
	{ 
		/// <summary>FX hook: called right when animation tells "shot fired".</summary>
		protected virtual void OnBeforeAttackFx(UnitContext ctx) { }

		/// <summary>FX hook: called after gameplay decided hit/miss.</summary>
		protected virtual void OnShotResolvedFx(UnitContext ctx, Transform target, bool didHit) { }

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
			OnBeforeAttackFx(ctx); 
			await UniTask.Yield(ct);

			if (target == null) return;

			var targetLogic = target.GetComponentInParent<UnitController>();
			float targetVul = targetLogic != null ? targetLogic.Context.GetEffectiveVulnerability() : 0f;

			float finalHitChance = ctx.Stats.accuracy * (1f - Mathf.Clamp01(targetVul));
			bool didHit = Random.value <= finalHitChance;

			if (didHit)
			{
				if (targetLogic != null)
				{
					targetLogic.ReceiveDamage(ctx.Stats.damage);
				}
				else
				{
					// Если цель реализует IDamageable (например, база), нанести урон
					var damageable = target.GetComponentInParent<IDamageable>();
					if (damageable != null)
					{
						damageable.ApplyDamage(ctx.Stats.damage, ctx.Transform.gameObject);
					}
				}
			}

			OnShotResolvedFx(ctx, target, didHit);
		}
	}
}