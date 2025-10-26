using Cysharp.Threading.Tasks;
using Units.Logic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
[DisallowMultipleComponent]
public class Mine : MonoBehaviour
{
    [SerializeField] private MineData data;           
    [SerializeField] private GameObject explosionEffect;
    [SerializeField] private AudioClip explosionSound;

    private bool _armed = true;
    private AudioSource _audio;

    // кеш уровневых значений
    private int _damage;
    private float _radius;

    // ВЛАДЕЛЕЦ МИНЫ (команда)
    private int _ownerTeamId = 0;

    public void Init(MineData d, int level, int ownerTeamId)
    {
        data = d;
        _armed = true;

        _ownerTeamId = ownerTeamId;

        _damage = data.GetExplosionDamageForLevel(level);
        _radius = data.GetExplosionRadiusForLevel(level);

        explosionEffect = data.explosionEffect;
        explosionSound  = data.explosionSound;
    }

    // Старую сигнатуру оставим на всякий случай (по умолчанию owner = 0)
    public void Init(MineData d, int level) => Init(d, level, 0);

    private void Awake()
    {
        _audio = GetComponent<AudioSource>();
        _audio.playOnAwake = false;
        _audio.spatialBlend = 1f;
    }

    private void OnCollisionEnter(Collision other)
    {
        if (!_armed || data == null) return;

        if (other.gameObject.TryGetComponent(out UnitController u))
        {
            // ВЗРЫВАЕМСЯ ТОЛЬКО НА ПРОТИВНИКАХ
            if (u.TeamId != _ownerTeamId)
                Explode().Forget();
        }
    }

    private async UniTaskVoid Explode()
    {
        _armed = false;

        // Урон по области — только по ПРОТИВНИКАМ
        var hits = Physics.OverlapSphere(transform.position, _radius, ~0, QueryTriggerInteraction.Collide);
        foreach (var h in hits)
            if (h.TryGetComponent(out UnitController c) && c.TeamId != _ownerTeamId)
                c.ReceiveDamage(_damage);

        if (explosionEffect)
        {
            var fx = Instantiate(explosionEffect, transform.position, Quaternion.identity);
            Destroy(fx, 3f);
        }

        float wait = 0.4f;
        if (_audio && explosionSound)
        {
            _audio.PlayOneShot(explosionSound);
            wait = Mathf.Max(wait, explosionSound.length / Mathf.Max(0.01f, _audio.pitch));
        }

        await UniTask.Delay(System.TimeSpan.FromSeconds(wait));
        Destroy(gameObject);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        float r = (_radius > 0f) ? _radius : (data ? data.explosionRadius : 0f);
        if (r > 0f)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, r);
        }
    }
#endif
}
