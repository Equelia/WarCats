using System.Threading;
using Cysharp.Threading.Tasks;
using Helpers;
using Units.Logic.Core;
using UnityEngine;

namespace Units.Logic.Services
{
	public interface IMovementService
	{
		void AdvanceTowardsBase(UnitContext ctx);
		void GoTo(UnitContext ctx, Vector3 pos);
		bool Arrived(UnitContext ctx, Vector3 pos);
		void ResetPath(UnitContext ctx);
		void SyncWalkingAnim(UnitContext ctx, bool isWalking);
		void OverrideStoppingDistance(UnitContext ctx, float tempValue);
		void RestoreStoppingDistance(UnitContext ctx);
	}

	public struct CoverCandidate
	{
		public Vector3 position;
		public Cover cover;
	}

	public interface ICoverService
	{
		/// <summary>
		/// Select the best cover candidate and return navmesh point behind it.
		/// Returns null if nothing suitable found.
		/// </summary>
		CoverCandidate? FindBest(UnitContext ctx, Vector3 fromPos, Vector3 enemyPos, float radius);

		/// <summary>Try to occupy a cover (sets occupant and ctx.CurrentCover).</summary>
		bool Occupy(UnitContext ctx, Cover cov);

		/// <summary>Release current cover if owned by this unit.</summary>
		void Release(UnitContext ctx);
	}

	public interface ISensorService
	{
		/// <summary>Find nearest enemy transform within radius (center-to-center).</summary>
		Transform FindNearestEnemy(UnitContext ctx, float radius);
	}

	public interface ICombatService
	{
		/// <summary>Try to attack the current target if cooldown allows.</summary>
		void TryAttack(UnitContext ctx);

		/// <summary>Actual async attack (hit chance, damage application, FX hooks).</summary>
		UniTask PerformAttackAsync(UnitContext ctx, Transform target, CancellationToken ct);
	}
}