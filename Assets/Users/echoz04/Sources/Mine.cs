using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Units.Logic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class Mine : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] private int teamToDamage = 1;
    [SerializeField] private int damage = 993;

    [Header("FX")]
    [SerializeField] private GameObject explosionEffect;
    [SerializeField] private GameObject visuals;
    [SerializeField] private bool effectIsChild = true;
    [SerializeField, Range(0.1f, 10f)] private float fxTimeoutSeconds = 3f;

    [Header("Audio")]
    [SerializeField] private AudioClip explosionClip;
    [SerializeField, Range(0f, 1f)] private float explosionVolume = 1f;

    private bool _armed = true;
    private CancellationTokenSource _cts;
    private AudioSource _audioSource;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
    }

    private void OnEnable()
    {
        _armed = true;
        _cts = new CancellationTokenSource();
        if (effectIsChild && explosionEffect != null)
            explosionEffect.SetActive(false);
    }

    private void OnDisable()
    {
        _armed = false;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private void OnCollisionEnter(Collision other)
    {
        HandleHitAsync(other).Forget();
    }

    private async UniTaskVoid HandleHitAsync(Collision other)
    {
        if (!_armed) return;
        if (!other.gameObject.TryGetComponent(out UnitController controller)) return;
        if (controller.TeamId != teamToDamage) return;

        _armed = false;

        try
        {
            controller.ReceiveDamage(damage);
            await PlayExplosionFxAsync(_cts.Token);
            Destroy(gameObject);
            Debug.Log("Boom");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            Debug.LogException(e, this);
            gameObject.SetActive(false);
        }
    }

private async UniTask PlayExplosionFxAsync(CancellationToken token)
{
    UniTask sfxTask = UniTask.CompletedTask;
    if (_audioSource != null && explosionClip != null)
    {
        _audioSource.PlayOneShot(explosionClip, explosionVolume);
        var seconds = explosionClip.length / Mathf.Max(0.01f, _audioSource.pitch);
        sfxTask = UniTask.Delay(TimeSpan.FromSeconds(seconds), cancellationToken: token);
    }

    UniTask vfxTask;
    if (explosionEffect == null)
    {
        vfxTask = UniTask.Yield(PlayerLoopTiming.Update, token);
    }
    else if (effectIsChild)
    {
        explosionEffect.transform.SetPositionAndRotation(transform.position, transform.rotation);
        explosionEffect.SetActive(true);
        visuals.SetActive(false);

        if (explosionEffect.TryGetComponent(out ParticleSystem ps))
        {
            vfxTask = UniTask.WhenAny(
                UniTask.WaitUntil(() => !ps.IsAlive(true), cancellationToken: token),
                UniTask.Delay(TimeSpan.FromSeconds(fxTimeoutSeconds), cancellationToken: token)
            ).AsUniTask();
        }
        else
        {
            vfxTask = UniTask.Delay(TimeSpan.FromSeconds(0.25f), cancellationToken: token);
        }
    }
    else
    {
        var fx = Instantiate(explosionEffect, transform.position, transform.rotation);
        vfxTask = UniTask.Create(async () =>
        {
            try
            {
                if (fx.TryGetComponent(out ParticleSystem ps))
                {
                    ps.Play(true);
                    await UniTask.WhenAny(
                        UniTask.WaitUntil(() => !ps.IsAlive(true), cancellationToken: token),
                        UniTask.Delay(TimeSpan.FromSeconds(fxTimeoutSeconds), cancellationToken: token)
                    );
                }
                else
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(0.5f), cancellationToken: token);
                }
            }
            finally
            {
                if (fx != null) Destroy(fx);
            }
        });
    }

    await UniTask.WhenAll(sfxTask, vfxTask);
}
}
