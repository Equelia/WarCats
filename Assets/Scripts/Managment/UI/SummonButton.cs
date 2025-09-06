using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class SummonButton : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Button button;
    [SerializeField] private Image cooldownMask;   // BIG refill progress (LimitedWithRecharge)
    [SerializeField] private TMP_Text costText;
    [SerializeField] private GameObject iconObject;

    [Header("Stock UI")]
    [Tooltip("Shows only the current available amount (e.g., \"3\").")]
    [SerializeField] private TMP_Text stockText;

    private UnitSummonController controller;
    private bool _configured;

    private void Awake()
    {
        if (!button) button = GetComponent<Button>();
        if (cooldownMask) cooldownMask.fillAmount = 0f;

        // может существовать на объекте, но мы не считаем это «сконфигурированным»
        controller = GetComponent<UnitSummonController>();

        button.onClick.AddListener(OnClick);
        ShowEmptySlot(); // безопасное стартовое состояние
    }

    private void OnDestroy()
    {
        button.onClick.RemoveListener(OnClick);
        Unsubscribe();
    }

    /// <summary>Связать UI с контроллером (делает слот активным).</summary>
    public void BindToController(UnitSummonController c)
    {
        if (c == null)
        {
            ShowEmptySlot();
            return;
        }

        Unsubscribe();
        controller = c;
        Subscribe();

        _configured = true;

        // визуально активируем
        var cg = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 1f;

        if (costText) costText.gameObject.SetActive(true);
        if (iconObject) iconObject.SetActive(true);

        RefreshAll();
    }

    /// <summary>Показать слот как пустой: без текста, без иконки, невзаимодействующий.</summary>
    public void ShowEmptySlot()
    {
        Unsubscribe();
        controller = null;
        _configured = false;

        if (button) button.interactable = false;

        if (costText) { costText.text = string.Empty; costText.gameObject.SetActive(false); }
        if (iconObject) iconObject.SetActive(false);
        if (stockText) { stockText.text = string.Empty; stockText.gameObject.SetActive(false); }
        if (cooldownMask) cooldownMask.fillAmount = 0f;

        var cg = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0.35f;
    }

    private void Subscribe()
    {
        if (controller == null) return;
        controller.OnStockChanged        += HandleStockChanged;
        controller.OnRefillActiveChanged += HandleRefillActive;
        controller.OnRefillProgress      += HandleRefillProgress;
    }

    private void Unsubscribe()
    {
        if (controller == null) return;
        controller.OnStockChanged        -= HandleStockChanged;
        controller.OnRefillActiveChanged -= HandleRefillActive;
        controller.OnRefillProgress      -= HandleRefillProgress;
    }

    private void RefreshAll()
    {
        if (controller == null)
        {
            ShowEmptySlot();
            return;
        }

        // price
        if (costText)
        {
            costText.text = controller.SpawnCost.ToString();
            costText.gameObject.SetActive(true);
            if (iconObject) iconObject.SetActive(true);
        }

        // stock
        HandleStockChanged(controller.HasStockLimit ? controller.StockCurrent : -1,
                           controller.HasStockLimit ? controller.StockMax : -1);

        // refill state
        HandleRefillActive(controller.IsRefillingToMax);
        if (cooldownMask && !controller.IsRefillingToMax) cooldownMask.fillAmount = 0f;

        SetInteractable(controller.CanSummon);
    }

    private void HandleStockChanged(int current, int max)
    {
        if (!stockText) return;

        if (current < 0) // unlimited
        {
            stockText.gameObject.SetActive(false);
        }
        else
        {
            stockText.gameObject.SetActive(true);
            stockText.text = current.ToString();
        }
        SetInteractable(controller != null && controller.CanSummon);
    }

    private void HandleRefillActive(bool active)
    {
        SetInteractable(controller != null && controller.CanSummon);
        if (!cooldownMask) return;
        if (!active) cooldownMask.fillAmount = 0f;
    }

    private void HandleRefillProgress(float fill01)
    {
        if (cooldownMask) cooldownMask.fillAmount = Mathf.Clamp01(fill01);
    }

    private void OnClick()
    {
        if (!_configured || controller == null) return;

        bool ok = controller.TrySummon();
        if (!ok) SetInteractable(controller.CanSummon);
    }

    private void SetInteractable(bool interactable)
    {
        if (button) button.interactable = interactable;
        var cg = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        cg.alpha = interactable ? 1f : 0.85f;
    }
}
