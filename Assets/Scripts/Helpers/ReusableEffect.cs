using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Helpers
{
    /// <summary>
    /// Lightweight reusable effect runner that plays a ParticleSystem on the GameObject and disables it when done.
    /// Uses UniTask instead of coroutines.
    /// Expectation: the effect GameObject is a child of a unit prefab and is initially inactive or has PlayOnAwake = false.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class ReusableEffect : MonoBehaviour
    {
        ParticleSystem _ps;
        CancellationTokenSource _cts;

        void Awake()
        {
            _ps = GetComponent<ParticleSystem>();
            // keep GameObject inactive in prefab OR ensure it doesn't play on awake
        }

        /// <summary>
        /// Play the effect. If already playing it restarts.
        /// </summary>
        public void Play()
        {
            // cancel previous watcher
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }

            gameObject.SetActive(true);
            _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _ps.Play();

            _cts = new CancellationTokenSource();
            WatchAndDisableAsync(_cts.Token).Forget();
        }

        async UniTaskVoid WatchAndDisableAsync(CancellationToken ct)
        {
            try
            {
                // wait until particle system is no longer alive (children included)
                while (!ct.IsCancellationRequested && _ps != null && _ps.IsAlive(true))
                {
                    await UniTask.Yield(ct);
                }
            }
            catch (System.Exception)
            {
                // ignore
            }

            if (!ct.IsCancellationRequested)
            {
                try
                {
                    gameObject.SetActive(false);
                }
                catch { }
            }
        }

        public void StopAndHide()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }
            if (_ps != null) _ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (_cts != null) _cts.Cancel();
            if (_cts != null) _cts.Dispose();
            _cts = null;
        }
    }
}
