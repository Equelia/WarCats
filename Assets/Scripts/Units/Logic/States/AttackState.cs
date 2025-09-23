using System.Threading;
using Cysharp.Threading.Tasks;
using Units.Logic.Core;
using Units.Logic.Fsm;
using Units.Logic.Services;
using UnityEngine;

namespace Units.Logic.States
{
    public sealed class AttackState : IState
    {
        private readonly UnitContext _ctx;
        private readonly IMovementService _move;
        private readonly ISensorService _sensor;
        private readonly ICoverService _cover;
        private readonly StateMachine _fsm;
        private readonly ICombatService _combat;

        public AttackState(UnitContext ctx, IMovementService move, ISensorService sensor, ICoverService cover, StateMachine fsm, ICombatService combat = null)
        {
            _ctx = ctx; _move = move; _sensor = sensor; _cover = cover; _fsm = fsm;
            _combat = combat ?? new CombatService();
        }

        public UniTask EnterAsync(CancellationToken ct)
        {
            if (_ctx.PrevStoppingDistance < 0f)
                _ctx.Agent.stoppingDistance = _ctx.Stats.attackRange;
            return UniTask.CompletedTask;
        }

        public void Tick()
        {
            var enemy = _sensor.FindNearestEnemy(_ctx, _ctx.Stats.attackRange);
            if (enemy != null && enemy != _ctx.EnemyBase)
                _ctx.CurrentTarget = enemy;

            if (_ctx.CurrentTarget == null)
            {
                ReleaseRangeBoostIfNeeded();
                _cover.Release(_ctx);
                _ = _fsm.SetStateAsync(new AdvanceState(_ctx, _move, _sensor, _cover, _fsm, _combat));
                return;
            }

            if (_ctx.CurrentTarget != _ctx.EnemyBase)
            {
                ReleaseRangeBoostIfNeeded();
            }

            float dist = Vector3.Distance(_ctx.Transform.position, _ctx.CurrentTarget.position);
            if (dist > _ctx.Stats.attackRange * 1.05f)
            {
                _cover.Release(_ctx);
                _move.GoTo(_ctx, _ctx.CurrentTarget.position);
                _ = _fsm.SetStateAsync(new MoveToPosState(_ctx, _move, _sensor, _cover, _fsm, _combat));
                return;
            }

            _move.ResetPath(_ctx);
            _ctx.FaceTowards(_ctx.CurrentTarget.position);
            _combat.TryAttack(_ctx);
            _move.SyncWalkingAnim(_ctx, false);
        }

        public void Exit()
        {
            ReleaseRangeBoostIfNeeded();
        }

        private void ReleaseRangeBoostIfNeeded()
        {
            if (_ctx != null && _ctx.HasBaseRangeBoost)
            {
                _ctx.HasBaseRangeBoost = false;
                if (_ctx.RangeBoostOriginal >= 0f)
                {
                    _ctx.Stats.attackRange = _ctx.RangeBoostOriginal;
                    _ctx.RangeBoostOriginal = -1f;
                }
                if (_ctx.Agent != null)
                    _ctx.Agent.stoppingDistance = _ctx.Stats.attackRange;
            }
        }
    }
}
