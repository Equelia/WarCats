using UnityEngine;

namespace Units.Data
{
    [CreateAssetMenu(menuName = "Units/AutomaticData", fileName = "AutomaticData")]
    public class AutomaticData : UnitData
    {
        [Header("Automatic-specific (VFX/feel)")]
        [Tooltip("Сколько визуальных пуль вылетает за одну атаку (очередь).")]
        public int burstCount = 5;

        [Tooltip("Интервал между пулями в очереди, сек.")]
        public float burstInterval = 0.06f;

        [Tooltip("Горизонтальное разбрасывание (радиус) вокруг точки прицела для «шальных» пуль.")]
        public float spreadRadius = 0.35f;

        [Tooltip("Скорость визуальной пули.")]
        public float bulletSpeed = 55f;

        private void Reset()
        {
            // Бэйз-статы «Автоматчика» (можешь подогнать под свою мету)
            unitName       = "Автоматчик";
            spawnCost      = 25;   // из таблицы
            attackRange    = 8f;   // базовая дальность
            attackCooldown = 0.5f; // перерыв между «очередями»
            accuracy       = 0.65f;
            vulnerability  = 0.05f;
            damage         = 3;    // урон за «выстрел» сервиса (логика урона – в твоём CombatService)
            maxHealth      = 22;
            moveSpeed      = 2.4f;

            stockMode      = StockMode.Unlimited;

            // Апгрейды уровнями (1..3):
            levelModifiers = new LevelModifier[]
            {
                new LevelModifier
                {
                    level = 1,
                    attackRangeMultiplier = 1f,
                    attackCooldownMultiplier = 1f,
                    accuracyAdd = 0f,
                    damageAdd = 0,
                    healthAdd = 0,
                    moveSpeedMultiplier = 1f,
                    vulnerabilityAdd = 0f
                },
                new LevelModifier
                {
                    level = 2, // 1-й апгрейд: +точность
                    attackRangeMultiplier = 1f,
                    attackCooldownMultiplier = 1f,
                    accuracyAdd = 0.10f,
                    damageAdd = 0,
                    healthAdd = 0,
                    moveSpeedMultiplier = 1f,
                    vulnerabilityAdd = 0f
                },
                new LevelModifier
                {
                    level = 3, // 2-й апгрейд: +дальность и +точность
                    attackRangeMultiplier = 1.20f,
                    attackCooldownMultiplier = 1f,
                    accuracyAdd = 0.18f,
                    damageAdd = 0,
                    healthAdd = 0,
                    moveSpeedMultiplier = 1f,
                    vulnerabilityAdd = 0f
                }
            };
        }
    }
}
