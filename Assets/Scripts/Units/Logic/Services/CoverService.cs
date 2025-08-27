using System.Collections.Generic;
using Helpers;
using Units.Logic.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Units.Logic.Services
{
    public sealed class CoverService : ICoverService
    {
        public CoverCandidate? FindBest(UnitContext ctx, Vector3 fromPos, Vector3 enemyPos, float radius)
        {
            // collect candidates around
            Collider[] hits = Physics.OverlapSphere(fromPos, radius);
            var candidates = new List<Cover>();

            // forward used to exclude "far behind" covers by angle
            Vector3 forward = (ctx.Agent != null ? ctx.Agent.transform.forward : ctx.Transform.forward);
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = (enemyPos - fromPos);
                forward.y = 0f;
                if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            }
            forward.Normalize();

            float minDot = Mathf.Cos(ctx.CoverExcludeAngleDeg * Mathf.Deg2Rad);

            bool IsBehind(Vector3 coverPos)
            {
                Vector3 toCover = coverPos - fromPos;
                toCover.y = 0f;
                if (toCover.sqrMagnitude < 0.0001f) return false;
                toCover.Normalize();
                float dot = Vector3.Dot(forward, toCover);
                bool behind = dot < minDot;
                if (ctx.DebugCover && behind)
                    Debug.Log($"{ctx.Transform.name}: Excluding cover at {coverPos} (dot={dot:F2} < {minDot:F2})", ctx.Transform);
                return behind;
            }

            if (hits != null && hits.Length > 0)
            {
                var seen = new HashSet<Cover>();
                foreach (var c in hits)
                {
                    var cov = c.GetComponentInParent<Cover>();
                    if (cov == null) continue;
                    if (seen.Contains(cov)) continue;
                    if (IsBehind(cov.transform.position)) continue;
                    if (cov.IsOccupied && cov.occupant != ctx.Transform.GetComponent<Units.Logic.UnitController>())
                    {
                        if (ctx.DebugCover) Debug.Log($"{ctx.Transform.name}: Skipping occupied cover at {cov.transform.position}", ctx.Transform);
                        continue;
                    }
                    seen.Add(cov);
                    candidates.Add(cov);
                }
            }

            if (candidates.Count == 0)
            {
                var all = Object.FindObjectsOfType<Cover>();
                foreach (var cov in all)
                {
                    float d = Vector3.Distance(fromPos, cov.transform.position);
                    if (d > radius) continue;
                    if (IsBehind(cov.transform.position)) continue;
                    if (cov.IsOccupied && cov.occupant != ctx.Transform.GetComponent<Units.Logic.UnitController>())
                    {
                        if (ctx.DebugCover) Debug.Log($"{ctx.Transform.name}: Skipping occupied cover at {cov.transform.position}", ctx.Transform);
                        continue;
                    }
                    candidates.Add(cov);
                }
            }

            if (candidates.Count == 0) return null;

            Cover bestCover = null;
            Vector3 bestPos = Vector3.zero;
            float bestPathLen = float.MaxValue;

            foreach (var cov in candidates)
            {
                Vector3 dir = (cov.transform.position - enemyPos);
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.001f)
                {
                    dir = (cov.transform.position - fromPos);
                    dir.y = 0f;
                    if (dir.sqrMagnitude < 0.001f) dir = Vector3.forward;
                }
                dir.Normalize();

                float behindDist = 0.6f + (ctx.Agent != null ? ctx.Agent.radius : 0.5f);
                Vector3 candidate = cov.transform.position + dir * behindDist;

                if (!TrySample(candidate, out var nav, ctx.Agent))
                {
                    const int attempts = 8;
                    bool found = false;
                    for (int i = 0; i < attempts; i++)
                    {
                        float angle = i * Mathf.PI * 2f / attempts;
                        Vector3 offset = Quaternion.Euler(0f, angle * Mathf.Rad2Deg, 0f) * (dir * behindDist);
                        if (TrySample(cov.transform.position + offset, out nav, ctx.Agent))
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found) continue;
                }

                if (!TryPathLength(ctx, nav.position, fromPos, out float pathLen)) continue;

                if (pathLen < bestPathLen)
                {
                    bestPathLen = pathLen;
                    bestCover = cov;
                    bestPos = nav.position;
                }
            }

            if (bestCover == null) return null;
            if (ctx.DebugCover) Debug.Log($"{ctx.Transform.name}: Selected cover at {bestPos} (pathLen={bestPathLen:F2})", ctx.Transform);

            return new CoverCandidate { position = bestPos, cover = bestCover };
        }

        public bool Occupy(UnitContext ctx, Cover cov)
        {
            if (cov == null) return false;

            // already ours
            if (cov.occupant == ctx.Transform.GetComponent<UnitController>())
            {
                ctx.CurrentCover = cov;
                return true;
            }

            if (cov.IsOccupied) return false;

            cov.occupant = ctx.Transform.GetComponent<UnitController>();
            ctx.CurrentCover = cov;
            return true;
        }

        public void Release(UnitContext ctx)
        {
            if (ctx.CurrentCover != null)
            {
                if (ctx.CurrentCover.occupant == ctx.Transform.GetComponent<UnitController>())
                    ctx.CurrentCover.occupant = null;
                ctx.CurrentCover = null;
            }
        }

        private bool TrySample(Vector3 pos, out NavMeshHit hit, NavMeshAgent agent)
        {
            float sampleRange = 1.5f + (agent != null ? agent.radius : 0.5f);
            return NavMesh.SamplePosition(pos, out hit, sampleRange, NavMesh.AllAreas);
        }

        private bool TryPathLength(UnitContext ctx, Vector3 to, Vector3 from, out float length)
        {
            var path = new NavMeshPath();
            bool ok = (ctx.Agent != null)
                ? ctx.Agent.CalculatePath(to, path)
                : NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path);

            length = 0f;
            if (!ok || path.status != NavMeshPathStatus.PathComplete) return false;

            Vector3 prev = path.corners.Length > 0 ? path.corners[0] : from;
            for (int i = 1; i < path.corners.Length; i++)
            {
                length += Vector3.Distance(prev, path.corners[i]);
                prev = path.corners[i];
            }
            return true;
        }
    }
}
