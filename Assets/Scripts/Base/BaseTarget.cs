using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BaseTarget : MonoBehaviour, IDamageable
{
    [Header("Team")]
    public int teamId = -1;
    public bool autoDetectTeamByTag = true;

    [Header("Base Health")]
    public int maxHealth = 1000;
    public int currentHealth;

    [Header("Hit FX (each damage tick)")]
    public GameObject hitVfx;

    [Header("Persistent Damage Stages (enable by HP %)")]
    public GameObject vfxBase25Damage;
    public GameObject vfxBase50Damage;
    public GameObject vfxBase75Damage;

    [Header("Death FX")]
    public GameObject vfxBaseDestroyed;

    [Header("Sparks settings")]
    public float sparksCooldown = 1.0f;
    public string sparksChildNameContains = "SparksDamage";

    private float _lastSparksTime = -999f;
    private ParticleSystem[] _sparkSystems;
    private bool _stage25On, _stage50On, _stage75On, _dead;

    private void Awake()
    {
        if (autoDetectTeamByTag && teamId < 0)
        {
            if (CompareTag("PlayerBase")) teamId = 0;
            else if (CompareTag("EnemyBase")) teamId = 1;
        }

        if (teamId < 0) teamId = 1;

        currentHealth = Mathf.Max(1, maxHealth);
        _sparkSystems = GetComponentsInChildren<ParticleSystem>(true);
        SetActiveSafe(vfxBase25Damage, false);
        SetActiveSafe(vfxBase50Damage, false);
        SetActiveSafe(vfxBase75Damage, false);
    }

    public void ApplyDamage(int amount, GameObject source)
    {
        if (_dead) return;

        currentHealth -= Mathf.Max(1, amount);
        if (currentHealth < 0) currentHealth = 0;

        if (hitVfx) VfxPlayer.SpawnOneShot(hitVfx, transform.position, Quaternion.identity);

        if (Time.time - _lastSparksTime >= sparksCooldown)
        {
            _lastSparksTime = Time.time;
            PlaySparksChildren();
        }

        UpdateStageVfxByHealth();

        if (currentHealth == 0 && !_dead)
        {
            _dead = true;
            if (vfxBaseDestroyed) VfxPlayer.SpawnOneShot(vfxBaseDestroyed, transform.position, Quaternion.identity);
            UnitEvents.RaiseBaseDestroyed(teamId);
            Destroy(gameObject);
        }
    }

    private void PlaySparksChildren()
    {
        if (_sparkSystems == null || _sparkSystems.Length == 0) return;
        for (int i = 0; i < _sparkSystems.Length; i++)
        {
            var ps = _sparkSystems[i];
            if (!ps) continue;
            string n = ps.gameObject.name;
            if (!string.IsNullOrEmpty(n) && n.Contains(sparksChildNameContains))
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Play(true);
            }
        }
    }

    private void UpdateStageVfxByHealth()
    {
        float hpFrac = (maxHealth > 0) ? (currentHealth / (float)maxHealth) : 0f;
        if (!_stage25On && hpFrac <= 0.75f) { _stage25On = true; SetActiveSafe(vfxBase25Damage, true); }
        if (!_stage50On && hpFrac <= 0.50f) { _stage50On = true; SetActiveSafe(vfxBase50Damage, true); }
        if (!_stage75On && hpFrac <= 0.25f) { _stage75On = true; SetActiveSafe(vfxBase75Damage, true); }
    }

    private static void SetActiveSafe(GameObject go, bool v)
    {
        if (go && go.activeSelf != v) go.SetActive(v);
    }
}
