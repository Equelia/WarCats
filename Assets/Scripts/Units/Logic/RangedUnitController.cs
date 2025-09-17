using Units.Logic.Core;
using Units.Logic.Services;
using UnityEngine;

namespace Units.Logic
{
    public abstract class RangedUnitController : UnitController
    {
        [Header("Common VFX / Sockets")]
        public GameObject projectilePrefab;          // rocket/sniper/auto/pistol: назначаем через Bootstrapper
        public GameObject hitEnemyVfxPrefab;
        public GameObject hitEnvVfxPrefab;
        public GameObject spawnVfxPrefab;
        public GameObject muzzleFlashInstance;
        public GameObject muzzleSmokeInstance;
        public Transform firePoint;

        [Header("Common Targeting")]
        public Vector3 targetOffset = new Vector3(0f, 1.2f, 0f);
        public LayerMask hitMask = ~0;

        protected ReusableEffect _muzzleFx;
        protected ReusableEffect _muzzleSmokeFx;

        protected override void OnBuilt()
        {
            if (muzzleFlashInstance)
                _muzzleFx = muzzleFlashInstance.GetComponent<ReusableEffect>() ??
                            muzzleFlashInstance.AddComponent<ReusableEffect>();
            if (muzzleSmokeInstance)
                _muzzleSmokeFx = muzzleSmokeInstance.GetComponent<ReusableEffect>() ??
                                 muzzleSmokeInstance.AddComponent<ReusableEffect>();

            if (spawnVfxPrefab)
                VfxPlayer.SpawnOneShot(spawnVfxPrefab, transform.position, transform.rotation);

            OnRangedBuilt(); // hook for children to grab their Data etc.
        }

        /// <summary>Optional hook for children (e.g. cache typed UnitData).</summary>
        protected virtual void OnRangedBuilt() {}

        internal void PlayMuzzleFx()
        {
            _muzzleFx?.Play();
            _muzzleSmokeFx?.Play();
        }

        protected (Vector3 origin, Vector3 forward) GetOriginForward(UnitContext ctx)
        {
            var o = firePoint ? firePoint.position : ctx.Transform.position;
            var f = firePoint ? firePoint.forward  : ctx.Transform.forward;
            return (o, f);
        }

        /// <summary>Spawns a VisualBullet already launched forward. Returns null if no prefab.</summary>
        protected VisualBullet SpawnBullet(UnitContext ctx, float speed)
        {
            if (!projectilePrefab) return null;
            var (o, f) = GetOriginForward(ctx);
            var go = VfxPool.Get(projectilePrefab, o, Quaternion.LookRotation(f, Vector3.up));
            var vb = go.GetComponent<VisualBullet>() ?? go.AddComponent<VisualBullet>();
            vb.ResetTrailIfAny();
            vb.SetSpeed(speed);
            vb.LaunchLinear(o, f);
            return vb;
        }
    }
}
