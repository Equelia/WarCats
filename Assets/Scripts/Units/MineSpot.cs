using UnityEngine;

[DisallowMultipleComponent]
public class MineSpot : MonoBehaviour
{
    [Header("Occupation")]
    [SerializeField, Tooltip("Радиус, внутри которого считаем, что место занято миной.")]
    private float occupyRadius = 0.6f;
    
    [Header("Team")]
    [Tooltip("-1 = любой, иначе только для указанной команды")]
    public int teamId = -1;

    public bool IsForTeam(int tid) => teamId == -1 || teamId == tid;

    [SerializeField, Tooltip("Какие слои считаем за мину (для поиска занятости). Оставь 0 и будет поиск по компоненту Mine.")]
    private LayerMask mineLayer;

    [Header("Visual")]
    [SerializeField] private Renderer markerRenderer; // любой рендерер круга/кольца
    [SerializeField] private Color freeColor     = new Color(0f, 1f, 0f, 0.55f);
    [SerializeField] private Color occupiedColor = new Color(1f, 0f, 0f, 0.55f);
    [SerializeField] private Color hoverColor    = new Color(0.2f, 0.8f, 1f, 0.7f);

    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private MaterialPropertyBlock _mpb;

    public bool IsOccupied { get; private set; }

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        if (!markerRenderer) markerRenderer = GetComponentInChildren<Renderer>(true);
        SetMarkerVisible(false);
    }

    public void SetMarkerVisible(bool v)
    {
        if (markerRenderer) markerRenderer.enabled = v;
    }

    public void RefreshOccupation()
    {
        bool occ = false;

        if (mineLayer != 0)
        {
            var hits = Physics.OverlapSphere(transform.position, occupyRadius, mineLayer);
            occ = hits.Length > 0;
        }
        else
        {
            var hits = Physics.OverlapSphere(transform.position, occupyRadius, ~0);
            foreach (var h in hits)
            {
                if (h.GetComponent<Mine>())
                {
                    occ = true; break;
                }
            }
        }

        IsOccupied = occ;
        SetColor(occ ? occupiedColor : freeColor);
    }

    public void SetHover(bool hover)
    {
        if (!markerRenderer || !markerRenderer.enabled) return;
        if (hover)
            SetColor(IsOccupied ? occupiedColor : hoverColor);
        else
            SetColor(IsOccupied ? occupiedColor : freeColor);
    }

    private void SetColor(Color c)
    {
        if (!markerRenderer) return;
        markerRenderer.GetPropertyBlock(_mpb);
        if (markerRenderer.sharedMaterial && markerRenderer.sharedMaterial.HasProperty(BaseColor))
            _mpb.SetColor(BaseColor, c);
        else
        {
            // fallback
            _mpb.SetColor(BaseColor, c);
        }
        markerRenderer.SetPropertyBlock(_mpb);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = IsOccupied ? Color.red : Color.green;
        Gizmos.DrawWireSphere(transform.position, occupyRadius);
    }
#endif
}
