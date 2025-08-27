using UnityEngine;

namespace Units.Data
{
	/// <summary>
	/// Base data for all unit types. Use ScriptableObject assets to define concrete units.
	/// Level indexes are 1..3 (level 0 is base/default and treated as level 1 in runtime).
	/// </summary>
	public abstract class UnitData : ScriptableObject
	{
		[Header("General")] public string unitName = "Unit";

		[Tooltip("Cost to spawn this unit (credits).")]
		public int spawnCost = 10;

		[Tooltip("Cooldown seconds between spawns (time to be able to spawn again).")]
		public float spawnCooldown = 1f;

		[Header("Combat (base stats)")] public float attackRange = 5f;

		[Tooltip("Attack cooldown in seconds (lower = faster attacks).")]
		public float attackCooldown = 1f;

		[Tooltip("Accuracy of each shot. (0..1)")] [Range(0f, 1f)]
		public float accuracy = 0.9f;

		[Tooltip(
			"Base vulnerability. NOTE: in this project higher vulnerability value means HARDER to hit (it's used as a protection factor). Range 0..1.")]
		[Range(0f, 1f)]
		public float vulnerability = 0f;

		public int damage = 5;
		public int maxHealth = 20;

		[Header("Movement")] public float moveSpeed = 2.5f;

		[Header("Level modifiers")] [Tooltip("Modifiers for levels 1..3. If empty, base stats used.")]
		public LevelModifier[] levelModifiers = new LevelModifier[0];

		[System.Serializable]
		public struct LevelModifier
		{
			[Tooltip("Level number (1..3)")] public int level;
			public float attackRangeMultiplier; // multiply base attackRange
			public float attackCooldownMultiplier;
			public float accuracyAdd; // add to base accuracy
			public int damageAdd; // add to base damage
			public int healthAdd;
			public float moveSpeedMultiplier;
			public float vulnerabilityAdd; // add to base vulnerability
		}

		/// <summary>
		/// Compose runtime stats for a given level (clamped to 1..3).
		/// </summary>
		public UnitRuntimeStats GetStatsForLevel(int level)
		{
			var stats = new UnitRuntimeStats
			{
				attackRange = attackRange,
				attackCooldown = attackCooldown,
				accuracy = accuracy,
				damage = damage,
				maxHealth = maxHealth,
				moveSpeed = moveSpeed,
				spawnCost = spawnCost,
				spawnCooldown = spawnCooldown,
				vulnerability = vulnerability
			};

			if (level < 1) level = 1;
			if (level > 3) level = 3;

			// find modifier for this level if provided
			foreach (var mod in levelModifiers)
			{
				if (mod.level == level)
				{
					stats.attackRange *= (mod.attackRangeMultiplier == 0f ? 1f : mod.attackRangeMultiplier);
					stats.attackCooldown *= (mod.attackCooldownMultiplier == 0f ? 1f : mod.attackCooldownMultiplier);
					stats.accuracy += mod.accuracyAdd;
					stats.damage += mod.damageAdd;
					stats.maxHealth += mod.healthAdd;
					stats.moveSpeed *= (mod.moveSpeedMultiplier == 0f ? 1f : mod.moveSpeedMultiplier);
					stats.vulnerability += mod.vulnerabilityAdd;
					break;
				}
			}

			// clamp sensible ranges
			stats.accuracy = Mathf.Clamp01(stats.accuracy);
			stats.vulnerability = Mathf.Clamp01(stats.vulnerability);
			if (stats.attackCooldown < 0.05f) stats.attackCooldown = 0.05f;

			return stats;
		}
	}

	/// <summary>
	/// Simple container for computed runtime stats.
	/// </summary>
	public struct UnitRuntimeStats
	{
		public float attackRange;
		public float attackCooldown;
		public float accuracy;
		public int damage;
		public int maxHealth;
		public float moveSpeed;
		public int spawnCost;
		public float spawnCooldown;
		public float vulnerability; // 0..1, interpretation: higher -> harder to hit (adds protection)
	}
}