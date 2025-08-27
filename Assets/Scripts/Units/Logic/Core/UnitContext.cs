using System.Threading;
using Helpers;
using Units.Data;
using UnityEngine;
using UnityEngine.AI;

namespace Units.Logic.Core
{
    /// <summary>
    /// Shared mutable data passed between states and services.
    /// </summary>
    public sealed class UnitContext
    {
        // Static animator hashes (reused by all instances)
        public static readonly int AnimIsWalking = Animator.StringToHash("isWalking");
        public static readonly int AnimShoot     = Animator.StringToHash("shoot");
        public static readonly int AnimDie       = Animator.StringToHash("die");

        // Configuration (assigned from MonoBehaviour/Inspector)
        public UnitData UnitData;
        public int Level;
        public int TeamId;
        public bool DebugCover;
        public float CoverSearchRadius;
        public float CoverSeekDistance;
        public float CoverExcludeAngleDeg;

        // Runtime references
        public Transform Transform;
        public Animator Animator;
        public NavMeshAgent Agent;
        public Transform EnemyBase;
        public CancellationTokenSource Cts;

        // Runtime stats & state
        public UnitRuntimeStats Stats;
        public int CurrentHealth;
        public Transform CurrentTarget;
        public Vector3 MoveTargetPos;

        // Cover ownership & workflow
        public Cover CurrentCover;           // actually occupied cover
        public Cover DesiredCover;           // target cover to occupy after reaching nav point
        public float PrevStoppingDistance = -1f;
        public float CoverSearchStartTime = -999f;

        // Combat meta
        public float LastAttackTime = -999f;

        // Lifecycle
        public bool IsInitialized;

        public void FaceTowards(Vector3 worldPos, float slerp = 10f)
        {
            Vector3 dir = worldPos - Transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) return;
            Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
            Transform.rotation = Quaternion.Slerp(Transform.rotation, targetRot, Time.deltaTime * slerp);
        }

        public float GetEffectiveVulnerability()
        {
            float v = Stats.vulnerability;
            if (CurrentCover != null) v += CurrentCover.protection;
            return Mathf.Clamp01(v);
        }
    }
}
