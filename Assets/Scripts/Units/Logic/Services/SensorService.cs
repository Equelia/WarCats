using Units.Logic.Core;
using UnityEngine;

namespace Units.Logic.Services
{
	public sealed class SensorService : ISensorService
	{
		public Transform FindNearestEnemy(UnitContext ctx, float radius)
		{
			Collider[] hits = Physics.OverlapSphere(ctx.Transform.position, radius);
			Transform best = null;
			float bestDist = float.MaxValue;

			if (hits != null && hits.Length > 0)
			{
				foreach (var c in hits)
				{
					var u = c.GetComponentInParent<UnitController>();
					if (u == null) continue;
					if (u.gameObject == ctx.Transform.gameObject) continue;
					if (u.TeamId == ctx.TeamId) continue;

					float d = Vector3.Distance(ctx.Transform.position, u.transform.position);
					if (d > radius) continue;

					if (d < bestDist)
					{
						bestDist = d;
						best = u.transform;
					}
				}
			}

			// Если врагов нет — вернуть базу как цель, если она в радиусе (без «поблажек»)
			if (best == null && ctx.EnemyBase != null)
			{
				float baseDist = Vector3.Distance(ctx.Transform.position, ctx.EnemyBase.position);
				if (baseDist <= radius)
					return ctx.EnemyBase;
			}

			return best;
		}
	}
}