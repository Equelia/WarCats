using System.Collections;
using UnityEngine;

namespace Helpers
{
    /// <summary>
    /// Lightweight helper that plays ParticleSystem on the GameObject and disables it when done.
    /// Designed to be attached to per-unit FX child (example: MuzzleFlash on unit prefab).
    /// Usage: keep GameObject inactive in prefab (or particle PlayOnAwake = false).
    /// Call Play() to show the effect (it will enable, play, wait until finished, then disable).
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class ReusableEffect : MonoBehaviour
    {
        ParticleSystem _ps;
        Coroutine _runner;

        void Awake()
        {
            _ps = GetComponent<ParticleSystem>();
            // ensure we start disabled when not used by code:
            // leave GameObject inactive in prefab OR allow this to disable it on Awake
            // we don't auto-disable here to not interfere if you want it active in editor.
        }

        /// <summary>
        /// Play the effect. If already playing, restarts it.
        /// Safe to call frequently.
        /// </summary>
        public void Play()
        {
            if (_runner != null)
            {
                StopCoroutine(_runner);
                _runner = null;
            }

            // ensure GameObject active so particles are visible
            gameObject.SetActive(true);

            // restart particle system
            _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _ps.Play();

            _runner = StartCoroutine(WatchAndDisable());
        }

        IEnumerator WatchAndDisable()
        {
            // Wait until particle system is fully done (including children/remaining lifetime)
            while (_ps.IsAlive(true))
            {
                yield return null;
            }

            _runner = null;
            // disable after finished to hide and be ready for reuse
            gameObject.SetActive(false);
        }

        /// <summary>Optional: stop immediately and hide.</summary>
        public void StopAndHide()
        {
            if (_runner != null)
            {
                StopCoroutine(_runner);
                _runner = null;
            }

            _ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            gameObject.SetActive(false);
        }
    }
}
