using Units.Logic.Core;
using UnityEngine;

namespace Units.Logic.Services
{
    public sealed class MovementService : IMovementService
    {
        public void AdvanceTowardsBase(UnitContext ctx)
        {
            if (ctx.EnemyBase != null)
            {
                // leave cover when advancing
                // (CoverService.Release is called by states when needed)
                ctx.Agent.SetDestination(ctx.EnemyBase.position);
            }
        }

        public void GoTo(UnitContext ctx, Vector3 pos)
        {
            ctx.Agent.SetDestination(pos);
        }

        public bool Arrived(UnitContext ctx, Vector3 pos)
        {
            // robust arrival check similar to original logic
            bool arrivedByNav = !ctx.Agent.pathPending && ctx.Agent.hasPath &&
                                ctx.Agent.remainingDistance <= Mathf.Max(0.25f, ctx.Agent.stoppingDistance + 0.05f);
            bool arrivedByDistance = Vector3.Distance(ctx.Transform.position, pos) <= 0.35f;
            bool arrivedNoPathClose = (!ctx.Agent.hasPath && !ctx.Agent.pathPending &&
                                       Vector3.Distance(ctx.Transform.position, pos) <= 1.0f);
            return arrivedByNav || arrivedByDistance || arrivedNoPathClose;
        }

        public void ResetPath(UnitContext ctx)
        {
            ctx.Agent.ResetPath();
        }

        public void SyncWalkingAnim(UnitContext ctx, bool isWalking)
        {
            if (ctx.Animator != null)
                ctx.Animator.SetBool(UnitContext.AnimIsWalking, isWalking);
        }

        public void OverrideStoppingDistance(UnitContext ctx, float tempValue)
        {
            if (ctx.PrevStoppingDistance < 0f)
                ctx.PrevStoppingDistance = ctx.Agent.stoppingDistance;
            ctx.Agent.stoppingDistance = tempValue;
        }

        public void RestoreStoppingDistance(UnitContext ctx)
        {
            if (ctx.PrevStoppingDistance >= 0f)
            {
                ctx.Agent.stoppingDistance = ctx.PrevStoppingDistance;
                ctx.PrevStoppingDistance = -1f;
            }
        }
    }
}
