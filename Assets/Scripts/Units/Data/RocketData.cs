using UnityEngine;

namespace Units.Data
{
    [CreateAssetMenu(menuName = "Units/RocketData", fileName = "RocketData")]
    public class RocketData : UnitData
    {
        [Header("Rocket-specific")]
        public float splashRadius = 2.5f;

        public float rocketSpeed = 25f;

        public int[] spawnCountPerSlot = new int[3] { 1, 1, 2 };

        private void Reset()
        {
            unitName       = "Рокетчик";
            spawnCost      = 65;     
            attackRange    = 10f;
            attackCooldown = 1.6f;
            accuracy       = 0.75f;
            vulnerability  = 0.1f;
            damage         = 12;      
            maxHealth      = 28;
            moveSpeed      = 1.9f;

            stockMode      = StockMode.Unlimited;

            levelModifiers = new LevelModifier[]
            {
                new LevelModifier { level = 1, attackRangeMultiplier=1f, attackCooldownMultiplier=1f, accuracyAdd=0f, damageAdd=0, healthAdd=0, moveSpeedMultiplier=1f, vulnerabilityAdd=0f },
                new LevelModifier { level = 2, attackRangeMultiplier=1f, attackCooldownMultiplier=1f, accuracyAdd=0f, damageAdd=0, healthAdd=0, moveSpeedMultiplier=1.25f, vulnerabilityAdd=0f },
                new LevelModifier { level = 3, attackRangeMultiplier=1f, attackCooldownMultiplier=1f, accuracyAdd=0f, damageAdd=0, healthAdd=0, moveSpeedMultiplier=1f,   vulnerabilityAdd=0f }
            };
        }

        public override int GetSpawnCountForLevel(int level)
        {
            int idx = Mathf.Clamp(level, 1, 3) - 1;
            if (spawnCountPerSlot == null || spawnCountPerSlot.Length < 3)
                return 1;
            return Mathf.Max(1, spawnCountPerSlot[idx]); // по твоей таблице: 1,1,2
        }
    }
}
