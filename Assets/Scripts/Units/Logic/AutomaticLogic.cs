using Cysharp.Threading.Tasks;
using Units.Data;
using Units.Logic.Core;
using Units.Logic.Services;
using UnityEngine;
using UnityEngine.AI;

namespace Units.Logic
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class AutomaticLogic : UnitController
    {
        [Header("VFX - Prefabs")]
        [Tooltip("Префаб визуальной пули с VisualBullet (добавится автоматически, если нет).")]
        public GameObject projectilePrefab;

        [Tooltip("VFX попадания по врагу.")]
        public GameObject hitEnemyVfxPrefab;

        [Tooltip("VFX попадания по окружению.")]
        public GameObject hitEnvVfxPrefab;

        [Tooltip("Деталь на оружии для вспышки. Воспроизводит все дочерние ParticleSystem.")]
        public GameObject muzzleFlashInstance;

        [Tooltip("Деталь с дымом (опционально). Воспроизводит все дочерние ParticleSystem.")]
        public GameObject muzzleSmokeInstance;

        [Tooltip("VFX появления юнита.")]
        public GameObject spawnVfxPrefab;

        [Header("Firing")]
        [Tooltip("Точка вылета пули. Если null — берём transform юнита.")]
        public Transform firePoint;

        [Header("Targeting visuals")]
        [Tooltip("Смещение точки прицеливания на цели (например, грудь).")]
        public Vector3 targetOffset = new Vector3(0f, 1.2f, 0f);

        [Tooltip("Лэйер-маска для поверхностей попаданий (земля/укрытия).")]
        public LayerMask hitMask = ~0;

        private AutomaticData _autoData;
        private ReusableEffect _muzzleFx;
        private ReusableEffect _muzzleSmokeFx;

        protected override void Awake()
        {
            // готовим эффекты ДО base.Awake()
            if (muzzleFlashInstance)
                _muzzleFx = muzzleFlashInstance.GetComponent<ReusableEffect>() ??
                            muzzleFlashInstance.AddComponent<ReusableEffect>();
            if (muzzleSmokeInstance)
                _muzzleSmokeFx = muzzleSmokeInstance.GetComponent<ReusableEffect>() ??
                                 muzzleSmokeInstance.AddComponent<ReusableEffect>();

            base.Awake();

            _autoData = Context.UnitData as AutomaticData;
            if (_autoData == null)
                Debug.LogWarning($"{name}: UnitData is not AutomaticData (or null).", this);

            if (spawnVfxPrefab)
                VfxPlayer.SpawnOneShot(spawnVfxPrefab, transform.position, transform.rotation);
        }

        protected override ICombatService CreateCombatService()
        {
            return new AutomaticCombatService(this);
        }

        private sealed class AutomaticCombatService : CombatService
        {
            private readonly AutomaticLogic _owner;
            private ReusableEffect _muzzleFx;
            private ReusableEffect _muzzleSmokeFx;

            public AutomaticCombatService(AutomaticLogic owner)
            {
                _owner = owner;
            }

            protected override void OnBeforeAttackFx(UnitContext ctx)
            {
                // ленивое восстановление ссылок
                if (_muzzleFx == null && _owner.muzzleFlashInstance)
                    _muzzleFx = _owner.muzzleFlashInstance.GetComponent<ReusableEffect>();
                if (_muzzleSmokeFx == null && _owner.muzzleSmokeInstance)
                    _muzzleSmokeFx = _owner.muzzleSmokeInstance.GetComponent<ReusableEffect>();

                _muzzleFx?.Play();
                _muzzleSmokeFx?.Play();
            }

            /// <summary>
            /// Базовый CombatService уже решил, попал ли выстрел (didHit) по цели.
            /// Здесь мы отрисуем «очередь»: первая пуля совпадает с решением (hit/miss),
            /// остальные — визуальные, с лёгким разбросом вокруг цели.
            /// </summary>
            protected override void OnShotResolvedFx(UnitContext ctx, Transform target, bool didHit)
            {
                _ = PlayBurstFxAsync(ctx, target, didHit);
            }

            private async UniTaskVoid PlayBurstFxAsync(UnitContext ctx, Transform target, bool didHit)
            {
                var data = _owner._autoData;
                if (data == null)  return;

                int burst = Mathf.Max(1, data.burstCount);
                float interval = Mathf.Max(0.0f, data.burstInterval);
                float speed = data.bulletSpeed;

                Vector3 origin() => _owner.firePoint ? _owner.firePoint.position : ctx.Transform.position;
                Vector3 forward() => _owner.firePoint ? _owner.firePoint.forward : ctx.Transform.forward;

                // Пуля №1 — «главная»: следует результату didHit
                SpawnOneBulletFx(ctx, target, didHit, origin(), forward(), speed, exactAim: true, data);

                // Остальные — со спредом вокруг aim-точки
                for (int i = 1; i < burst; i++)
                {
                    await UniTask.Delay((int)(interval * 1000));
                    SpawnOneBulletFx(ctx, target, didHit: false, origin(), forward(), speed, exactAim: false, data);
                }
            }

            private void SpawnOneBulletFx(
                UnitContext ctx,
                Transform target,
                bool didHit,
                Vector3 origin,
                Vector3 forward,
                float bulletSpeed,
                bool exactAim,
                AutomaticData data)
            {
                if (!_owner.projectilePrefab) return;

                var go = VfxPool.Get(
                    _owner.projectilePrefab,
                    origin,
                    Quaternion.LookRotation(forward, Vector3.up)
                );

                var vb = go.GetComponent<VisualBullet>();
                if (!vb) vb = go.AddComponent<VisualBullet>();

                vb.ResetTrailIfAny();
                vb.SetSpeed(bulletSpeed);

                // начальное направление
                vb.LaunchLinear(origin, forward);

                // куда летим?
                Vector3 aimPoint;
                if (target)
                {
                    aimPoint = target.position + _owner.targetOffset;

                    if (!exactAim) // «шальная» пуля — немного рядом
                    {
                        Vector2 c = Random.insideUnitCircle * Mathf.Max(0.02f, data.spreadRadius);
                        aimPoint = new Vector3(aimPoint.x + c.x, aimPoint.y, aimPoint.z + c.y);
                    }
                }
                else
                {
                    // нет цели — стреляем вперёд
                    float dist = Mathf.Max(5f, ctx.Stats.attackRange * (exactAim ? 1f : 0.8f));
                    aimPoint = origin + forward * dist;
                }

                Vector3 endPoint = aimPoint;
                Vector3 impactNormal = -((aimPoint - origin).normalized);
                bool spawnEnemyImpact = didHit && exactAim;

                if (target)
                {
                    if (spawnEnemyImpact)
                    {
                        var col = target.GetComponentInChildren<Collider>();
                        if (col != null)
                        {
                            Vector3 closest = col.ClosestPoint(aimPoint);
                            endPoint = closest;
                            impactNormal = -((aimPoint - origin).normalized);
                        }
                    }
                    else
                    {
                        // промах: ищем пол/укрытие вниз от точки около цели
                        if (Physics.Raycast(
                                aimPoint + Vector3.up * 2f,
                                Vector3.down,
                                out var hit,
                                6f,
                                _owner.hitMask,
                                QueryTriggerInteraction.Ignore))
                        {
                            endPoint = hit.point;
                            impactNormal = hit.normal;
                        }
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
