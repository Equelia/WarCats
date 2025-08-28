using System.Threading;
using Cysharp.Threading.Tasks;
using Units.Logic.Core;
using Units.Logic.Fsm;
using Units.Logic.Services;
using UnityEngine;

namespace Units.Logic.States
{
    public sealed class AdvanceState : IState
    {
        private readonly UnitContext _ctx;
        private readonly IMovementService _move;
        private readonly ISensorService _sensor;
        private readonly ICoverService _cover;
        private readonly ICombatService _combat; 
        private readonly StateMachine _fsm;

        public AdvanceState(
            UnitContext ctx,
            IMovementService move,
            ISensorService sensor,
            ICoverService cover,
            StateMachine fsm,
            ICombatService combat) // NEW
        {
            _ctx = ctx; _move = move; _sensor = sensor; _cover = cover; _fsm = fsm; _combat = combat; // NEW
        }

        public UniTask EnterAsync(CancellationToken ct)
        {
            if (_ctx.PrevStoppingDistance < 0f)
                _ctx.Agent.stoppingDistance = _ctx.Stats.attackRange;

            _move.AdvanceTowardsBase(_ctx);
            return UniTask.CompletedTask;
        }

        public void Tick()
        {
            _ctx.Agent.speed = _ctx.Stats.moveSpeed;
            bool walking = _ctx.Agent.velocity.sqrMagnitude > 0.01f;
            _move.SyncWalkingAnim(_ctx, walking);

            var enemy = _sensor.FindNearestEnemy(_ctx, _ctx.Stats.attackRange);
            if (enemy == null) return;

            float d = Vector3.Distance(_ctx.Transform.position, enemy.position);

            if (d <= _ctx.CoverSeekDistance)
            {
                var cand = _cover.FindBest(_ctx, _ctx.Transform.position, enemy.position, _ctx.CoverSearchRadius);
                if (cand.HasValue)
                {
                    _ctx.MoveTargetPos = cand.Value.position;
                    _ctx.DesiredCover = cand.Value.cover;
                    if (_ctx.DebugCover) Debug.Log($"{_ctx.Transform.name}: Found cover at {_ctx.MoveTargetPos}", _ctx.Transform);

                    _ctx.CoverSearchStartTime = Time.time;
                    _move.OverrideStoppingDistance(_ctx, 0.2f);
                    _ctx.CurrentTarget = enemy;
                    _move.GoTo(_ctx, _ctx.MoveTargetPos);

                    _ = _fsm.SetStateAsync(new MoveToPosState(_ctx, _move, _sensor, _cover, _fsm, _combat)); // pass combat
                    return;
                }
                else
                {
                    if (_ctx.DebugCover) Debug.Log($"{_ctx.Transform.name}: No cover found → Attack", _ctx.Transform);
                    _ctx.CurrentTarget = enemy;
                    _ = _fsm.SetStateAsync(new AttackState(_ctx, _move, _sensor, _cover, _fsm, _combat)); // pass combat
                    return;
                }
            }

            if (d > _ctx.CoverSeekDistance)
            {
                _ctx.CurrentTarget = enemy;
                _ = _fsm.SetStateAsync(new AttackState(_ctx, _move, _sensor, _cover, _fsm, _combat)); // pass combat
            }
        }

        public void Exit() { }
    }
}
