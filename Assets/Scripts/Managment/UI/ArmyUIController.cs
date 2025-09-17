using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class ArmyUIController : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private ArmyEconomy armyEconomy;
    
    [SerializeField] private UnitBootstrapper modularPrefab;

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
    [SerializeField] private MonoBehaviour deckProviderBehaviour;
    private IUnitDeckProvider deckProvider;
    [SerializeField] private SummonButton[] summonButtons = new SummonButton[6];

    [Header("Spawn Routing")]
    [SerializeField] private UnitSpawner spawner;
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
            OnArmyUpgradeProgressChanged(armyEconomy.State.armyUpgradeProgress, armyEconomy.State.armyUpgradeStepsRequired);
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
    private void OnCreditsChanged(int v) { if (creditsText) creditsText.text = v.ToString(); }
    private void OnPointsChanged(int v)  { if (pointsText) pointsText.text   = v.ToString(); }
    private void OnXpLevelChanged(int l) { if (levelText) levelText.text     = l.ToString(); }
    private void OnXpChanged(float f)    { xpBar?.Set01(f); }
    private void OnSlotsChanged(int occ, int max) { lampRow?.SetState(occ, max); }
    private void OnArmyUpgradeProgressChanged(int cur, int req)
    { if (armyUpgradeProgressText) armyUpgradeProgressText.text = $"{cur}\n---\n{req}"; }

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

            UnitArchetype archetype = null;

            bool hasEntry =
                deckProvider != null &&
                i < deckProvider.VisibleSlots &&
                deckProvider.TryGetArchetype(i, out archetype);

            if (!hasEntry)
            {
                var oldCtrl = btn.GetComponent<UnitSummonController>();
                if (oldCtrl) Destroy(oldCtrl);
                btn.ShowEmptySlot();
                continue;
            }

            var controller = btn.GetComponent<UnitSummonController>();
            if (!controller) controller = btn.gameObject.AddComponent<UnitSummonController>();

            controller.Configure(
                archetype: archetype,
                army: armyEconomy,
                spawner: spawner,
                baseProvider: FindObjectOfType<MonoBehaviour>() as ITeamBaseProvider,
                explicitSpawnPoint: null,
                areas: sharedSpawnAreas,
                forceTeamId: 0,
                forceLevel: null,
                forceSpawnRadius: null
            );

            var iconRenderer = btn.GetComponentInChildren<PrefabIconRenderer>(true);
            if (iconRenderer)
            {
                iconRenderer.gameObject.SetActive(true);
                iconRenderer.RenderModularArchetype(modularPrefab, archetype, teamId: 0);
            }

            btn.BindToController(controller);
        }
    }
}
