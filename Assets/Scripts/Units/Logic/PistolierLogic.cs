using Cysharp.Threading.Tasks;
using Helpers;
using Units.Data;
using Units.Logic.Core;
using Units.Logic.Services;
using UnityEngine;
using UnityEngine.AI;

namespace Units.Logic
{
    /// <summary>
    /// Ranged pistol unit. Reuses the modular controller and injects FX hook for shots.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class PistolierLogic : UnitController
    {
        [Header("FX")]
        [Tooltip("Assign a muzzle flash GameObject (child on the unit prefab). It must have a ParticleSystem.")]
        public GameObject muzzleFlashInstance;

        private PistolierData _pistolierData;

        protected override void Awake()
        {
            // Do NOT touch _muzzleEffect here; the combat service will build it.
            base.Awake();

            _pistolierData = Context.UnitData as PistolierData;
            if (_pistolierData == null)
                Debug.LogWarning($"{name}: assigned UnitData is not PistolierData (or is null).", this);
        }

        /// <summary>
        /// Build the combat service and safely prepare the FX component here.
        /// This runs during base.Awake(), so we must not rely on fields set after base.Awake().
        /// </summary>
        protected override ICombatService CreateCombatService()
        {
            ReusableEffect fx = null;

            if (muzzleFlashInstance != null)
            {
                // Ensure the FX component exists (and a ParticleSystem is present due to RequireComponent)
                fx = muzzleFlashInstance.GetComponent<ReusableEffect>();
                if (fx == null)
                    fx = muzzleFlashInstance.AddComponent<ReusableEffect>();

            }
            else
            {
                Debug.LogWarning($"{name}: muzzleFlashInstance is not assigned. No muzzle FX will play.", this);
            }

            return new PistolCombatService(fx);
        }

        public int GetSpawnCountForLevel()
        {
            if (_pistolierData == null) return 1;
            return _pistolierData.GetSpawnCountForLevel(Context.Level);
        }

        /// <summary>
        /// Custom combat service that plays muzzle flash right when the shot triggers.
        /// </summary>
        private sealed class PistolCombatService : CombatService
        {
            private readonly ReusableEffect _fx;
            public PistolCombatService(ReusableEffect fx) => _fx = fx;

            protected override void OnBeforeAttackFx(UnitContext ctx)
            {
                _fx?.Play();
            }
        }
    }
}
