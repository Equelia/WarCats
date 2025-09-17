using Cysharp.Threading.Tasks;
using Units.Data;
using Units.Logic.Core;
using Units.Logic.Services;
using UnityEngine;
using UnityEngine.AI;

namespace Units.Logic
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class AutomaticLogic : RangedUnitController
    {
        private AutomaticData _data;
        protected override void OnRangedBuilt() { _data = Context.UnitData as AutomaticData; }
        protected override ICombatService CreateCombatService() => new AutomaticCombatService(this);

        private sealed class AutomaticCombatService : RangedCombatServiceBase
        {
            public AutomaticCombatService(AutomaticLogic owner) : base(owner) {}

            protected override void OnShotResolvedFx(UnitContext ctx, Transform target, bool didHit)
            {
                _ = PlayBurstAsync((AutomaticLogic)Owner, ctx, target, didHit);
            }

            private async UniTaskVoid PlayBurstAsync(AutomaticLogic o, UnitContext ctx, Transform target, bool didHit)
            {
                var data = o._data; if (data == null) return;

                int burst = Mathf.Max(1, data.burstCount);
                float gap = Mathf.Max(0f, data.burstInterval);

                // shot #1 follows didHit, others – just visuals with spread
                FireOne(o, ctx, target, didHit, true, data);
                for (int i = 1; i < burst; i++)
                {
                    await UniTask.Delay((int)(gap * 1000));
                    FireOne(o, ctx, target, false, false, data);
                }
            }

            private void FireOne(AutomaticLogic o, UnitContext ctx, Transform target, bool didHit, bool exact, AutomaticData data)
            {
                var vb = o.SpawnBullet(ctx, data.bulletSpeed);
                if (!vb) return;

                var (origin, forward) = o.GetOriginForward(ctx);
                var aim = target ? target.position + o.targetOffset
                                 : origin + forward * Mathf.Max(5f, ctx.Stats.attackRange);

                if (!exact && target)
                {
                    var c = Random.insideUnitCircle * Mathf.Max(0.02f, data.spreadRadius);
                    aim = new Vector3(aim.x + c.x, aim.y, aim.z + c.y);
                }

                bool enemyImpact = didHit && exact;
                Vector3 end = aim, normal = -((aim - origin).normalized);

                if (target)
                {
                    if (enemyImpact)
                    {
                        var col = target.GetComponentInChildren<Collider>();
                        if (col) end = col.ClosestPoint(aim);
                    }
                    else if (Physics.Raycast(aim + Vector3.up*2f, Vector3.down, out var hit, 6f, o.hitMask))
                    {
                        end = hit.point; normal = hit.normal;
                    }
                }

                vb.FlyTo(end, () =>
                {
                    var vfx = enemyImpact ? o.hitEnemyVfxPrefab : o.hitEnvVfxPrefab;
                    if (vfx) VfxPlayer.SpawnOneShot(vfx, end, Quaternion.LookRotation(normal));
                });
            }
        }
    }
}
