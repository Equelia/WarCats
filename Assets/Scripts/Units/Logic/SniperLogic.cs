using Units.Data;
using Units.Logic.Core;
using Units.Logic.Services;
using UnityEngine;
using UnityEngine.AI;

namespace Units.Logic
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class SniperLogic : RangedUnitController
    {
        private SniperData _data;

        protected override void OnRangedBuilt()
        {
            _data = Context.UnitData as SniperData;
        }

        protected override ICombatService CreateCombatService() => new SniperCombatService(this);

        private sealed class SniperCombatService : RangedCombatServiceBase
        {
            public SniperCombatService(SniperLogic owner) : base(owner) {}

            protected override void OnShotResolvedFx(UnitContext ctx, Transform target, bool didHit)
            {
                var owner = (SniperLogic)Owner;
                var vb = owner.SpawnBullet(ctx, owner._data ? owner._data.bulletSpeed : 85f);
                if (!vb) return;

                var (origin, _) = owner.GetOriginForward(ctx);
                var aim = target ? target.position + owner.targetOffset
                                 : origin + ctx.Transform.forward * Mathf.Max(10f, ctx.Stats.attackRange);

                var end = aim;
                var normal = -((aim - origin).normalized);

                if (target && didHit)
                {
                    var col = target.GetComponentInChildren<Collider>();
                    if (col) end = col.ClosestPoint(aim);
                }
                else if (!didHit)
                {
                    if (Physics.Raycast(aim + Vector3.up*2f, Vector3.down, out var hit, 12f, owner.hitMask))
                    { end = hit.point; normal = hit.normal; }
                }

                vb.FlyTo(end, () =>
                {
                    var vfx = didHit ? owner.hitEnemyVfxPrefab : owner.hitEnvVfxPrefab;
                    if (vfx) VfxPlayer.SpawnOneShot(vfx, end, Quaternion.LookRotation(normal));
                });
            }
        }
    }
}
