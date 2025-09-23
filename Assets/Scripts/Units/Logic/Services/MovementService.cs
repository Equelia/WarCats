using Units.Logic.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Units.Logic.Services
{
    public sealed class MovementService : IMovementService
    {
        public void AdvanceTowardsBase(UnitContext ctx)
        {
            if (ctx.EnemyBase != null)
            {
                float desired = Mathf.Max(0.25f, ctx.Stats.attackRange * 0.9f);
                OverrideStoppingDistance(ctx, desired);
                GoTo(ctx, ctx.EnemyBase.position);
            }
        }

        public void GoTo(UnitContext ctx, Vector3 pos)
        {
            Vector3 sep = ComputeSeparationOffset(ctx, pos);
            Vector3 target = pos + sep;
            if (NavMesh.SamplePosition(target, out var hit, 1.5f, NavMesh.AllAreas))
                ctx.Agent.SetDestination(hit.position);
            else
                ctx.Agent.SetDestination(pos);
        }

        public bool Arrived(UnitContext ctx, Vector3 pos)
        {
            bool arrivedByNav = !ctx.Agent.pathPending && ctx.Agent.hasPath &&
                                ctx.Agent.remainingDistance <= Mathf.Max(0.25f, ctx.Agent.stoppingDistance + 0.05f);
            bool arrivedByDistance = Vector3.Distance(ctx.Transform.position, pos) <= 0.35f;
            bool arrivedNoPathClose = (!ctx.Agent.hasPath && !ctx.Agent.pathPending &&
                                       Vector3.Distance(ctx.Transform.position, pos) <= 1.0f);
            return arrivedByNav || arrivedByDistance || arrivedNoPathClose;
        }

        public void ResetPath(UnitContext ctx) => ctx.Agent.ResetPath();

        public void SyncWalkingAnim(UnitContext ctx, bool isWalking)
        {
            if (ctx.Animator == null) return;
            if (ctx.LockWalkAnim) isWalking = false;
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

        private Vector3 ComputeSeparationOffset(UnitContext ctx, Vector3 desiredPos)
        {
            float sepRadius = Mathf.Max(1f, ctx.Agent.radius * 3f);
            Collider[] hits = Physics.OverlapSphere(ctx.Transform.position, sepRadius);
            Vector3 sep = Vector3.zero;
            int count = 0;
            foreach (var c in hits)
            {
                var other = c.GetComponentInParent<Units.Logic.UnitController>();
                if (other == null) continue;
                if (other.gameObject == ctx.Transform.gameObject) continue;
                Vector3 toOther = other.transform.position - ctx.Transform.position;
                toOther.y = 0f;
                float d = toOther.magnitude;
                if (d < 0.001f) continue;
                float inv = 1f / (d * d);
                sep -= toOther.normalized * inv;
                count++;
            }

            if (count == 0) return Vector3.zero;

            sep /= count;
            sep.y = 0f;
            if (sep.sqrMagnitude < 1e-6f) return Vector3.zero;
            float maxOffset = Mathf.Clamp(ctx.Agent.radius * 1.5f, 0.3f, 2.0f);
            Vector3 offset = sep.normalized * Mathf.Min(maxOffset, sep.magnitude);
            Vector3 toward = (desiredPos - ctx.Transform.position);
            toward.y = 0f;
            if (Vector3.Dot(toward.normalized, offset.normalized) < -0.85f)
            {
                offset = offset * 0.6f;
            }
            return offset;
        }
    }
}
