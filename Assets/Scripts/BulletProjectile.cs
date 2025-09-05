// BulletProjectile.cs
using System;
using UnityEngine;

[DisallowMultipleComponent]
public class BulletProjectile : MonoBehaviour
{
    [Header("Kinetics")]
    public float speed = 45f;
    public float radius = 0.05f;
    public float maxLifetime = 2f;

    private Vector3 _pos;
    private Vector3 _dir;
    private float _remainingDist;
    private float _timer;

    private LayerMask _hitMask;
    private LayerMask _enemyMask;

    private Action<RaycastHit> _onHitEnemy;
    private Action<RaycastHit> _onHitEnv;

    private bool _active;

    public void Launch(
        Vector3 origin,
        Vector3 direction,
        float maxDistance,
        LayerMask hitMask,
        LayerMask enemyMask,
        Action<RaycastHit> onHitEnemy,
        Action<RaycastHit> onHitEnv)
    {
        _pos = origin;
        _dir = direction.normalized;
        _remainingDist = maxDistance;
        _hitMask = hitMask;
        _enemyMask = enemyMask;
        _onHitEnemy = onHitEnemy;
        _onHitEnv = onHitEnv;
        _timer = 0f;
        _active = true;

        transform.position = origin;
        if (_dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(_dir, Vector3.up);

        // (Опционально) TrailRenderer: включить/сбросить здесь
        ResetTrailIfAny();
    }

    private void Update()
    {
        if (!_active) return;

        float dt = Time.deltaTime;
        float step = speed * dt;
        step = Mathf.Min(step, _remainingDist);

        // sphere cast вперед
        if (Physics.SphereCast(_pos, radius, _dir, out var hit, step, _hitMask, QueryTriggerInteraction.Ignore))
        {
            bool isEnemy = (_enemyMask.value & (1 << hit.collider.gameObject.layer)) != 0;

            // сдвигаем в точку контакта
            transform.position = hit.point;

            if (isEnemy) _onHitEnemy?.Invoke(hit);
            else _onHitEnv?.Invoke(hit);

            Despawn();
            return;
        }

        // нет хит — летим дальше
        _pos += _dir * step;
        transform.position = _pos;
        _remainingDist -= step;

        // таймауты / дистанция
        _timer += dt;
        if (_remainingDist <= 0f || _timer >= maxLifetime)
        {
            Despawn();
        }
    }

    private void OnDisable() => _active = false;

    private void Despawn()
    {
        _active = false;
        VfxPool.Release(gameObject);
    }

    private void ResetTrailIfAny()
    {
        var tr = GetComponent<TrailRenderer>();
        if (tr)
        {
            tr.Clear();
            tr.emitting = true;
        }
        // Если есть ParticleSystem на пуле пули — отключи emission в префабе и активируй только нужные.
    }
}
