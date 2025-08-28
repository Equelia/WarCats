using Units.Logic.Core;
using UnityEngine;

namespace Units.Logic.Services
{
	public sealed class SensorService : ISensorService
	{
		public Transform FindNearestEnemy(UnitContext ctx, float radius)
		{
			Collider[] hits = Physics.OverlapSphere(ctx.Transform.position, radius);
			if (hits == null || hits.Length == 0) return null;

			Transform best = null;
			float bestDist = float.MaxValue;

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
			return best;
		}
	}
}