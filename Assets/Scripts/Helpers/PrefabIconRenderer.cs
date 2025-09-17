using System.Linq;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PrefabIconRenderer : MonoBehaviour
{
    [Header("Output")]
    [Tooltip("UI RawImage that will display the RenderTexture.")]
    public RawImage target;

    [Header("Render Settings")]
    public int textureWidth = 256;
    public int textureHeight = 256;
    [Tooltip("Camera FOV degrees used for framing.")]
    public float cameraFov = 25f;
    [Range(0f, 1f)] public float padding = 0.15f;
    [Tooltip("Background color; alpha 0 = fully transparent.")]
    public Color clearColor = new Color(0, 0, 0, 0);
    [Tooltip("Rotation of the model in the icon (Euler).")]
    public Vector3 modelEuler = new Vector3(0, 135f, 0);
    [Tooltip("Optional uniform scale multiplier for the model.")]
    public float modelScale = 1f;

    [Header("Layer")]
    [Tooltip("Layer used so only the icon camera sees the model.")]
    public string iconLayerName = "UIIcon";

    private static readonly float RigStep = 50f;
    private static readonly Vector3 RigBase = new Vector3(10000f, 10000f, 10000f);

    // Runtime
    private GameObject _rigRoot;
    private Camera _iconCam;
    private RenderTexture _rt;
    private GameObject _instance;
    private int _iconLayer;

    private void Awake()
    {
        if (!target)
        {
            Debug.LogError($"{name}: Target RawImage is not assigned.");
            enabled = false;
            return;
        }

        _iconLayer = LayerMask.NameToLayer(iconLayerName);
        if (_iconLayer == -1)
        {
            Debug.LogWarning($"{name}: Layer '{iconLayerName}' not found. Using Default layer (0).");
            _iconLayer = 0;
        }

        BuildRig();
    }

    private void OnDestroy() => Cleanup();

    /// <summary>
    /// Instantiate a modular prefab, configure it only for visuals (no logic),
    /// and render it to the target RawImage.
    /// </summary>
    public void RenderModularArchetype(UnitBootstrapper modularPrefab, UnitArchetype archetype, int teamId)
    {
        if (!modularPrefab || !archetype)
        {
            Debug.LogWarning("RenderModularArchetype: missing modularPrefab or archetype.");
            return;
        }

        if (!_iconCam) BuildRig();
        if (target) target.enabled = true;

        if (_instance) DestroyImmediate(_instance);

        // Create instance
        _instance = Instantiate(modularPrefab.gameObject, _rigRoot.transform);
        _instance.transform.localPosition = Vector3.zero;
        _instance.transform.localRotation = Quaternion.Euler(modelEuler);
        _instance.transform.localScale = Vector3.one * modelScale;

        SetLayerRecursively(_instance, _iconLayer);

        // Configure visuals only (no AI, no colliders, no NavMeshAgent)
        var boot = _instance.GetComponent<UnitBootstrapper>();
        if (!boot)
        {
            Debug.LogError("RenderModularArchetype: modularPrefab has no UnitBootstrapper.");
            return;
        }
        boot.SetupVisualOnly(archetype, teamId);

        // Frame the model in camera
        var bounds = ComputeRenderersBounds(_instance);
        FrameBounds(bounds);
    }

    private void BuildRig()
    {
        if (_rigRoot) return;

        _rigRoot = new GameObject("[IconRig]");
        _rigRoot.hideFlags = HideFlags.DontSave;
        _rigRoot.transform.position = GetIsolatedRigPosition();

        var camGO = new GameObject("IconCamera");
        camGO.transform.SetParent(_rigRoot.transform, false);
        _iconCam = camGO.AddComponent<Camera>();
        _iconCam.clearFlags = CameraClearFlags.SolidColor;
        _iconCam.backgroundColor = clearColor;
        _iconCam.fieldOfView = cameraFov;
        _iconCam.orthographic = false;
        _iconCam.allowHDR = true;
        _iconCam.allowMSAA = true;
        _iconCam.nearClipPlane = 0.05f;
        _iconCam.farClipPlane = 1000f;
        _iconCam.cullingMask = (1 << _iconLayer);

        _rt = new RenderTexture(textureWidth, textureHeight, 24, RenderTextureFormat.ARGB32);
        _rt.name = $"UnitIconRT_{GetInstanceID()}";
        _rt.Create();
        _iconCam.targetTexture = _rt;
        target.texture = _rt;
    }

    private void Cleanup()
    {
        if (_instance) DestroyImmediate(_instance);
        if (_iconCam)
        {
            _iconCam.targetTexture = null;
            DestroyImmediate(_iconCam.gameObject);
            _iconCam = null;
        }
        if (_rt)
        {
            _rt.Release();
            DestroyImmediate(_rt);
            _rt = null;
        }
        if (_rigRoot) DestroyImmediate(_rigRoot);
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform t in go.transform)
            SetLayerRecursively(t.gameObject, layer);
    }

    private static Bounds ComputeRenderersBounds(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true)
            .Where(r => r.enabled && !(r is ParticleSystemRenderer));

        bool init = false;
        Bounds bounds = new Bounds(root.transform.position, Vector3.zero);

        foreach (var r in renderers)
        {
            if (!init)
            {
                bounds = r.bounds;
                init = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }

        if (!init)
            bounds = new Bounds(root.transform.position, Vector3.one);

        return bounds;
    }

    private void FrameBounds(Bounds b)
    {
        var center = b.center;
        var radius = b.extents.magnitude * (1f + padding);

        // Force camera forward along -Z
        _iconCam.transform.rotation = Quaternion.identity;

        float fovRad = _iconCam.fieldOfView * Mathf.Deg2Rad;
        float dist = Mathf.Max(0.1f, radius / Mathf.Sin(fovRad * 0.5f));

        _iconCam.transform.position = center + Vector3.back * dist;
        _iconCam.transform.LookAt(center, Vector3.up);

        _iconCam.nearClipPlane = Mathf.Max(0.01f, dist - radius * 2f);
        _iconCam.farClipPlane = dist + radius * 2f;
    }

    public void RebuildRenderTexture(int w, int h)
    {
        textureWidth = Mathf.Max(16, w);
        textureHeight = Mathf.Max(16, h);
        if (_rt)
        {
            _iconCam.targetTexture = null;
            _rt.Release();
            DestroyImmediate(_rt);
        }
        _rt = new RenderTexture(textureWidth, textureHeight, 24, RenderTextureFormat.ARGB32);
        _rt.name = $"UnitIconRT_{GetInstanceID()}";
        _rt.Create();
        _iconCam.targetTexture = _rt;
        target.texture = _rt;
    }

    private Vector3 GetIsolatedRigPosition()
    {
        int id = Mathf.Abs(GetInstanceID());
        int ix = (id) & 63;
        int iz = (id >> 6) & 63;
        return RigBase + new Vector3(ix * RigStep, 0f, iz * RigStep);
    }
}
