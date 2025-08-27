using System.Threading;
using Cysharp.Threading.Tasks;
using Helpers;
using Units.Logic.Core;
using Units.Logic.Fsm;
using Units.Logic.Services;
using UnityEngine;

namespace Units.Logic.States
{
    public sealed class MoveToPosState : IState
    {
        private readonly UnitContext _ctx;
        private readonly IMovementService _move;
        private readonly ISensorService _sensor;
        private readonly ICoverService _cover;
        private readonly StateMachine _fsm;
        private readonly ICombatService _combat; // NEW

        private const float ImmediateInterruptDistance = 1.2f;

        public MoveToPosState(
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
            _move.GoTo(_ctx, _ctx.MoveTargetPos);
            return UniTask.CompletedTask;
        }

        public void Tick()
        {
            _ctx.Agent.speed = _ctx.Stats.moveSpeed;
            bool walking = _ctx.Agent.velocity.sqrMagnitude > 0.01f;
            _move.SyncWalkingAnim(_ctx, walking);

            var enemy = _sensor.FindNearestEnemy(_ctx, _ctx.Stats.attackRange);
            if (enemy != null)
            {
                var enemyLogic = enemy.GetComponentInParent<UnitController>();
                float distToEnemy = Vector3.Distance(_ctx.Transform.position, enemy.position);

                bool shouldInterrupt = _ctx.DesiredCover == null
                                       || (enemyLogic != null && enemyLogic.Context.LastAttackTime > _ctx.CoverSearchStartTime)
                                       || distToEnemy <= ImmediateInterruptDistance;

                if (shouldInterrupt)
                {
                    if (_ctx.DebugCover) Debug.Log($"{_ctx.Transform.name}: Interrupt cover move → Attack", _ctx.Transform);
                    _ctx.DesiredCover = null;
                    _move.RestoreStoppingDistance(_ctx);
                    _ctx.CurrentTarget = enemy;
                    _ = _fsm.SetStateAsync(new AttackState(_ctx, _move, _sensor, _cover, _fsm, _combat)); // pass combat
                    return;
                }
            }

            if (_move.Arrived(_ctx, _ctx.MoveTargetPos))
            {
                _move.RestoreStoppingDistance(_ctx);

                if (_ctx.DesiredCover != null)
                {
                    var cov = _ctx.DesiredCover.GetComponent<Cover>() ?? _ctx.DesiredCover.GetComponentInParent<Cover>();
                    if (cov != null)
                    {
                        if (cov.IsOccupied && cov.occupant != _ctx.Transform.GetComponent<UnitController>())
                        {
                            if (_ctx.DebugCover) Debug.Log($"{_ctx.Transform.name}: Cover occupied on arrival → fallback", _ctx.Transform);
                            _ctx.DesiredCover = null;
                            _ctx.CurrentCover = null;
                            _ = _fsm.SetStateAsync(_ctx.CurrentTarget != null
                                ? new AttackState(_ctx, _move, _sensor, _cover, _fsm, _combat)
                                : new AdvanceState(_ctx, _move, _sensor, _cover, _fsm, _combat));
                        }
                        else
                        {
                            bool occupied = _cover.Occupy(_ctx, cov);
                            if (occupied)
                            {
                                if (_ctx.DebugCover) Debug.Log($"{_ctx.Transform.name}: Occupied cover", _ctx.Transform);
                                _ = _fsm.SetStateAsync(_ctx.CurrentTarget != null
                                    ? new AttackState(_ctx, _move, _sensor, _cover, _fsm, _combat)
                                    : new AdvanceState(_ctx, _move, _sensor, _cover, _fsm, _combat));
                            }
                            else
                            {
                                if (_ctx.DebugCover) Debug.Log($"{_ctx.Transform.name}: Failed to occupy cover → fallback", _ctx.Transform);
                                _ctx.DesiredCover = null;
                                _ctx.CurrentCover = null;
                                _ = _fsm.SetStateAsync(_ctx.CurrentTarget != null
                                    ? new AttackState(_ctx, _move, _sensor, _cover, _fsm, _combat)
                                    : new AdvanceState(_ctx, _move, _sensor, _cover, _fsm, _combat));
                            }
                        }
                    }
                    else
                    {
                        _ctx.DesiredCover = null;
                        _ = _fsm.SetStateAsync(_ctx.CurrentTarget != null
                            ? new AttackState(_ctx, _move, _sensor, _cover, _fsm, _combat)
                            : new AdvanceState(_ctx, _move, _sensor, _cover, _fsm, _combat));
                    }
                }
                else
                {
                    _ = _fsm.SetStateAsync(_ctx.CurrentTarget != null
                        ? new AttackState(_ctx, _move, _sensor, _cover, _fsm, _combat)
                        : new AdvanceState(_ctx, _move, _sensor, _cover, _fsm, _combat));
                }

                _ctx.DesiredCover = null;
            }
        }

        public void Exit() { }
    }
}
