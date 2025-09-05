using Units.Data;
using Units.Logic.Core;
using Units.Logic.Services;
using UnityEngine;
using UnityEngine.AI;

namespace Units.Logic
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class PistolierLogic : UnitController
    {
        [Header("VFX - Prefabs")]
        [Tooltip("Projectile prefab with VisualBullet component (or it will be added automatically).")]
        public GameObject projectilePrefab;
        [Tooltip("Impact VFX when hitting an enemy.")]
        public GameObject hitEnemyVfxPrefab;
        [Tooltip("Impact VFX when missing (hitting environment).")]
        public GameObject hitEnvVfxPrefab;
        [Tooltip("Child object on the weapon; plays all child ParticleSystems.")]
        public GameObject muzzleFlashInstance;
        [Tooltip("Optional child object with smoke; plays all child ParticleSystems.")]
        public GameObject muzzleSmokeInstance;
        [Tooltip("Spawn VFX for this unit.")]
        public GameObject spawnVfxPrefab;

        [Header("Firing")]
        [Tooltip("Where the projectile should appear from. If null, uses this.transform.")]
        public Transform firePoint;

        [Header("Targeting visuals")]
        [Range(0f, 1f)]
        [Tooltip("Miss radius around target on ground surfaces.")]
        public float missRadius = 0.6f;
        [Tooltip("Where to aim on the target (e.g., chest).")]
        public Vector3 targetOffset = new Vector3(0f, 1.2f, 0f);
        [Tooltip("Visual bullet speed.")]
        public float bulletSpeed = 45f;

        [Header("Masks")]
        [Tooltip("Surfaces for miss impact placement (Ground, Cover, Props, etc.).")]
        public LayerMask hitMask = ~0;
        [Tooltip("Enemy layer mask (kept for future).")]
        public LayerMask enemyMask = 0;

        private PistolierData _pistolierData;
        private ReusableEffect _muzzleFx;
        private ReusableEffect _muzzleSmokeFx;

        protected override void Awake()
        {
            // IMPORTANT: prepare muzzle effects BEFORE base.Awake(),
            // because base.Awake() builds the combat service.
            if (muzzleFlashInstance)
                _muzzleFx = muzzleFlashInstance.GetComponent<ReusableEffect>() ??
                            muzzleFlashInstance.AddComponent<ReusableEffect>();
            if (muzzleSmokeInstance)
                _muzzleSmokeFx = muzzleSmokeInstance.GetComponent<ReusableEffect>() ??
                                 muzzleSmokeInstance.AddComponent<ReusableEffect>();

            // Build context/services/FSM
            base.Awake();

            _pistolierData = Context.UnitData as PistolierData;
            if (_pistolierData == null)
                Debug.LogWarning($"{name}: assigned UnitData is not PistolierData (or is null).", this);

            if (spawnVfxPrefab)
                VfxPlayer.SpawnOneShot(spawnVfxPrefab, transform.position, transform.rotation);
        }

        protected override ICombatService CreateCombatService()
        {
            // Service now receives properly prepared muzzle effects
            return new PistolCombatService(
                this,
                _muzzleFx,
                _muzzleSmokeFx,
                projectilePrefab,
                hitEnemyVfxPrefab,
                hitEnvVfxPrefab
            );
        }

        public int GetSpawnCountForLevel()
        {
            if (_pistolierData == null) return 1;
            return _pistolierData.GetSpawnCountForLevel(Context.Level);
        }

        private sealed class PistolCombatService : CombatService
        {
            private readonly PistolierLogic _owner;
            private ReusableEffect _muzzleFx;
            private ReusableEffect _muzzleSmokeFx;
            private readonly GameObject _projectilePrefab;
            private readonly GameObject _hitEnemyVfx;
            private readonly GameObject _hitEnvVfx;

            private VisualBullet _pendingBullet;

            public PistolCombatService(
                PistolierLogic owner,
                ReusableEffect muzzleFx,
                ReusableEffect muzzleSmokeFx,
                GameObject projectilePrefab,
                GameObject hitEnemyVfx,
                GameObject hitEnvVfx)
            {
                _owner = owner;
                _muzzleFx = muzzleFx;
                _muzzleSmokeFx = muzzleSmokeFx;
                _projectilePrefab = projectilePrefab;
                _hitEnemyVfx = hitEnemyVfx;
                _hitEnvVfx = hitEnvVfx;
            }

            protected override void OnBeforeAttackFx(UnitContext ctx)
            {
                // Lazy fallback in case references were not ready at construction time
                if (_muzzleFx == null && _owner.muzzleFlashInstance)
                    _muzzleFx = _owner.muzzleFlashInstance.GetComponent<ReusableEffect>();
                if (_muzzleSmokeFx == null && _owner.muzzleSmokeInstance)
                    _muzzleSmokeFx = _owner.muzzleSmokeInstance.GetComponent<ReusableEffect>();

                _muzzleFx?.Play();
                _muzzleSmokeFx?.Play();

                if (_projectilePrefab && _owner.firePoint)
                {
                    var go = VfxPool.Get(
                        _projectilePrefab,
                        _owner.firePoint.position,
                        Quaternion.LookRotation(_owner.firePoint.forward, Vector3.up)
                    );

                    var vb = go.GetComponent<VisualBullet>();
                    if (!vb) vb = go.AddComponent<VisualBullet>();

                    vb.ResetTrailIfAny();
                    vb.SetSpeed(_owner.bulletSpeed);

                    // Start forward; final end point will be set in OnShotResolvedFx
                    vb.LaunchLinear(_owner.firePoint.position, _owner.firePoint.forward);
                    _pendingBullet = vb;
                }
            }

            protected override void OnShotResolvedFx(UnitContext ctx, Transform target, bool didHit)
            {
                if (_pendingBullet == null)
                    return;

                Vector3 origin = _owner.firePoint ? _owner.firePoint.position : ctx.Transform.position;
                Vector3 aimPoint = target
                    ? target.position + _owner.targetOffset
                    : origin + ctx.Transform.forward * Mathf.Max(5f, ctx.Stats.attackRange);

                Vector3 endPoint = aimPoint;
                Vector3 impactPos = aimPoint;
                Vector3 impactNormal = -((aimPoint - origin).normalized);
                bool spawnEnemyImpact = didHit;

                if (target)
                {
                    if (didHit)
                    {
                        var col = target.GetComponentInChildren<Collider>();
                        if (col != null)
                        {
                            Vector3 closest = col.ClosestPoint(aimPoint);
                            impactPos = closest;
                            impactNormal = -((aimPoint - origin).normalized);
                            endPoint = impactPos;
                        }
                        else
                        {
                            impactPos = aimPoint;
                            impactNormal = -((aimPoint - origin).normalized);
                            endPoint = aimPoint;
                        }
                    }
                    else
                    {
                        Vector2 c = Random.insideUnitCircle * Mathf.Max(0.05f, _owner.missRadius);
                        Vector3 missAround = new Vector3(aimPoint.x + c.x, aimPoint.y, aimPoint.z + c.y);

                        if (Physics.Raycast(
                                missAround + Vector3.up * 2f,
                                Vector3.down,
                                out var hit,
                                6f,
                                _owner.hitMask,
                                QueryTriggerInteraction.Ignore))
                        {
                            impactPos = hit.point;
                            impactNormal = hit.normal;
                            endPoint = hit.point;
                        }
                        else
                        {
                            impactPos = missAround;
                            impactNormal = Vector3.up;
                            endPoint = missAround;
                        }

                        spawnEnemyImpact = false;
                    }
                }
                else
                {
                    endPoint = origin + (_owner.firePoint ? _owner.firePoint.forward : ctx.Transform.forward) *
                               Mathf.Max(5f, ctx.Stats.attackRange);
                    impactPos = endPoint;
                    impactNormal = Vector3.up;
                    spawnEnemyImpact = false;
                }

                _pendingBullet.FlyTo(endPoint, onArrive: () =>
                {
                    if (spawnEnemyImpact)
                    {
                        if (_hitEnemyVfx)
                            VfxPlayer.SpawnOneShot(_hitEnemyVfx, impactPos, Quaternion.LookRotation(impactNormal));
                    }
                    else
                    {
                        if (_hitEnvVfx)
                            VfxPlayer.SpawnOneShot(_hitEnvVfx, impactPos, Quaternion.LookRotation(impactNormal));
                    }
                });

                _pendingBullet = null;
            }
        }
    }
}
