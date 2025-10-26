using UnityEngine;
using UnityEngine.UI;

public class MinePlacementController : MonoBehaviour
{
    [Header("Camera / Raycast")]
    [SerializeField] private Camera worldCamera;
    [SerializeField] private LayerMask groundMask = ~0; 
    [SerializeField] private LayerMask spotMask;          

    [Header("Overlay (screen dim)")]
    [SerializeField] private Canvas dimCanvas;           
    [SerializeField] private Image dimImage;
    [SerializeField, Range(0f, 1f)] private float dimAlpha = 0.55f;

    // runtime
    private MineData _mineData;
    private bool _active;
    private MineSpot _hoverSpot;
    private int _selectedLevel;

    private IArmyEconomy _army;
    private UnitSummonController _ownerCtrl;

    private void Awake()
    {
        if (!worldCamera) worldCamera = Camera.main;
        EnsureDimmer();
    }
    
    public void StartPlacement(MineData data, int level, IArmyEconomy army, UnitSummonController ownerCtrl)
    {
        if (!data || !data.minePrefab)
        {
            Debug.LogError("MinePlacementController: MineData or Mine Prefab not set.");
            return;
        }
        if (!worldCamera)
        {
            Debug.LogError("MinePlacementController: worldCamera is null (tag your camera MainCamera or assign).");
            return;
        }

        _mineData = data;
        _selectedLevel = Mathf.Clamp(level, 1, 3);
        _army = army;
        _ownerCtrl = ownerCtrl;

        _active = true;

        SetDimmer(true);

        if (MineSpotsRegistry.Instance)
        {
            MineSpotsRegistry.Instance.SetMarkersVisible(true);
            MineSpotsRegistry.Instance.RefreshAll();
        }
    }

    public void StartPlacement(MineData data, int level)
    {
        Debug.LogWarning("MinePlacementController.StartPlacement(data, level) вызван без экономики и владельца — кредиты не будут списаны. Используйте перегрузку с IArmyEconomy и UnitSummonController.");
        StartPlacement(data, level, null, null);
    }

    private void Update()
    {
        if (!_active) return;

        UpdateHover();

        if (Input.GetMouseButtonDown(0))
        {
            if (_hoverSpot && !_hoverSpot.IsOccupied)
            {
                PlaceAtSpot(_hoverSpot);
            }
        }

        if (Input.GetMouseButtonDown(1))
        {
            CancelPlacement();
        }
    }

    private void UpdateHover()
    {
        MineSpot newHover = null;

        if (spotMask != 0)
        {
            var ray = worldCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, 500f, spotMask))
            {
                newHover = hit.collider.GetComponentInParent<MineSpot>();
            }
        }
        else
        {
            var ray = worldCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, 500f, groundMask))
            {
                newHover = FindClosestFreeSpotToPoint(hit.point, 1.2f);
            }
        }

        if (newHover != _hoverSpot)
        {
            if (_hoverSpot) _hoverSpot.SetHover(false);
            _hoverSpot = newHover;
            if (_hoverSpot) _hoverSpot.SetHover(true);
        }
    }

    private MineSpot FindClosestFreeSpotToPoint(Vector3 p, float maxDist)
    {
        MineSpot best = null;
        float bestSqr = maxDist * maxDist;

        if (MineSpotsRegistry.Instance == null) return null;

        foreach (var s in MineSpotsRegistry.Instance.Spots)
        {
            if (!s || s.IsOccupied) continue;
            float d2 = (s.transform.position - p).sqrMagnitude;
            if (d2 <= bestSqr)
            {
                best = s;
                bestSqr = d2;
            }
        }
        return best;
    }
    
    private void PlaceAtSpot(MineSpot spot)
    {
        int cost = _mineData.GetStatsForLevel(_selectedLevel).spawnCost;

        if (_army != null && !_army.TrySpendCredits(cost))
        {
            Debug.Log("MinePlacementController: Not enough credits to place a mine.");
            return;
        }

        var pos = spot.transform.position;
        var rot = spot.transform.rotation;

        var go = Instantiate(_mineData.minePrefab, pos, rot);

        var mine = go.GetComponent<Mine>() ?? go.AddComponent<Mine>();
        
        int ownerTeam = _ownerCtrl != null ? _ownerCtrl.teamId : 0;
        mine.Init(_mineData, _selectedLevel, ownerTeam);

        var minesLayer = LayerMask.NameToLayer("Mines");
        if (minesLayer != -1) go.layer = minesLayer;

        var anchor = go.GetComponent<MineSpotAnchor>() ?? go.AddComponent<MineSpotAnchor>();
        anchor.Bind(spot);

        _ownerCtrl?.OnMinePlacedSuccessfully();

        spot.RefreshOccupation();
        if (MineSpotsRegistry.Instance) MineSpotsRegistry.Instance.RefreshAll();

        CancelPlacement();
    }

    public void CancelPlacement()
    {
        _active = false;
        _mineData = null;
        _army = null;
        _ownerCtrl = null;

        if (_hoverSpot) { _hoverSpot.SetHover(false); _hoverSpot = null; }

        if (MineSpotsRegistry.Instance)
            MineSpotsRegistry.Instance.SetMarkersVisible(false);

        SetDimmer(false);
    }

    // ===== DIMMER =====

    private void EnsureDimmer()
    {
        if (!dimCanvas)
        {
            var go = new GameObject("[MineDimCanvas]");
            dimCanvas = go.AddComponent<Canvas>();
            dimCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            dimCanvas.sortingOrder = 0;
            go.AddComponent<CanvasScaler>();
            go.AddComponent<GraphicRaycaster>();
        }
        if (!dimImage)
        {
            var imgGO = new GameObject("Dim");
            imgGO.transform.SetParent(dimCanvas.transform, false);
            dimImage = imgGO.AddComponent<Image>();
            dimImage.color = new Color(0f, 0f, 0f, 0f);
            dimImage.raycastTarget = false; 
            var rt = dimImage.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
        SetDimmer(false);
    }

    private void SetDimmer(bool on)
    {
        if (!dimImage || !dimCanvas) return;
        dimCanvas.enabled = on;
        dimImage.color = on ? new Color(0f, 0f, 0f, dimAlpha) : new Color(0f, 0f, 0f, 0f);
    }
}
