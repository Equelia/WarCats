using System.Threading;
using Cysharp.Threading.Tasks;
using Helpers;
using Units.Data;
using UnityEngine;
using UnityEngine.AI;

namespace Units.Logic
{
    /// <summary>
    /// Ranged pistol unit that uses UnitLogic state machine and NavMeshAgent.
    /// Plays muzzle flash from a reusable instance and uses UniTask for async attack.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class PistolierLogic : UnitLogic
    {
        [Header("FX")]
        [Tooltip("Assign the muzzle flash GameObject (child on the unit prefab). Keep it inactive in prefab.")]
        public GameObject muzzleFlashInstance;

        private ReusableEffect _muzzleEffect;
        private PistolierData pistolierData => unitData as PistolierData;

        protected override void Awake()
        {
            base.Awake();

            if (pistolierData == null)
                Debug.LogWarning($"{name}: assigned UnitData is not PistolierData (or is null).", this);

            // locate ReusableEffect on the assigned instance (or add it)
            if (muzzleFlashInstance != null)
            {
                _muzzleEffect = muzzleFlashInstance.GetComponent<ReusableEffect>();
                if (_muzzleEffect == null)
                {
                    _muzzleEffect = muzzleFlashInstance.AddComponent<ReusableEffect>();
                }

                // ensure instance is initially inactive so it doesn't play until requested
                muzzleFlashInstance.SetActive(false);
            }
        }

        /// <summary>
        /// Override to play muzzle flash before applying damage logic in base.PerformAttackAsync.
        /// </summary>
        protected override async UniTask PerformAttackAsync(Transform target, CancellationToken ct)
        {
            // play muzzle effect immediately (fire-and-forget handled by ReusableEffect)
            if (_muzzleEffect != null)
            {
                _muzzleEffect.Play();
            }

            // small delay so FX/animation feels synced - tune as needed (e.g. 0..200 ms)
            await UniTask.DelayFrame(1, cancellationToken: ct); // one frame delay

            // call base logic (damage calculation using vulnerability)
            await base.PerformAttackAsync(target, ct);
        }

        public int GetSpawnCountForLevel()
        {
            if (pistolierData == null) return 1;
            return pistolierData.GetSpawnCountForLevel(level);
        }
    }
}
