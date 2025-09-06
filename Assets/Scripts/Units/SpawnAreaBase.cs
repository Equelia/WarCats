// SpawnAreaBase.cs
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public abstract class SpawnAreaBase : MonoBehaviour
{
    [Header("Ownership")]
    [Tooltip("Team that is allowed to spawn here (optional, can be -1 to allow any).")]
    public int teamId = -1;

    [Header("Projection")]
    [Tooltip("Project sampled point to ground via raycast.")]
    public bool projectToGround = true;
    public LayerMask groundMask = ~0;
    public float raycastHeight = 20f;
    public float yOffset = 0.0f;
    public bool alignUpToGroundNormal = false;

    [Header("NavMesh")]
    public bool useNavMesh = true;
    public float navMeshMaxDistance = 2.0f;
    public int navMeshAreaMask = NavMesh.AllAreas;

    [Header("Gizmos")]
    public bool drawGizmos = true;
    public bool drawWire = true;
    public Color gizmoColor = new Color(0f, 1f, 0f, 0.20f);
    public Color wireColor = new Color(0f, 0.8f, 0f, 1f);

    /// <summary>Try get a valid spawn pose inside this area.</summary>
    public bool TryGetRandomPose(out Vector3 pos, out Quaternion rot)
    {
        pos = default; rot = default;

        // 1) Sample a candidate in world space (shape-specific).
        Vector3 p = SampleWorldPoint();

        // 2) Project to ground (optional).
        Vector3 up = Vector3.up;
        if (projectToGround)
        {
            Ray ray = new Ray(p + Vector3.up * raycastHeight, Vector3.down);
            if (Physics.Raycast(ray, out var hit, raycastHeight * 2f, groundMask, QueryTriggerInteraction.Ignore))
            {
                p = hit.point + Vector3.up * yOffset;
                up = alignUpToGroundNormal ? hit.normal : Vector3.up;
            }
            else
            {
                return false;
            }
        }

        // 3) Snap to NavMesh (optional).
        if (useNavMesh)
        {
            if (NavMesh.SamplePosition(p, out var hit, navMeshMaxDistance, navMeshAreaMask))
                p = hit.position;
            else
                return false;
        }

        pos = p;
        rot = Quaternion.LookRotation(transform.forward, up);
        return true;
    }

    /// <summary>Shape-specific sampling in world space.</summary>
    protected abstract Vector3 SampleWorldPoint();

    // Let shapes draw themselves
    protected abstract void DrawGizmosShape();

    protected virtual void OnDrawGizmos()
    {
        if (!drawGizmos) return;
        var old = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);

        var prev = Gizmos.color;
        Gizmos.color = gizmoColor;
        DrawGizmosShape();

        if (drawWire)
        {
            Gizmos.color = wireColor;
            DrawGizmosWire();
        }

        // forward arrow
        Gizmos.color = wireColor;
        Gizmos.DrawLine(Vector3.zero, Vector3.forward * 1.0f);
        Gizmos.DrawSphere(Vector3.forward * 1.0f, 0.05f);

        Gizmos.color = prev;
        Gizmos.matrix = old;
    }

    protected virtual void DrawGizmosWire() { /* optional per-shape */ }
}
