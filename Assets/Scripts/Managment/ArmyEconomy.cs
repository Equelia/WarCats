// ArmyEconomy.cs
using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zenject;
using Units.Logic;

public interface IArmyEconomy
{
    ArmyState State { get; }
    
    int StateLevelOwnerTeamId { get; }

    event Action OnStateChanged;
    event Action<int> OnCreditsChanged;
    event Action<int> OnPointsChanged;
    event Action<int,int> OnSlotsChanged; // (occupied, max)
    event Action<int> OnLevelChanged;
    event Action<float> OnXpChanged;      // 0..1
    event Action<int, int> OnArmyUpgradeProgressChanged; 


    void AddCredits(int amount);
    bool TrySpendCredits(int amount);

    void AddPoints(int amount);
    bool TrySpendPoints(int amount);

    void AddExperience(int xp);
    bool TryUpgradeArmyLevel();          // spend point -> level up army
    bool TryIncreaseMaxSlots(int add);   // spend point -> increase capacity
    bool TryExchangePointForCredits(int creditsPerPoint);

    /// <summary> Attempts to reserve one slot for a new unit. </summary>
    bool TryReserveSlot();
    void ReleaseSlot();
}


[DefaultExecutionOrder(-100)]
public class ArmyEconomy : MonoBehaviour, IArmyEconomy
{
    [Header("Config")]
    [Tooltip("Team id of this army (0=player, 1=enemy). UI uses the army for the player team).")]
    public int teamId = 0;
    
    public int StateLevelOwnerTeamId => teamId;

    [Tooltip("Credits passive income tick seconds.")]
    public float incomeInterval = 3f;

    [Tooltip("Credits gained per tick.")]
    public int incomeAmount = 5;

    [Tooltip("XP gained when an enemy unit dies (for this army).")]
    public int xpPerEnemyKill = 20;

    [Tooltip("Points granted on each level up.")]
    public int pointsPerLevelUp = 1;

    [Tooltip("How XP requirement grows each level. Final XP = baseXp + level*xpGrowth.")]
    public int baseXpToLevel = 100;
    public int xpGrowthPerLevel = 25;

    [Header("Initial State")]
    public ArmyState initial = new ArmyState
    {
        level = 1,
        credits = 100,
        points = 0,
        maxSlots = 8,
        occupiedSlots = 0,
        currentXp = 0,
        xpToLevel = 100
    };

    public ArmyState State { get; private set; }

    public event Action OnStateChanged;
    public event Action<int> OnCreditsChanged;
    public event Action<int> OnPointsChanged;
    public event Action<int,int> OnSlotsChanged;
    public event Action<int> OnLevelChanged;
    public event Action<float> OnXpChanged;
    public event Action<int> OnXpLevelChanged;      
    public event Action<int,int> OnArmyUpgradeProgressChanged;

    private bool _runningIncome;

    [Inject(Optional = true)] private UnitSpawner _spawner; // not required here, but useful if you want to spawn from logic

    private void Awake()
    {
        // Deep copy initial into State
        State = new ArmyState
        {
            level = initial.level,
            credits = initial.credits,
            points = initial.points,
            maxSlots = initial.maxSlots,
            occupiedSlots = 0,
            currentXp = 0,
            xpToLevel = initial.xpToLevel
        };

        // Listen deaths to grant XP to the opposite team.
        UnitEvents.OnUnitDied += HandleUnitDied;
        ArmyDirectory.Register(this); 
    }

    private void OnDestroy()
    {
        UnitEvents.OnUnitDied -= HandleUnitDied;
        ArmyDirectory.Unregister(this);
    }

    private void Start()
    {
        // Recompute xpToLevel based on config + current level
        State.xpToLevel = Mathf.Max(1, baseXpToLevel + (State.xpLevel  - 1) * xpGrowthPerLevel);
        RunIncomeLoop().Forget();
        RaiseAll();
    }

    private async UniTask  RunIncomeLoop()
    {
        _runningIncome = true;
        while (_runningIncome)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(incomeInterval), ignoreTimeScale: false);
            AddCredits(incomeAmount);
        }
    }

    private void HandleUnitDied(int deadTeamId)
    {
        // If an enemy unit (opposite team) dies -> grant XP to this army.
        if (deadTeamId != teamId)
        {
            AddExperience(xpPerEnemyKill);
        }
    }

    // ---------- Economy API ----------

    public void AddCredits(int amount)
    {
        if (amount == 0) return;
        State.credits = Mathf.Max(0, State.credits + amount);
        OnCreditsChanged?.Invoke(State.credits);
        OnStateChanged?.Invoke();
    }

    public bool TrySpendCredits(int amount)
    {
        if (amount <= 0) return true;
        if (State.credits < amount) return false;
        State.credits -= amount;
        OnCreditsChanged?.Invoke(State.credits);
        OnStateChanged?.Invoke();
        return true;
    }

    public void AddPoints(int amount)
    {
        if (amount == 0) return;
        State.points = Mathf.Max(0, State.points + amount);
        OnPointsChanged?.Invoke(State.points);
        OnStateChanged?.Invoke();
    }

    public bool TrySpendPoints(int amount)
    {
        if (amount <= 0) return true;
        if (State.points < amount) return false;
        State.points -= amount;
        OnPointsChanged?.Invoke(State.points);
        OnStateChanged?.Invoke();
        return true;
    }

    public void AddExperience(int xp)
    {
        if (xp <= 0) return;
        State.currentXp += xp;

        while (State.currentXp >= State.xpToLevel)
        {
            State.currentXp -= State.xpToLevel;

            State.xpLevel++;                      
            OnXpLevelChanged?.Invoke(State.xpLevel);  
            
            AddPoints(pointsPerLevelUp);           

            State.xpToLevel = Mathf.Max(1, baseXpToLevel + (State.xpLevel - 1) * xpGrowthPerLevel);
        }

        OnXpChanged?.Invoke(State.XpFill);
        OnStateChanged?.Invoke();
    }

    public bool TryUpgradeArmyLevel()
    {
        const int cost = 1;
        if (!TrySpendPoints(cost)) return false;

        State.level++;
        OnLevelChanged?.Invoke(State.level);
        
        OnStateChanged?.Invoke();
        return true;
    }
    
    
    public bool TryUpgradeArmyLevelStep()
    {
        if (State.points <= 0) return false;

        State.points--;
        State.armyUpgradeProgress++;

        OnPointsChanged?.Invoke(State.points);
        OnArmyUpgradeProgressChanged?.Invoke(State.armyUpgradeProgress, State.armyUpgradeStepsRequired);

        if (State.armyUpgradeProgress >= State.armyUpgradeStepsRequired)
        {
            State.armyUpgradeProgress = 0;
            State.level++;                        
            OnLevelChanged?.Invoke(State.level);
            
            OnArmyUpgradeProgressChanged?.Invoke(State.armyUpgradeProgress, State.armyUpgradeStepsRequired);

        }
        OnStateChanged?.Invoke();
        return true;
    }

    public bool TryIncreaseMaxSlots(int add)
    {
        const int cost = 1; // 1 point per capacity upgrade (you can parameterize)
        if (!TrySpendPoints(cost)) return false;

        State.maxSlots = Mathf.Max(State.maxSlots, State.maxSlots + add);
        OnSlotsChanged?.Invoke(State.occupiedSlots, State.maxSlots);
        OnStateChanged?.Invoke();
        return true;
    }

    public bool TryExchangePointForCredits(int creditsPerPoint)
    {
        if (!TrySpendPoints(1)) return false;
        AddCredits(creditsPerPoint);
        return true;
    }

    public bool TryReserveSlot()
    {
        if (State.occupiedSlots >= State.maxSlots) return false;
        State.occupiedSlots++;
        
        Debug.LogWarning($"[ArmyEconomy] ReserveSlot caller:\n{new System.Diagnostics.StackTrace(1, true)}");
        
        OnSlotsChanged?.Invoke(State.occupiedSlots, State.maxSlots);
        OnStateChanged?.Invoke();
        return true;
    }

    public void ReleaseSlot()
    {
        State.occupiedSlots = Mathf.Max(0, State.occupiedSlots - 1);
        OnSlotsChanged?.Invoke(State.occupiedSlots, State.maxSlots);
        OnStateChanged?.Invoke();
    }

    private void RaiseAll()
    {
        OnCreditsChanged?.Invoke(State.credits);
        OnPointsChanged?.Invoke(State.points);
        OnLevelChanged?.Invoke(State.level);
        OnSlotsChanged?.Invoke(State.occupiedSlots, State.maxSlots);
        OnXpLevelChanged?.Invoke(State.xpLevel); 
        OnXpChanged?.Invoke(State.XpFill);
        OnArmyUpgradeProgressChanged?.Invoke(State.armyUpgradeProgress, State.armyUpgradeStepsRequired);
        OnStateChanged?.Invoke();
    }

}
