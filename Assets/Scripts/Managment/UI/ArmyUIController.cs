using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;
using Units.Logic;

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
    [Tooltip("Any component that implements IUnitDeckProvider (e.g., SceneUnitDeck)")]
    [SerializeField] private MonoBehaviour deckProviderBehaviour;
    private IUnitDeckProvider deckProvider;
    [SerializeField] private SummonButton[] summonButtons = new SummonButton[6];

    [Header("Spawn Routing")]
    [SerializeField] private UnitSpawner spawner;

    [Tooltip("Optional shared spawn areas for all summon slots (can be left empty if each controller has its own).")]
    [SerializeField] private SpawnAreaBase[] sharedSpawnAreas;

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

        // events
        if (armyEconomy)
        {
            armyEconomy.OnCreditsChanged += OnCreditsChanged;
            armyEconomy.OnPointsChanged += OnPointsChanged;
            armyEconomy.OnXpLevelChanged += OnXpLevelChanged;
            armyEconomy.OnXpChanged += OnXpChanged;
            armyEconomy.OnSlotsChanged += OnSlotsChanged;
            armyEconomy.OnArmyUpgradeProgressChanged += OnArmyUpgradeProgressChanged;

            OnCreditsChanged(armyEconomy.State.credits);
            OnPointsChanged(armyEconomy.State.points);
            OnXpLevelChanged(armyEconomy.State.xpLevel);
            OnXpChanged(armyEconomy.State.XpFill);
            OnSlotsChanged(armyEconomy.State.occupiedSlots, armyEconomy.State.maxSlots);
            OnArmyUpgradeProgressChanged(
                armyEconomy.State.armyUpgradeProgress,
                armyEconomy.State.armyUpgradeStepsRequired
            );
        }

        if (btnUpgradeLevel) btnUpgradeLevel.onClick.AddListener(OnClickUpgradeLevel);
        if (btnIncreaseCapacity) btnIncreaseCapacity.onClick.AddListener(OnClickIncreaseCap);
        if (btnExchangePoint) btnExchangePoint.onClick.AddListener(OnClickExchangePoint);

        var view = btnUpgradeLevel ? btnUpgradeLevel.GetComponent<ArmyUpgradeButtonView>() : null;
        if (view && armyEconomy) view.Bind(armyEconomy);

        deckProvider = deckProviderBehaviour as IUnitDeckProvider;
        SetupSummonSlots();
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
    private void OnCreditsChanged(int value) { if (creditsText) creditsText.text = value.ToString(); }
    private void OnPointsChanged(int value)  { if (pointsText) pointsText.text   = value.ToString(); }
    private void OnXpLevelChanged(int xpLvl) { if (levelText) levelText.text     = xpLvl.ToString(); }
    private void OnXpChanged(float fill01)   { xpBar?.Set01(fill01); }
    private void OnSlotsChanged(int occupied, int max) { lampRow?.SetState(occupied, max); }
    private void OnArmyUpgradeProgressChanged(int current, int required)
    {
        if (armyUpgradeProgressText)
            armyUpgradeProgressText.text = $"{current}\n---\n{required}";
    }

    // -------- UI -> Economy ----------
    private void OnClickUpgradeLevel() => armyEconomy?.TryUpgradeArmyLevelStep();
    private void OnClickIncreaseCap()  => armyEconomy?.TryIncreaseMaxSlots(capacityIncreaseBy);
    private void OnClickExchangePoint()=> armyEconomy?.TryExchangePointForCredits(exchangeCreditsPerPoint);

    // -------- Summon slots wiring ----------
    private void SetupSummonSlots()
    {
        for (int i = 0; i < summonButtons.Length; i++)
        {
            var btn = summonButtons[i];
            if (!btn) continue;

            UnitController unitPrefab = null;

            bool hasEntry =
                deckProvider != null &&
                i < deckProvider.VisibleSlots &&
                deckProvider.TryGetPrefab(i, out unitPrefab); 

            if (!hasEntry)
            {
                var oldCtrl = btn.GetComponent<UnitSummonController>();
                if (oldCtrl) Destroy(oldCtrl);
                btn.ShowEmptySlot();
                continue;
            }

            // 3) Контроллер призыва
            var controller = btn.GetComponent<UnitSummonController>();
            if (!controller) controller = btn.gameObject.AddComponent<UnitSummonController>();

            controller.Configure(
                prefab: unitPrefab,                         
                army: armyEconomy,
                spawner: spawner,
                baseProvider: FindObjectOfType<MonoBehaviour>() as ITeamBaseProvider,
                explicitSpawnPoint: null,
                areas: sharedSpawnAreas,
                forceTeamId: 0,
                forceLevel: null,
                forceSpawnRadius: null
            );
            
            var raw = btn.GetComponentInChildren<UnityEngine.UI.RawImage>(true);
            if (raw)
            {
                raw.enabled = true;
                raw.gameObject.SetActive(true); 
            }
            
            var icon = btn.GetComponentInChildren<PrefabIconRenderer>(true);
            if (icon) icon.RenderPrefab(unitPrefab.gameObject);

            btn.BindToController(controller);
        }
    }

}
