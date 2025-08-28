using System.Threading;
using Cysharp.Threading.Tasks;
using Units.Logic.Core;
using Units.Logic.Fsm;
using Units.Logic.Services;
using UnityEngine;

namespace Units.Logic.States
{
    /// <summary>
    /// Face target and attack while it is in range. Chase if it moves away.
    /// </summary>
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
            // ensure stopping distance equals attack range while shooting
            if (_ctx.PrevStoppingDistance < 0f)
                _ctx.Agent.stoppingDistance = _ctx.Stats.attackRange;

            return UniTask.CompletedTask;
        }

        public void Tick()
        {
            if (_ctx.CurrentTarget == null)
            {
                _cover.Release(_ctx);
                _ = _fsm.SetStateAsync(new AdvanceState(_ctx, _move, _sensor, _cover, _fsm, _combat));
                return;
            }

            float dist = Vector3.Distance(_ctx.Transform.position, _ctx.CurrentTarget.position);
            if (dist > _ctx.Stats.attackRange * 1.05f)
            {
                // release cover and chase
                _cover.Release(_ctx);
                _move.GoTo(_ctx, _ctx.CurrentTarget.position);
                _ = _fsm.SetStateAsync(new MoveToPosState(_ctx, _move, _sensor, _cover, _fsm, _combat));
                return;
            }

            _move.ResetPath(_ctx);
            _ctx.FaceTowards(_ctx.CurrentTarget.position);
            _combat.TryAttack(_ctx);

            // walking anim off while stationary shooting
            _move.SyncWalkingAnim(_ctx, false);
        }

        public void Exit() { }
    }
}
