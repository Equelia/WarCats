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
    public class RocketLogic : UnitController
    {
        [Header("VFX / Prefabs")]
        [Tooltip("Префаб визуальной ракеты. Если нет VisualBullet — добавим автоматически.")]
        public GameObject rocketProjectilePrefab;

        [Tooltip("VFX взрыва.")]
        public GameObject explosionVfxPrefab;

        [Tooltip("Вспышка на оружии (проигрывает все дочерние ParticleSystem).")]
        public GameObject muzzleFlashInstance;

        [Tooltip("Дым от выстрела (опционально).")]
        public GameObject muzzleSmokeInstance;

        [Tooltip("VFX появления юнита.")]
        public GameObject spawnVfxPrefab;

        [Header("Shoot")]
        [Tooltip("Точка вылета ракеты.")]
        public Transform firePoint;

        [Header("Masks")]
        [Tooltip("Поверхности для приземления/взрыва.")]
        public LayerMask hitMask = ~0;

        [Tooltip("Слои юнитов противника (для AoE-поражения). Можно оставить ~0, фильтруем кодом по TeamId.")]
        public LayerMask enemyUnitMask = ~0;

        private RocketData _data;
        private ReusableEffect _muzzleFx;
        private ReusableEffect _muzzleSmokeFx;

        protected override void Awake()
        {
            if (muzzleFlashInstance)
                _muzzleFx = muzzleFlashInstance.GetComponent<ReusableEffect>() ?? muzzleFlashInstance.AddComponent<ReusableEffect>();
            if (muzzleSmokeInstance)
                _muzzleSmokeFx = muzzleSmokeInstance.GetComponent<ReusableEffect>() ?? muzzleSmokeInstance.AddComponent<ReusableEffect>();

            base.Awake();

            _data = Context.UnitData as RocketData;
            if (_data == null)
                Debug.LogWarning($"{name}: UnitData is not RocketData (or null).", this);

            if (spawnVfxPrefab)
                VfxPlayer.SpawnOneShot(spawnVfxPrefab, transform.position, transform.rotation);
        }

        protected override ICombatService CreateCombatService() => new RocketCombatService(this);

        public int GetSpawnCountForLevel()
        {
            return _data ? _data.GetSpawnCountForLevel(Context.Level) : 1;
        }

        private sealed class RocketCombatService : CombatService
        {
            private readonly RocketLogic _owner;
            private ReusableEffect _muzzleFx;
            private ReusableEffect _muzzleSmokeFx;

            public RocketCombatService(RocketLogic owner) { _owner = owner; }

            protected override void OnBeforeAttackFx(UnitContext ctx)
            {
                if (_muzzleFx == null && _owner.muzzleFlashInstance)
                    _muzzleFx = _owner.muzzleFlashInstance.GetComponent<ReusableEffect>();
                if (_muzzleSmokeFx == null && _owner.muzzleSmokeInstance)
                    _muzzleSmokeFx = _owner.muzzleSmokeInstance.GetComponent<ReusableEffect>();

                _muzzleFx?.Play();
                _muzzleSmokeFx?.Play();
            }

            protected override void OnShotResolvedFx(UnitContext ctx, Transform target, bool didHit)
            {
                _ = PlayRocketFxAsync(ctx, target, didHit);
            }

            private async UniTaskVoid PlayRocketFxAsync(UnitContext ctx, Transform target, bool didHit)
            {
                if (!_owner.rocketProjectilePrefab)  return;

                Vector3 origin = _owner.firePoint ? _owner.firePoint.position : ctx.Transform.position;
                Vector3 forward = _owner.firePoint ? _owner.firePoint.forward : ctx.Transform.forward;

                var go = VfxPool.Get(_owner.rocketProjectilePrefab, origin, Quaternion.LookRotation(forward, Vector3.up));
                var vb = go.GetComponent<VisualBullet>();
                if (!vb) vb = go.AddComponent<VisualBullet>();

                vb.ResetTrailIfAny();
                float speed = _owner._data ? _owner._data.rocketSpeed : 25f;
                vb.SetSpeed(speed);
                vb.LaunchLinear(origin, forward);

                // Куда летим
                Vector3 endPoint;
                if (target)
                {
                    // если промазали — ищем землю рядом
                    Vector3 aimPoint = target.position + new Vector3(0, 1.0f, 0);
                    if (!didHit)
                    {
                        if (Physics.Raycast(aimPoint + Vector3.up * 3f, Vector3.down, out var hit, 8f, _owner.hitMask, QueryTriggerInteraction.Ignore))
                            aimPoint = hit.point;
                    }
                    endPoint = aimPoint;
                }
                else
                {
                    endPoint = origin + forward * Mathf.Max(8f, ctx.Stats.attackRange);
                }

                vb.FlyTo(endPoint, onArrive: () =>
                {
                    // VFX взрыва
                    if (_owner.explosionVfxPrefab)
                        VfxPlayer.SpawnOneShot(_owner.explosionVfxPrefab, endPoint, Quaternion.identity);

                    // AOE-урон (простой, фильтруем по TeamId)
                    float radius = _owner._data ? Mathf.Max(0.1f, _owner._data.splashRadius) : 2.5f;
                    var hits = Physics.OverlapSphere(endPoint, radius, _owner.enemyUnitMask, QueryTriggerInteraction.Ignore);

                    foreach (var h in hits.Distinct())
                    {
                        var other = h.GetComponentInParent<UnitController>();
                        if (!other) continue;
                        if (other.TeamId == ctx.TeamId) continue; // не бьём своих

                        // простой вариант — полный урон всем попавшимся (можно сделать убывание по дистанции)
                        other.ReceiveDamage(ctx.Stats.damage);
                    }
                });
            }
        }
    }
}
