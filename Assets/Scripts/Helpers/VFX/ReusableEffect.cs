using UnityEngine;

[DisallowMultipleComponent]
public class ReusableEffect : MonoBehaviour
{
	private ParticleSystem[] _ps;
	private AudioSource[] _audio;

	private void Awake()
	{
		_ps = GetComponentsInChildren<ParticleSystem>(true);
		_audio = GetComponentsInChildren<AudioSource>(true);
	}

	public void Play()
	{
		if (_ps != null)
			foreach (var p in _ps) { p.Clear(true); p.Play(true); }
		if (_audio != null)
			foreach (var a in _audio) { if (a.clip) a.Play(); }
	}

	public void Stop()
	{
		if (_ps != null)
			foreach (var p in _ps) p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
		if (_audio != null)
			foreach (var a in _audio) a.Stop();
	}
}