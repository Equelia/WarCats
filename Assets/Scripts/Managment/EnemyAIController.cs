using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class EnemyAIController : MonoBehaviour
{
    [Header("Who/Where")]
    [SerializeField] private int teamId = 1;                    // враг = 1
    [SerializeField] private ArmyEconomy enemyArmy;             // экономика врага
    [SerializeField] private UnitSpawner spawner;               // ваш спавнер (тот же, что у игрока)
    [SerializeField] private MonoBehaviour deckProviderBehaviour; // IUnitDeckProvider для врага (можно SceneUnitDeck)
    private IUnitDeckProvider deck;

    [Header("Spawn setup (как у UI)")]
    [SerializeField] private SpawnAreaBase[] spawnAreas;        // зоны спавна врага
    [SerializeField] private float spawnRadiusFallback = 4f;    // если зон нет

    [Header("Pacing")]
    [Tooltip("Минимальная пауза между решениями.")]
    [SerializeField] private float thinkIntervalMin = 0.5f;
    [Tooltip("Максимальная пауза между решениями.")]
    [SerializeField] private float thinkIntervalMax = 1.5f;

    [Tooltip("Не начинать спам, пока не накопим столько кредитов (анти-затык в начале).")]
    [SerializeField] private int minCreditsToAct = 10;

    [Header("Strategy")]
    [Tooltip("Макс. попыток за один тик принять решение о призыве.")]
    [SerializeField] private int attemptsPerTick = 3;

    [Tooltip("Если свободных слотов мало — копим на дорогие юниты.")]
    [Range(0f,1f)] [SerializeField] private float preferExpensiveWhenSlotsLow = 0.7f;

    [Tooltip("Кредиты, при которых точно пытаемся вызвать самого дорогого доступного юнита.")]
    [SerializeField] private int rushThreshold = 120;

    [Header("Auto-upgrades (очки)")]
    [Tooltip("Порог очков, при котором пробуем апгрейд армии (level).")]
    [SerializeField] private int pointsForLevelUp = 1;

    [Tooltip("Порог очков для увеличения капасити (слоты). 0 = не трогать.")]
    [SerializeField] private int pointsForCapacity = 0;
    [SerializeField] private int capacityIncreaseBy = 1;

    // внутреннее
    private UnitSummonController[] _controllers; // такие же, как под UI-кнопками, только “невидимые”
    private System.Random _rng;

    private void Awake()
    {
        _rng = new System.Random();

        if (!enemyArmy)  enemyArmy  = FindObjectOfType<ArmyEconomy>();
        if (!spawner)    spawner    = FindObjectOfType<UnitSpawner>();
        deck = deckProviderBehaviour as IUnitDeckProvider;

        BuildControllers();
        // регистрируем армию в директории (если это ещё не сделано)
        ArmyDirectory.Register(enemyArmy);

        // старт размышления
        _ = BrainLoop();
    }

    private void OnDestroy()
    {
        // подчистка
        if (_controllers != null)
            foreach (var c in _controllers)
                if (c) Destroy(c);
    }

    /// Создаём “невидимые кнопки призыва” — UnitSummonController для каждого слота колоды врага.
    private void BuildControllers()
    {
        if (deck == null || deck.VisibleSlots <= 0) return;

        _controllers = new UnitSummonController[deck.VisibleSlots];

        for (int i = 0; i < deck.VisibleSlots; i++)
        {
            if (!deck.TryGetArchetype(i, out var archetype) || !archetype) continue;

            var host = new GameObject($"[EnemySummonController_{i}]");
            host.transform.SetParent(transform, false);

            var ctrl = host.AddComponent<UnitSummonController>();
            ctrl.Configure(
                archetype:        archetype,
                army:             enemyArmy,
                spawner:          spawner,
                baseProvider:     FindObjectOfType<TeamBaseProvider>(),
                explicitSpawnPoint: null,
                areas:            spawnAreas,
                forceTeamId:      teamId,
                forceLevel:       null,
                forceSpawnRadius: spawnRadiusFallback
            );

            _controllers[i] = ctrl;
        }
    }

    private async UniTaskVoid BrainLoop()
    {
        // маленькая задержка на прогрев сцены
        await UniTask.Delay(350);

        while (this && enabled)
        {
            TryDoUpgrades();

            // если нет свободных слотов — подождём
            if (enemyArmy.State.occupiedSlots >= enemyArmy.State.maxSlots)
            {
                await DelayThink(); 
                continue;
            }

            // если денег совсем мало — подождём
            if (enemyArmy.State.credits < minCreditsToAct)
            {
                await DelayThink();
                continue;
            }

            // Попробуем несколько раз что-то призвать за тик
            for (int a = 0; a < attemptsPerTick; a++)
            {
                if (!TrySummonOne()) break; // если не получилось — прекращаем в этом тике
                // небольшая пауза между попытками (необязательно)
                await UniTask.Delay(50);
                if (enemyArmy.State.occupiedSlots >= enemyArmy.State.maxSlots) break;
            }

            await DelayThink();
        }
    }

    private async UniTask DelayThink()
    {
        float t = Mathf.Lerp(thinkIntervalMin, thinkIntervalMax, UnityEngine.Random.value);
        await UniTask.Delay((int)(t * 1000));
    }

    /// Выбираем слот/юнит и “нажимаем кнопку” (UnitSummonController.TrySummon()).
    private bool TrySummonOne()
    {
        if (_controllers == null || _controllers.Length == 0) return false;

        // Готовим список доступных по цене
        int credits = enemyArmy.State.credits;
        var candidates = System.Linq.Enumerable
            .Range(0, _controllers.Length)
            .Where(i => _controllers[i] && _controllers[i].CanSummon)
            .Select(i => new { idx = i, cost = _controllers[i].SpawnCost })
            .Where(e => e.cost <= credits)
            .ToList();

        if (candidates.Count == 0) return false;

        // Стратегия выбора:
        // - если много денег (rush) — самый дорогой доступный
        // - если мало слотов (занято >= 80%) — чаще берём дорогих
        // - иначе случай по стоимости с лёгким смещением к средним
        int maxSlots = Mathf.Max(1, enemyArmy.State.maxSlots);
        float fill = (float)enemyArmy.State.occupiedSlots / maxSlots;

        int pickIdx;
        if (credits >= rushThreshold)
        {
            pickIdx = candidates.OrderByDescending(c => c.cost).First().idx;
        }
        else if (fill > 0.8f && UnityEngine.Random.value < preferExpensiveWhenSlotsLow)
        {
            pickIdx = candidates.OrderByDescending(c => c.cost).First().idx;
        }
        else
        {
            // случай по стоимости, но не самый дешевый
            var sorted = candidates.OrderBy(c => c.cost).ToArray();
            int lo = Mathf.Max(0, (int)(sorted.Length * 0.25f));
            int hi = sorted.Length - 1;
            int k  = UnityEngine.Random.Range(lo, hi + 1);
            pickIdx = sorted[k].idx;
        }

        var ctrl = _controllers[pickIdx];
        if (!ctrl) return false;

        // Нажимаем кнопку
        return ctrl.TrySummon();
    }

    private void TryDoUpgrades()
    {
        // апгрейд уровня за очки (простое правило)
        if (enemyArmy.State.points >= pointsForLevelUp)
            enemyArmy.TryUpgradeArmyLevelStep();

        // расширение капасити, если просили
        if (pointsForCapacity > 0 && enemyArmy.State.points >= pointsForCapacity)
            enemyArmy.TryIncreaseMaxSlots(capacityIncreaseBy);
    }
}
