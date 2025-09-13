using UnityEngine;

namespace Units.Data
{
	[CreateAssetMenu(menuName = "Units/SniperData", fileName = "SniperData")]
	public class SniperData : UnitData
	{
		[Header("Sniper-specific")]
		[Tooltip("Скорость визуальной пули.")]
		public float bulletSpeed = 85f;

		private void Reset()
		{
			unitName       = "Снайпер";
			spawnCost      = 85;     // из таблицы
			attackRange    = 16f;
			attackCooldown = 2.4f;   // медленно, но больно
			accuracy       = 0.95f;
			vulnerability  = 0.05f;
			damage         = 25;
			maxHealth      = 22;
			moveSpeed      = 1.8f;

			stockMode      = StockMode.Unlimited;

			// Апгрейды:
			// L2: +здоровье
			// L3: увеличивается скорость атаки (уменьшаем cooldown)
			levelModifiers = new LevelModifier[]
			{
				new LevelModifier { level = 1, attackRangeMultiplier=1f, attackCooldownMultiplier=1f,   accuracyAdd=0f,   damageAdd=0, healthAdd=0,  moveSpeedMultiplier=1f, vulnerabilityAdd=0f },
				new LevelModifier { level = 2, attackRangeMultiplier=1f, attackCooldownMultiplier=1f,   accuracyAdd=0f,   damageAdd=0, healthAdd=12, moveSpeedMultiplier=1f, vulnerabilityAdd=0f },
				new LevelModifier { level = 3, attackRangeMultiplier=1f, attackCooldownMultiplier=0.65f,accuracyAdd=0.02f,damageAdd=0, healthAdd=0,  moveSpeedMultiplier=1f, vulnerabilityAdd=0f }
			};
		}
	}
}