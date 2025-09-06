using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

/// <summary>
/// Bridges ArmyEconomy (logic) and concrete UI widgets.
/// Assign references in inspector.
/// </summary>
public class ArmyUIController : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private ArmyEconomy armyEconomy; // the player's army logic (teamId=0)

    [Header("Top Fields")]
    [SerializeField] private TMP_Text creditsText;
    [SerializeField] private TMP_Text pointsText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text armyUpgradeProgressText;


    [Header("XP UI")]
    [SerializeField] private XpBarUI xpBar;

    [Header("Lamps")]
    [SerializeField] private LampRowUI lampRow;

    [Header("Summon Buttons")]
    [SerializeField] private UnitDeckSO deck;
    [SerializeField] private SummonButton[] summonButtons = new SummonButton[6];

    [Header("Spawn Routing")]
    [SerializeField] private UnitSpawner spawner;
    
    [Header("Economy Buttons")]
    [SerializeField] private Button btnUpgradeLevel;
    [SerializeField] private Button btnIncreaseCapacity;
    [SerializeField] private Button btnExchangePoint;

    [SerializeField] private int capacityIncreaseBy = 1;
    [SerializeField] private int exchangeCreditsPerPoint = 50;

    [Inject] private DiContainer _container;

    private void Awake()
    {
        if (!armyEconomy) armyEconomy = FindObjectOfType<ArmyEconomy>();

        // Wire economy events to UI
        if (armyEconomy)
        {
            armyEconomy.OnCreditsChanged += OnCreditsChanged;
            armyEconomy.OnPointsChanged += OnPointsChanged;
            armyEconomy.OnXpLevelChanged += OnXpLevelChanged;
            armyEconomy.OnXpChanged += OnXpChanged;
            armyEconomy.OnSlotsChanged += OnSlotsChanged;
            
        }

        // Setup top texts
        OnCreditsChanged(armyEconomy.State.credits);
        OnPointsChanged(armyEconomy.State.points);
        OnXpLevelChanged(armyEconomy.State.xpLevel);
        OnXpChanged(armyEconomy.State.XpFill);
        OnSlotsChanged(armyEconomy.State.occupiedSlots, armyEconomy.State.maxSlots);
        
        if (btnUpgradeLevel) btnUpgradeLevel.onClick.AddListener(OnClickUpgradeLevel);
        if (btnIncreaseCapacity) btnIncreaseCapacity.onClick.AddListener(OnClickIncreaseCap);
        if (btnExchangePoint) btnExchangePoint.onClick.AddListener(OnClickExchangePoint);
        if(armyEconomy) armyEconomy.OnArmyUpgradeProgressChanged += OnArmyUpgradeProgressChanged;
        
        OnArmyUpgradeProgressChanged(
            armyEconomy.State.armyUpgradeProgress,
            armyEconomy.State.armyUpgradeStepsRequired
        );

        // Setup summon buttons from deck
        for (int i = 0; i < summonButtons.Length; i++)
        {
            var btn = summonButtons[i];
            if (!btn) continue;

            bool hasEntry =
                deck && deck.entries != null &&
                i < deck.visibleSlots &&
                i < deck.entries.Length &&
                deck.entries[i].unitPrefab != null;

            if (hasEntry)
            {
                btn.Configure(
                    deck.entries[i],
                    armyEconomy, // IArmyEconomy для игрока
                    spawner, // UnitSpawner из сцены
                    explicitSpawnPoint: null,
                    forceTeamId: 0 // игрок
                );
            }
            else
            {
                btn.Clear();
            }
            
            var view = btnUpgradeLevel ? btnUpgradeLevel.GetComponent<ArmyUpgradeButtonView>() : null;
            if (view) view.Bind(armyEconomy);
        }
    }

    private void OnDestroy()
    {
        if (armyEconomy)
        {
            armyEconomy.OnCreditsChanged -= OnCreditsChanged;
            armyEconomy.OnPointsChanged -= OnPointsChanged;
            armyEconomy.OnXpLevelChanged -= OnXpLevelChanged;
            armyEconomy.OnXpChanged -= OnXpChanged;
            armyEconomy.OnSlotsChanged -= OnSlotsChanged;
            armyEconomy.OnArmyUpgradeProgressChanged -= OnArmyUpgradeProgressChanged;
        }

        if (btnUpgradeLevel) btnUpgradeLevel.onClick.RemoveListener(OnClickUpgradeLevel);
        if (btnIncreaseCapacity) btnIncreaseCapacity.onClick.RemoveListener(OnClickIncreaseCap);
        if (btnExchangePoint) btnExchangePoint.onClick.RemoveListener(OnClickExchangePoint);
    }

    // -------- Economy -> UI ----------

    private void OnCreditsChanged(int value)
    {
        if (creditsText) creditsText.text = value.ToString();
    }

    private void OnPointsChanged(int value)
    {
        if (pointsText) pointsText.text = value.ToString();
    }

    private void OnXpLevelChanged(int xpLvl)
    {
        if (levelText) levelText.text = xpLvl.ToString();
    }

    private void OnXpChanged(float fill01)
    {
        xpBar?.Set01(fill01);
    }

    private void OnSlotsChanged(int occupied, int max)
    {
        lampRow?.SetState(occupied, max);
    }

    // -------- UI -> Economy ----------

    private void OnClickUpgradeLevel()
    {
        armyEconomy?.TryUpgradeArmyLevelStep();
    }

    private void OnClickIncreaseCap()
    {
        armyEconomy?.TryIncreaseMaxSlots(capacityIncreaseBy);
    }

    private void OnClickExchangePoint()
    {
        armyEconomy?.TryExchangePointForCredits(exchangeCreditsPerPoint);
    }
    
    private void OnArmyUpgradeProgressChanged(int current, int required)
    {
        if (armyUpgradeProgressText)
            armyUpgradeProgressText.text = $"{current}\n---\n{required}";
    }
}
