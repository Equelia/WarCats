using System.Threading;
using Cysharp.Threading.Tasks;
using Units.Logic.Core;
using Units.Logic.Fsm;
using Units.Logic.Services;
using UnityEngine;
using UnityEngine.AI;
using Zenject;

namespace Units.Logic
{
    /// <summary>
    /// Thin orchestrator that wires up context, services and FSM states.
    /// Replace old monolithic UnitLogic with this class.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class UnitController : MonoBehaviour
    {
        public enum UnitState { Advance, MoveToPos, Attack, Dead }

        [Header("Data")]
        [SerializeField] protected Units.Data.UnitData unitData;
        [SerializeField, Range(1,3)] protected int level = 1;
        [SerializeField] protected int teamId = 0;

        [Header("Cover")]
        [Tooltip("How far the unit will search for covers (world units).")]
        public float coverSearchRadius = 6f;
        [Tooltip("Distance to enemy at which the unit prefers to start seeking cover.")]
        public float coverSeekDistance = 4f;
        [Tooltip("Exclude covers that are further than this angle behind the unit's forward.")]
        public float coverExcludeAngleDeg = 100f;

        [Header("Debug")]
        public bool debugCover = false;

        [Inject] protected ITeamBaseProvider baseProvider;

        // Public accessors
        public int TeamId => teamId;
        public UnitContext Context => _ctx;

        // Core
        protected UnitContext _ctx;
        protected StateMachine _fsm;

        // Services (can be swapped or injected if desired)
        protected IMovementService _move;
        protected ISensorService _sensor;
        protected ICoverService _cover;
        protected ICombatService _combat;

        protected virtual void Awake()
        {
            var agent = GetComponent<NavMeshAgent>();
            var animator = GetComponentInChildren<Animator>();

            if (unitData == null)
            {
                Debug.LogError($"{name}: UnitData not assigned.", this);
                enabled = false; return;
            }

            _ctx = new UnitContext
            {
                UnitData = unitData,
                Level = Mathf.Clamp(level, 1, 3),
                TeamId = teamId,
                DebugCover = debugCover,
                CoverSearchRadius = coverSearchRadius,
                CoverSeekDistance = coverSeekDistance,
                CoverExcludeAngleDeg = coverExcludeAngleDeg,

                Transform = transform,
                Animator = animator,
                Agent = agent,
                EnemyBase = baseProvider != null ? baseProvider.GetOpposingBaseTransform(teamId) : null,

                Cts = new CancellationTokenSource(),
                IsInitialized = true
            };

            _ctx.Stats = unitData.GetStatsForLevel(_ctx.Level);
            _ctx.CurrentHealth = _ctx.Stats.maxHealth;

            // Agent defaults (kept consistent with original)
            agent.speed = _ctx.Stats.moveSpeed;
            agent.stoppingDistance = _ctx.Stats.attackRange;
            agent.angularSpeed = 120f;
            agent.acceleration = 8f;
            agent.updateRotation = true;
            agent.updatePosition = true;

            // Services
            _move = new MovementService();
            _sensor = new SensorService();
            _cover  = new CoverService();
            _combat = CreateCombatService(); // overridable hook for subclasses

            // FSM
            _fsm = new StateMachine(_ctx.Cts.Token);
            _ = _fsm.SetStateAsync(new Units.Logic.States.AdvanceState(_ctx, _move, _sensor, _cover, _fsm, _combat));
        }
        
        public void Initialize(int team, int initLevel = 1, Transform explicitEnemyBase = null)
        {
            // Update serialized field for debugging/inspector clarity
            this.level  = Mathf.Clamp(initLevel, 1, 3);
            this.teamId = team;

            // If Awake already created the context, apply immediately
            if (_ctx != null)
            {
                _ctx.TeamId = team;
                SetLevel(initLevel); // will refresh stats, health, agent speed/range

                if (explicitEnemyBase != null)
                    _ctx.EnemyBase = explicitEnemyBase;
                else if (baseProvider != null)
                    _ctx.EnemyBase = baseProvider.GetOpposingBaseTransform(teamId);
            }
            // If called before Awake (rare with these patterns), Awake will still resolve EnemyBase via baseProvider.
        }

        protected virtual ICombatService CreateCombatService() => new CombatService();

        protected virtual void OnDestroy()
        {
            // Release occupied cover if any
            _cover?.Release(_ctx);

            if (_ctx?.Cts != null)
            {
                _ctx.Cts.Cancel();
                _ctx.Cts.Dispose();
                _ctx.Cts = null;
            }
        }

        protected virtual void Update()
        {
            if (!_ctx.IsInitialized) return;
            if (_ctx.CurrentHealth <= 0) return;

            // keep agent speed/stopping synced unless temp override is active
            _ctx.Agent.speed = _ctx.Stats.moveSpeed;
            if (_ctx.PrevStoppingDistance < 0f)
                _ctx.Agent.stoppingDistance = _ctx.Stats.attackRange;

            _fsm.Tick();
        }

        public virtual void ReceiveDamage(int amount)
        {
            if (_ctx.CurrentHealth <= 0) return;
            _ctx.CurrentHealth -= amount;
            if (_ctx.CurrentHealth <= 0) Die();
        }

        protected virtual void Die()
        {
            _cover.Release(_ctx);

            if (_ctx.Animator != null)
                _ctx.Animator.SetTrigger(UnitContext.AnimDie);

            if (_ctx.Agent != null) _ctx.Agent.isStopped = true;

            if (_ctx.Cts != null)
            {
                _ctx.Cts.Cancel();
                _ctx.Cts.Dispose();
                _ctx.Cts = null;
            }

            _ = _fsm.SetStateAsync(new Units.Logic.States.DeadState(_ctx));
            Destroy(gameObject, 1f);
        }

        /// <summary>
        /// Allows external systems to change level at runtime.
        /// </summary>
        public virtual void SetLevel(int newLevel)
        {
            _ctx.Level = Mathf.Clamp(newLevel, 1, 3);
            _ctx.Stats = _ctx.UnitData.GetStatsForLevel(_ctx.Level);
            _ctx.CurrentHealth = _ctx.Stats.maxHealth;
            if (_ctx.Agent != null)
            {
                _ctx.Agent.speed = _ctx.Stats.moveSpeed;
                _ctx.Agent.stoppingDistance = _ctx.Stats.attackRange;
            }
        }
    }
}
