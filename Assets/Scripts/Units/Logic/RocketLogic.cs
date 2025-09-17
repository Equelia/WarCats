using System.Linq;
using Cysharp.Threading.Tasks;
using Units.Data;
using Units.Logic.Core;
using Units.Logic.Services;
using UnityEngine;
using UnityEngine.AI;

namespace Units.Logic
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class RocketLogic : RangedUnitController
    {
        public GameObject explosionVfxPrefab;
        public LayerMask enemyUnitMask = ~0;

        private RocketData _data;
        protected override void OnRangedBuilt() { _data = Context.UnitData as RocketData; }
        protected override ICombatService CreateCombatService() => new RocketCombatService(this);

        private sealed class RocketCombatService : RangedCombatServiceBase
        {
            public RocketCombatService(RocketLogic owner) : base(owner) {}

            protected override void OnShotResolvedFx(UnitContext ctx, Transform target, bool didHit)
            {
                _ = PlayAsync((RocketLogic)Owner, ctx, target, didHit);
            }

            private async UniTaskVoid PlayAsync(RocketLogic o, UnitContext ctx, Transform target, bool didHit)
            {
                var vb = o.SpawnBullet(ctx, o._data ? o._data.rocketSpeed : 25f);
                if (!vb) return;

                var (origin, forward) = o.GetOriginForward(ctx);
                Vector3 aim = target ? target.position + new Vector3(0,1f,0)
                                     : origin + forward * Mathf.Max(8f, ctx.Stats.attackRange);

                if (target && !didHit)
                {
                    if (Physics.Raycast(aim + Vector3.up*3f, Vector3.down, out var hit, 8f, o.hitMask))
                        aim = hit.point;
                }

                var end = aim;
                vb.FlyTo(end, () =>
                {
                    if (o.explosionVfxPrefab)
                        VfxPlayer.SpawnOneShot(o.explosionVfxPrefab, end, Quaternion.identity);

                    float r = o._data ? Mathf.Max(0.1f, o._data.splashRadius) : 2.5f;
                    foreach (var h in Physics.OverlapSphere(end, r, o.enemyUnitMask).Distinct())
                    {
                        var u = h.GetComponentInParent<UnitController>();
                        if (u == null || u.TeamId == ctx.TeamId) continue;
                        u.ReceiveDamage(ctx.Stats.damage);
                    }
                });
            }
        }
    }
}
