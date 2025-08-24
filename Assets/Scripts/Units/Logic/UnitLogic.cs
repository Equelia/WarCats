using System.Collections;
using Helpers;
using Units.Data;
using UnityEngine;
using UnityEngine.AI;
using Zenject;

namespace Units.Logic
{
    /// <summary>
    /// Base runtime behavior for units using NavMeshAgent and a simple state machine.
    /// Movement when advancing is now directed to enemy base (enemyBase Transform).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public abstract class UnitLogic : MonoBehaviour
    {
        public enum UnitState
        {
            Advance,    // move toward enemy base
            MoveToPos,  // moving to specified world position (used for cover)
            Attack,     // attacking current target
            Dead
        }

        [Header("Data")]
        [SerializeField] protected UnitData unitData;
        [SerializeField, Range(1, 3)] protected int level = 1;
        [SerializeField] protected int teamId = 0;

        [Header("Movement")]
        [Tooltip("NavMeshAgent stopping distance used for attacking.")]
        public float attackStoppingDistance = 1.5f;

        [Header("Cover")]
        public float coverSearchRadius = 6f;
        public float coverSeekDistance = 4f;

        protected UnitRuntimeStats stats;
        protected int currentHealth;
        protected NavMeshAgent agent;
        protected UnitState state = UnitState.Advance;
        protected Animator animator;

        protected Transform currentTarget; // enemy unit transform
        protected Transform enemyBase;
        protected Vector3 moveTargetPos;    // used when MoveToPos state
        protected float lastAttackTime = -999f;
        protected bool isInitialized = false;
        
        protected static readonly int AnimIsWalking = Animator.StringToHash("isWalking");
        protected static readonly int AnimShoot = Animator.StringToHash("shoot");
        protected static readonly int AnimDie = Animator.StringToHash("die");
        
        [Inject]
        protected ITeamBaseProvider baseProvider;
        
        /// <summary>
        /// Call after spawn to set runtime params.
        /// Prefer to pass enemyBaseTransform here (so unit always knows where to go).
        /// </summary>
        public virtual void Initialize(int team, int initLevel = 1, Transform explicitEnemyBase = null)
        {
            teamId = team;
            SetLevel(initLevel);

            // prefer explicit param (if spawner passed it), otherwise use provider
            if (explicitEnemyBase != null)
            {
                enemyBase = explicitEnemyBase;
            }
            else if (baseProvider != null)
            {
                enemyBase = baseProvider.GetOpposingBaseTransform(teamId);
            }

            isInitialized = true;
            state = UnitState.Advance;
        }
        
        protected virtual void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            animator = GetComponentInChildren<Animator>();
            
            if (animator == null)
                Debug.LogWarning($"{name}: Animator not found on children. Animations will be disabled.", this);
            
            if (unitData == null)
            {
                Debug.LogError($"{name}: UnitData not assigned.", this);
                enabled = false;
                return;
            }

            stats = unitData.GetStatsForLevel(level);
            currentHealth = stats.maxHealth;

            // configure agent defaults
            agent.speed = stats.moveSpeed;
            agent.stoppingDistance = attackStoppingDistance;
            agent.angularSpeed = 120f;
            agent.acceleration = 8f;
            agent.updateRotation = true;
            agent.updatePosition = true;
        }

        protected virtual void Update()
        {
            if (!isInitialized) return;
            if (state == UnitState.Dead) return;

            // keep agent speed in sync with stats (in case of level change)
            agent.speed = stats.moveSpeed;
            
            bool walking = (state == UnitState.Advance || state == UnitState.MoveToPos) && agent.velocity.sqrMagnitude > 0.01f;
            if (animator != null)
                animator.SetBool(AnimIsWalking, walking);

            TickStateMachine();
        }

        protected virtual void TickStateMachine()
        {
            // First: sense for nearest enemy in detection range
            Transform nearestEnemy = FindNearestEnemyInRange(stats.attackRange);

            switch (state)
            {
                case UnitState.Advance:
                    if (nearestEnemy != null)
                    {
                        float d = Vector3.Distance(transform.position, nearestEnemy.position);
                        if (d <= coverSeekDistance)
                        {
                            // try seek cover
                            var cover = Cover.FindNearestCoverTransform(transform.position, coverSearchRadius);
                            if (cover != null)
                            {
                                moveTargetPos = cover.position;
                                GoToPosition(moveTargetPos);
                                state = UnitState.MoveToPos;
                                return;
                            }
                        }

                        // else switch to attack
                        currentTarget = nearestEnemy;
                        state = UnitState.Attack;
                        return;
                    }

                    // continue advancing (towards enemy base when available)
                    AdvanceTowardsBase();
                    break;

                case UnitState.MoveToPos:
                    if (nearestEnemy != null)
                    {
                        currentTarget = nearestEnemy;
                    }

                    // check arrival
                    if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
                    {
                        if (currentTarget != null)
                        {
                            state = UnitState.Attack;
                        }
                        else
                        {
                            state = UnitState.Advance;
                        }
                    }
                    break;

                case UnitState.Attack:
                    if (currentTarget == null)
                    {
                        state = UnitState.Advance;
                        return;
                    }

                    float distToTarget = Vector3.Distance(transform.position, currentTarget.position);

                    if (distToTarget > stats.attackRange * 1.05f)
                    {
                        GoToPosition(currentTarget.position);
                        state = UnitState.MoveToPos;
                        return;
                    }

                    // face and attack
                    agent.ResetPath(); // stop moving
                    FaceTowards(currentTarget.position);
                    TryAttack(currentTarget);
                    break;
            }
        }

        #region Movement helpers

        protected void AdvanceTowardsBase()
        {
            if (enemyBase != null)
            {
                agent.SetDestination(enemyBase.position);
            }
            else
            {
                // fallback behaviour...
            }
        }

        protected void GoToPosition(Vector3 worldPos)
        {
            agent.SetDestination(worldPos);
        }

        protected void FaceTowards(Vector3 worldPos)
        {
            Vector3 dir = worldPos - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) return;
            Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 10f);
        }
        #endregion

        #region Combat
        protected virtual void TryAttack(Transform target)
        {
            if (Time.time - lastAttackTime < stats.attackCooldown) return;
            lastAttackTime = Time.time;
            
            if (animator != null)
                animator.SetTrigger(AnimShoot);
            
            StartCoroutine(PerformAttackCoroutine(target));
        }

        protected virtual IEnumerator PerformAttackCoroutine(Transform target)
        {
            yield return null;
            if (target == null) yield break;

            float roll = Random.value;
            if (roll <= stats.accuracy)
            {
                var unit = target.GetComponent<UnitLogic>();
                if (unit != null) unit.ReceiveDamage(stats.damage);
            }
        }
        #endregion

        public virtual void ReceiveDamage(int amount)
        {
            if (state == UnitState.Dead) return;
            currentHealth -= amount;
            if (currentHealth <= 0)
            {
                Die();
            }
        }

        protected virtual void Die()
        {
            state = UnitState.Dead;
            if (agent != null) agent.isStopped = true;
            
            if (animator != null)
                animator.SetTrigger(AnimDie);
            
            Destroy(gameObject, 1f);
        }

        protected Transform FindNearestEnemyInRange(float radius)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, radius);
            Transform best = null;
            float bestDist = float.MaxValue;

            foreach (var c in hits)
            {
                if (c.attachedRigidbody != null && c.attachedRigidbody.gameObject == gameObject) continue;
                var u = c.GetComponent<UnitLogic>();
                if (u == null) continue;
                if (u.teamId == this.teamId) continue;

                float d = Vector3.Distance(transform.position, u.transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = u.transform;
                }
            }

            return best;
        }

        public virtual void SetLevel(int newLevel)
        {
            level = Mathf.Clamp(newLevel, 1, 3);
            stats = unitData.GetStatsForLevel(level);
            currentHealth = stats.maxHealth;
            if (agent != null) agent.speed = stats.moveSpeed;
        }

        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            if (unitData != null) Gizmos.DrawWireSphere(transform.position, unitData.attackRange);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, coverSearchRadius);

            if (enemyBase != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, enemyBase.position);
                Gizmos.DrawWireSphere(enemyBase.position, 0.5f);
            }
        }
    }
}
