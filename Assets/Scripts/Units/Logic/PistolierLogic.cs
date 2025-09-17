using Units.Data;
using Units.Logic.Core;
using Units.Logic.Services;
using UnityEngine;
using UnityEngine.AI;

namespace Units.Logic
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class PistolierLogic : RangedUnitController
    {
        [Range(0f,1f)] public float missRadius = 0.6f;
        public float bulletSpeed = 45f;

        private PistolierData _data;
        protected override void OnRangedBuilt() { _data = Context.UnitData as PistolierData; }

        protected override ICombatService CreateCombatService() => new PistolCombatService(this);

        private sealed class PistolCombatService : RangedCombatServiceBase
        {
            public PistolCombatService(PistolierLogic owner) : base(owner) {}

            protected override void OnShotResolvedFx(UnitContext ctx, Transform target, bool didHit)
            {
                var owner = (PistolierLogic)Owner;
                var vb = owner.SpawnBullet(ctx, owner.bulletSpeed);
                if (!vb) return;

                var (origin, forward) = owner.GetOriginForward(ctx);
                var aim = target ? target.position + owner.targetOffset
                                 : origin + forward * Mathf.Max(5f, ctx.Stats.attackRange);

                bool enemyImpact = didHit;
                Vector3 end = aim, normal = -((aim - origin).normalized);

                if (target && !didHit)
                {
                    var miss = aim + (Vector3) (Random.insideUnitCircle * Mathf.Max(0.05f, owner.missRadius));
                    if (Physics.Raycast(miss + Vector3.up*2f, Vector3.down, out var hit, 6f, owner.hitMask))
                    { end = hit.point; normal = hit.normal; } else { end = miss; normal = Vector3.up; }
                    enemyImpact = false;
                }
                else if (target && didHit)
                {
                    var col = target.GetComponentInChildren<Collider>();
                    if (col) { end = col.ClosestPoint(aim); normal = -((aim - origin).normalized); }
                }

                vb.FlyTo(end, () =>
                {
                    var vfx = enemyImpact ? owner.hitEnemyVfxPrefab : owner.hitEnvVfxPrefab;
                    if (vfx) VfxPlayer.SpawnOneShot(vfx, end, Quaternion.LookRotation(normal));
                });
            }
        }
    }
}
