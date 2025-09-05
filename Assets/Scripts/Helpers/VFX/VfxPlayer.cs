using UnityEngine;

namespace Helpers
{
	public class VfxPlayer
	{
		
	}
}

public static class VfxPlayer
{
	public static void SpawnOneShot(GameObject prefab, Vector3 pos, Quaternion rot, float lifetimeOverride = -1f)
	{
		if (!prefab) return;

		var go = VfxPool.Get(prefab, pos, rot);

		float life = lifetimeOverride > 0f ? lifetimeOverride : CalcLifetime(go);
		// Перезапустить все ParticleSystem'ы
		var pss = go.GetComponentsInChildren<ParticleSystem>(true);
		foreach (var ps in pss) { ps.Clear(true); ps.Play(true); }

		// TrailRenderer reset
		var tr = go.GetComponent<TrailRenderer>();
		if (tr) { tr.Clear(); tr.emitting = true; }

		// Автодеспаун
		DespawnAfter(go, life).Forget();
	}

	private static async Cysharp.Threading.Tasks.UniTaskVoid DespawnAfter(GameObject go, float seconds)
	{
		await Cysharp.Threading.Tasks.UniTask.Delay((int)(seconds * 1000));
		if (go) VfxPool.Release(go);
	}

	private static float CalcLifetime(GameObject go)
	{
		float max = 0.5f;

		foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
		{
			var main = ps.main;
			float t = main.duration + main.startLifetime.constantMax;
			if (t > max) max = t;
		}
		foreach (var au in go.GetComponentsInChildren<AudioSource>(true))
		{
			if (au.clip) max = Mathf.Max(max, au.clip.length);
		}
		var tr = go.GetComponent<TrailRenderer>();
		if (tr) max = Mathf.Max(max, tr.time);

		return Mathf.Clamp(max, 0.2f, 5f);
	}
}
