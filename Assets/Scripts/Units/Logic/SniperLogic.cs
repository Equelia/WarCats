using Cysharp.Threading.Tasks;
using Units.Data;
using Units.Logic.Core;
using Units.Logic.Services;
using UnityEngine;
using UnityEngine.AI;

namespace Units.Logic
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class SniperLogic : UnitController
    {
        [Header("VFX / Prefabs")]
        [Tooltip("Префаб визуальной пули. Если нет VisualBullet — добавим.")]
        public GameObject projectilePrefab;

        [Tooltip("VFX попадания по врагу.")]
        public GameObject hitEnemyVfxPrefab;

        [Tooltip("VFX попадания по окружению.")]
        public GameObject hitEnvVfxPrefab;

        [Tooltip("Вспышка на стволе.")]
        public GameObject muzzleFlashInstance;

        [Tooltip("Дым выстрела (опц.).")]
        public GameObject muzzleSmokeInstance;

        [Tooltip("VFX появления юнита.")]
        public GameObject spawnVfxPrefab;

        [Header("Shoot")]
        [Tooltip("Точка вылета пули.")]
        public Transform firePoint;

        [Header("Targeting visuals")]
        [Tooltip("Куда целимся на цели (голова/грудь).")]
        public Vector3 targetOffset = new Vector3(0f, 1.4f, 0f);

        [Tooltip("Маска окружения для промаха.")]
        public LayerMask hitMask = ~0;

        private SniperData _data;
        private ReusableEffect _muzzleFx;
        private ReusableEffect _muzzleSmokeFx;

        protected override void Awake()
        {
            if (muzzleFlashInstance)
                _muzzleFx = muzzleFlashInstance.GetComponent<ReusableEffect>() ?? muzzleFlashInstance.AddComponent<ReusableEffect>();
            if (muzzleSmokeInstance)
                _muzzleSmokeFx = muzzleSmokeInstance.GetComponent<ReusableEffect>() ?? muzzleSmokeInstance.AddComponent<ReusableEffect>();

            base.Awake();

            _data = Context.UnitData as SniperData;
            if (_data == null)
                Debug.LogWarning($"{name}: UnitData is not SniperData (or null).", this);

            if (spawnVfxPrefab)
                VfxPlayer.SpawnOneShot(spawnVfxPrefab, transform.position, transform.rotation);
        }

        protected override ICombatService CreateCombatService() => new SniperCombatService(this);

        private sealed class SniperCombatService : CombatService
        {
            private readonly SniperLogic _owner;
            private ReusableEffect _muzzleFx;
            private ReusableEffect _muzzleSmokeFx;

            public SniperCombatService(SniperLogic owner) { _owner = owner; }

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
                if (!_owner.projectilePrefab) return;

                Vector3 origin = _owner.firePoint ? _owner.firePoint.position : ctx.Transform.position;
                Vector3 forward = _owner.firePoint ? _owner.firePoint.forward : ctx.Transform.forward;

                var go = VfxPool.Get(_owner.projectilePrefab, origin, Quaternion.LookRotation(forward, Vector3.up));
                var vb = go.GetComponent<VisualBullet>();
                if (!vb) vb = go.AddComponent<VisualBullet>();

                vb.ResetTrailIfAny();
                float speed = _owner._data ? _owner._data.bulletSpeed : 85f;
                vb.SetSpeed(speed);
                vb.LaunchLinear(origin, forward);

                Vector3 aimPoint;
                bool spawnEnemyImpact = didHit;

                if (target)
                {
                    aimPoint = target.position + _owner.targetOffset;
                }
                else
                {
                    aimPoint = origin + forward * Mathf.Max(10f, ctx.Stats.attackRange);
                    spawnEnemyImpact = false;
                }

                Vector3 endPoint = aimPoint;
                Vector3 impactNormal = -((aimPoint - origin).normalized);

                if (target && didHit)
                {
                    var col = target.GetComponentInChildren<Collider>();
                    if (col)
                    {
                        Vector3 closest = col.ClosestPoint(aimPoint);
                        endPoint = closest;
                        impactNormal = -((aimPoint - origin).normalized);
                    }
                }
                else if (!didHit)
                {
                    // аккуратный промах — ищем поверхность
                    if (Physics.Raycast(aimPoint + Vector3.up * 2f, Vector3.down, out var hit, 12f, _owner.hitMask, QueryTriggerInteraction.Ignore))
                    {
                        endPoint = hit.point;
                        impactNormal = hit.normal;
                    }
                }

                vb.FlyTo(endPoint, onArrive: () =>
                {
                    if (spawnEnemyImpact)
                    {
                        if (_owner.hitEnemyVfxPrefab)
                            VfxPlayer.SpawnOneShot(_owner.hitEnemyVfxPrefab, endPoint, Quaternion.LookRotation(impactNormal));
                    }
                    else
                    {
                        if (_owner.hitEnvVfxPrefab)
                            VfxPlayer.SpawnOneShot(_owner.hitEnvVfxPrefab, endPoint, Quaternion.LookRotation(impactNormal));
                    }
                });
            }
        }
    }
}
