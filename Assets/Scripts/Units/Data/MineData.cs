// MineData.cs
using UnityEngine;
using Units.Data;

[CreateAssetMenu(menuName = "Units/Mine Data", fileName = "MineData")]
public class MineData : UnitData
{
	[Header("Mine Specific (base values)")]
	public float explosionRadius = 3f;     // базовый радиус
	public int   explosionDamage = 100;    // базовый урон
	public GameObject minePrefab;

	[Header("FX & Sound")]
	public GameObject explosionEffect;
	public AudioClip  explosionSound;

	[System.Serializable]
	public struct MineLevelModifier
	{
		[Tooltip("Level 1..3")] public int level;
		[Tooltip("+ к урону")] public int damageAdd;
		[Tooltip("× к радиусу (1 = без изменений)")] public float radiusMul;
	}

	[Header("Per-Level Mods (optional)")]
	public MineLevelModifier[] mineLevelModifiers = new MineLevelModifier[0];

	// -- API: вернуть эффективные значения для данного уровня (1..3) --
	public int   GetExplosionDamageForLevel(int level)
	{
		level = Mathf.Clamp(level, 1, 3);
		int dmg = explosionDamage;
		foreach (var m in mineLevelModifiers)
			if (m.level == level) { dmg += m.damageAdd; break; }
		return Mathf.Max(0, dmg);
	}

	public float GetExplosionRadiusForLevel(int level)
	{
		level = Mathf.Clamp(level, 1, 3);
		float r = explosionRadius;
		foreach (var m in mineLevelModifiers)
			if (m.level == level) { r *= (m.radiusMul == 0f ? 1f : m.radiusMul); break; }
		return Mathf.Max(0.01f, r);
	}
}