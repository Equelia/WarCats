using System;
using UnityEngine;

[DisallowMultipleComponent]
public class VisualBullet : MonoBehaviour
{
    [SerializeField] private float speed = 45f;
    [SerializeField] private Vector3 localRotationOffsetEuler = Vector3.zero;

    private Quaternion _localRotOffset;
    private Vector3 _dir;
    private bool _toPoint;
    private Vector3 _end;
    private Action _onArrive;

    private void Awake()
    {
        _localRotOffset = Quaternion.Euler(localRotationOffsetEuler);
    }

    public void SetSpeed(float s) => speed = Mathf.Max(0.01f, s);

    public void LaunchLinear(Vector3 origin, Vector3 dir)
    {
        transform.position = origin;
        _dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
        transform.rotation = Quaternion.LookRotation(_dir, Vector3.up) * _localRotOffset;
        _toPoint = false;
        _onArrive = null;
    }

    public void FlyTo(Vector3 worldEndPoint, Action onArrive)
    {
        _end = worldEndPoint;
        _onArrive = onArrive;
        _toPoint = true;

        Vector3 to = (_end - transform.position);
        if (to.sqrMagnitude > 0.0001f)
            _dir = to.normalized;

        transform.rotation = Quaternion.LookRotation(_dir, Vector3.up) * _localRotOffset;
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        if (_toPoint)
        {
            Vector3 to = _end - transform.position;
            float dist = to.magnitude;
            if (dist <= speed * dt)
            {
                transform.position = _end;
                var cb = _onArrive; _onArrive = null;
                cb?.Invoke();
                VfxPool.Release(gameObject);
                return;
            }
            _dir = to / dist;
        }

        transform.position += _dir * (speed * dt);
        transform.rotation = Quaternion.LookRotation(_dir, Vector3.up) * _localRotOffset;
    }

    public void ResetTrailIfAny()
    {
        var tr = GetComponent<TrailRenderer>();
        if (tr) { tr.Clear(); tr.emitting = true; }
    }
}
