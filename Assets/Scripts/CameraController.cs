using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Drag-to-pan with smooth motion + smooth zoom (mouse wheel / pinch) for PC & Mobile,
/// with tilt and height driven by zoom level.
/// - Pan: LMB drag on PC, one-finger drag on mobile
///   * Smooth damping while dragging
///   * Optional inertial glide after releasing the drag
/// - Zoom: Mouse wheel on PC (smooth), two-finger pinch on mobile (smooth)
/// - Tilt: Pitch increases when zoomed out, decreases when zoomed in
/// - Height: World Y lowers when zoomed in, raises when zoomed out
/// - Optional XZ bounds
/// </summary>
public class CameraController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float dragSpeed = 1.5f;
    [Tooltip("Extra multiplier for touch drag (for high-DPI screens).")]
    [SerializeField] private float touchDragMultiplier = 1.0f;
    [SerializeField] private bool invertX = false;
    [SerializeField] private bool invertY = false;

    [Header("Pan Smoothing")]
    [SerializeField] private bool smoothPan = true;
    [Tooltip("Time (seconds) for pan to reach ~63% of the remaining distance.")]
    [SerializeField] private float panSmoothTime = 0.12f;
    [Tooltip("Max pan speed (units/sec) for the damping. Use Infinity for no clamp.")]
    [SerializeField] private float panMaxSpeed = Mathf.Infinity;

    [Header("Pan Inertia")]
    [SerializeField] private bool panInertia = true;
    [Tooltip("How quickly the inertial velocity decays (higher = stops sooner).")]
    [SerializeField] private float inertiaDamping = 6f;
    [Tooltip("Minimum inertial speed before it snaps to a stop.")]
    [SerializeField] private float inertiaMinSpeed = 0.05f;

    [Header("Bounds (XZ only)")]
    [SerializeField] private bool useBounds = false;
    [SerializeField] private Vector2 minXZ = new Vector2(-200f, -200f);
    [SerializeField] private Vector2 maxXZ = new Vector2(200f, 200f);

    [Header("Zoom")]
    [SerializeField] private bool enableZoom = true;
    [Tooltip("Mouse wheel sensitivity (FOV degrees per wheel step, or ortho size units).")]
    [SerializeField] private float wheelSensitivity = 8f;
    [Tooltip("Pinch sensitivity (FOV degrees per pixel of pinch delta, or ortho size units).")]
    [SerializeField] private float pinchSensitivity = 0.03f;
    [SerializeField] private float minFOV = 25f;
    [SerializeField] private float maxFOV = 70f;
    [SerializeField] private float minOrthoSize = 5f;
    [SerializeField] private float maxOrthoSize = 25f;

    [Header("Zoom Smoothing")]
    [SerializeField] private bool smoothZoom = true;
    [SerializeField] private float zoomSmoothTime = 0.12f;
    [SerializeField] private float zoomMaxSpeed = Mathf.Infinity;

    [Header("Tilt vs Zoom")]
    [SerializeField] private float minPitch = 50f; // when zoomed in
    [SerializeField] private float maxPitch = 80f; // when zoomed out
    [SerializeField] private float tiltLerpSpeed = 10f;

    [Header("Height vs Zoom (world Y)")]
    [SerializeField] private bool zoomControlsHeight = true;
    [SerializeField] private float minHeight = 12f; // lowest at zoom-in
    [SerializeField] private float maxHeight = 40f; // highest at zoom-out
    [SerializeField] private float heightLerpSpeed = 10f;

    // Internals
    private Camera _cam;
    private bool _dragging;
    private Vector3 _prevMouse;
    private int _activeFingerId = -1; // -1 means none
    private Vector2 _prevTouchPos;

    private bool _pinching;
    private float _prevPinchDist;

    // Smooth zoom state
    private float _targetFOV;
    private float _targetOrthoSize;
    private float _zoomVel; // SmoothDamp velocity for zoom

    // Smooth pan state (XZ only)
    private Vector2 _targetXZ;               // desired XZ (updated by drag/inertia)
    private float _panVelX, _panVelZ;        // SmoothDamp velocities for X and Z

    // Inertia
    private Vector3 _inertiaVel; // world units/sec (XZ used)

    // Heuristic to ignore sudden bogus deltas
    private const float MaxDeltaSqrForMouse = 300f * 300f;  // pixels^2
    private const float MaxDeltaSqrForTouch = 300f * 300f;  // pixels^2

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        if (_cam == null) _cam = Camera.main;

        // Clamp and seed zoom targets
        if (_cam.orthographic)
        {
            _cam.orthographicSize = Mathf.Clamp(_cam.orthographicSize, minOrthoSize, maxOrthoSize);
            _targetOrthoSize = _cam.orthographicSize;
        }
        else
        {
            _cam.fieldOfView = Mathf.Clamp(_cam.fieldOfView, minFOV, maxFOV);
            _targetFOV = _cam.fieldOfView;
        }

        // Seed pan target from current position
        _targetXZ = new Vector2(transform.position.x, transform.position.z);

        // Anchor maxHeight to current Y so camera returns here when zoomed out (if default)
        if (zoomControlsHeight && Mathf.Approximately(maxHeight, 40f))
            maxHeight = transform.position.y;
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        // Input
        if (Input.touchSupported && Input.touchCount > 0)
        {
            HandleTouch();
        }
        else
        {
            HandleMouse();
            HandleMouseWheelZoom();
        }

        // Inertial glide (applies to target XZ)
        ApplyPanInertia(dt);

        // Smooth toward target XZ
        UpdatePanSmoothing(dt);

        // Zoom smoothing and dependent effects
        if (enableZoom)
        {
            UpdateZoomSmoothing(dt);
            UpdateTiltFromZoom(dt);
            if (zoomControlsHeight) UpdateHeightFromZoom(dt);
        }
    }

    #region Mouse
    private void HandleMouse()
    {
        Vector3 mouse = Input.mousePosition;
        bool overUI = IsPointerOverUI();

        if (Input.GetMouseButtonDown(0))
        {
            _dragging = !overUI;
            _prevMouse = mouse;
        }

        if (Input.GetMouseButtonUp(0))
        {
            _dragging = false;
        }

        if (!_dragging)
        {
            _prevMouse = mouse;
            return;
        }

        Vector2 delta = (Vector2)(mouse - _prevMouse);

        if (delta.sqrMagnitude > MaxDeltaSqrForMouse)
            delta = Vector2.zero;

        ApplyPanFromScreenDelta(delta, 1f);
        _prevMouse = mouse;
    }

    private void HandleMouseWheelZoom()
    {
        if (!enableZoom) return;
        if (IsPointerOverUI()) return;

        float wheel = Input.mouseScrollDelta.y; // positive -> zoom in
        if (Mathf.Abs(wheel) > Mathf.Epsilon)
        {
            ApplyZoomDelta(wheel, isTouch: false); // write to target; smoothing later
        }
    }
    #endregion

    #region Touch
    private void HandleTouch()
    {
        // Pinch takes priority
        if (enableZoom && Input.touchCount >= 2)
        {
            HandlePinchZoom();
            return; // skip pan while pinching
        }
        else if (_pinching && Input.touchCount < 2)
        {
            _pinching = false;
        }

        // One-finger drag pan
        if (_activeFingerId == -1)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);
                if (t.phase == TouchPhase.Began && !IsPointerOverUI(t.fingerId))
                {
                    _activeFingerId = t.fingerId;
                    _prevTouchPos = t.position;
                    _dragging = true;
                    break;
                }
            }
        }

        if (_activeFingerId != -1)
        {
            bool fingerStillExists = false;

            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);
                if (t.fingerId != _activeFingerId) continue;

                fingerStillExists = true;

                if (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary)
                {
                    Vector2 delta = t.position - _prevTouchPos;

                    if (delta.sqrMagnitude > MaxDeltaSqrForTouch)
                        delta = Vector2.zero;

                    ApplyPanFromScreenDelta(delta, touchDragMultiplier);
                    _prevTouchPos = t.position;
                }
                else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                {
                    _dragging = false;
                    _activeFingerId = -1;
                }

                break;
            }

            if (!fingerStillExists)
            {
                _dragging = false;
                _activeFingerId = -1;
            }
        }
        else
        {
            _dragging = false;
        }
    }

    private void HandlePinchZoom()
    {
        Touch t0 = Input.GetTouch(0);
        Touch t1 = Input.GetTouch(1);
        if (IsPointerOverUI(t0.fingerId) || IsPointerOverUI(t1.fingerId))
        {
            _pinching = false;
            return;
        }

        // Cancel pan + inertia while pinching
        _dragging = false;
        _activeFingerId = -1;
        _inertiaVel = Vector3.zero;

        float currentDist = Vector2.Distance(t0.position, t1.position);

        if (!_pinching)
        {
            _pinching = true;
            _prevPinchDist = currentDist;
            return;
        }

        float delta = currentDist - _prevPinchDist; // >0 apart, <0 together
        _prevPinchDist = currentDist;

        ApplyZoomDelta(delta, isTouch: true);
    }
    #endregion

    #region Core Pan / Zoom / Tilt / Height
    /// <summary>
    /// Converts a screen-space drag delta (in pixels) into a world-space XZ pan target update.
    /// Uses smooth damping to approach the target; also captures inertia velocity.
    /// </summary>
    private void ApplyPanFromScreenDelta(Vector2 screenDelta, float extraMultiplier)
    {
        if (invertX) screenDelta.x = -screenDelta.x;
        if (invertY) screenDelta.y = -screenDelta.y;

        // Flatten camera vectors onto XZ plane
        Vector3 right = _cam.transform.right; right.y = 0f; right.Normalize();
        Vector3 fwd   = _cam.transform.forward; fwd.y   = 0f; fwd.Normalize();

        // World "speed" (units/sec) induced by the drag
        Vector3 worldSpeed = (right * -screenDelta.x + fwd * -screenDelta.y) * (dragSpeed * extraMultiplier);

        // Update the target XZ using dt-integrated step
        float dt = Time.deltaTime;
        Vector3 step = worldSpeed * dt;
        _targetXZ += new Vector2(step.x, step.z);

        // Bounds on target (XZ)
        if (useBounds)
        {
            _targetXZ.x = Mathf.Clamp(_targetXZ.x, minXZ.x, maxXZ.x);
            _targetXZ.y = Mathf.Clamp(_targetXZ.y, minXZ.y, maxXZ.y);
        }

        // Capture inertia velocity (XZ). Blend to reduce jitter.
        _inertiaVel.x = Mathf.Lerp(_inertiaVel.x, worldSpeed.x, 0.5f);
        _inertiaVel.z = Mathf.Lerp(_inertiaVel.z, worldSpeed.z, 0.5f);
    }

    /// <summary>
    /// Applies inertial glide to the pan target when not dragging.
    /// </summary>
    private void ApplyPanInertia(float dt)
    {
        if (_dragging || !panInertia) return;

        Vector2 velXZ = new Vector2(_inertiaVel.x, _inertiaVel.z);
        if (velXZ.sqrMagnitude <= inertiaMinSpeed * inertiaMinSpeed)
        {
            _inertiaVel = Vector3.zero;
            return;
        }

        // Integrate position from current inertial velocity
        _targetXZ += velXZ * dt;

        // Bounds + kill velocity component that hits the boundary
        if (useBounds)
        {
            if (_targetXZ.x <= minXZ.x && _inertiaVel.x < 0f) _inertiaVel.x = 0f;
            if (_targetXZ.x >= maxXZ.x && _inertiaVel.x > 0f) _inertiaVel.x = 0f;
            if (_targetXZ.y <= minXZ.y && _inertiaVel.z < 0f) _inertiaVel.z = 0f;
            if (_targetXZ.y >= maxXZ.y && _inertiaVel.z > 0f) _inertiaVel.z = 0f;

            _targetXZ.x = Mathf.Clamp(_targetXZ.x, minXZ.x, maxXZ.x);
            _targetXZ.y = Mathf.Clamp(_targetXZ.y, minXZ.y, maxXZ.y);
        }

        // Exponential decay of velocity
        float decay = Mathf.Exp(-inertiaDamping * dt);
        _inertiaVel.x *= decay;
        _inertiaVel.z *= decay;
    }

    /// <summary>
    /// Smoothly moves current XZ toward target XZ (Y is handled separately by height/zoom).
    /// </summary>
    private void UpdatePanSmoothing(float dt)
    {
        Vector3 p = transform.position;

        if (!smoothPan)
        {
            // Snap to target XZ
            transform.position = new Vector3(_targetXZ.x, p.y, _targetXZ.y);
            return;
        }

        float newX = Mathf.SmoothDamp(p.x, _targetXZ.x, ref _panVelX, panSmoothTime, panMaxSpeed, dt);
        float newZ = Mathf.SmoothDamp(p.z, _targetXZ.y, ref _panVelZ, panSmoothTime, panMaxSpeed, dt);

        transform.position = new Vector3(newX, p.y, newZ);
    }

    /// <summary>
    /// Applies a zoom delta by modifying the target (smoothed later).
    /// For mouse: delta is wheel steps; for touch: delta is pinch pixel change.
    /// </summary>
    private void ApplyZoomDelta(float delta, bool isTouch)
    {
        if (_cam.orthographic)
        {
            float sizeDelta = (isTouch ? -delta * pinchSensitivity : -delta * (wheelSensitivity * 0.2f));
            if (_targetOrthoSize <= 0f) _targetOrthoSize = _cam.orthographicSize;
            _targetOrthoSize = Mathf.Clamp(_targetOrthoSize + sizeDelta, minOrthoSize, maxOrthoSize);
        }
        else
        {
            float fovDelta = (isTouch ? -delta * pinchSensitivity : -delta * wheelSensitivity);
            if (_targetFOV <= 0f) _targetFOV = _cam.fieldOfView;
            _targetFOV = Mathf.Clamp(_targetFOV + fovDelta, minFOV, maxFOV);
        }
    }

    /// <summary>
    /// Smoothly moves current FOV/OrthoSize towards target using SmoothDamp.
    /// </summary>
    private void UpdateZoomSmoothing(float dt)
    {
        if (!smoothZoom)
        {
            if (_cam.orthographic && _targetOrthoSize > 0f) _cam.orthographicSize = _targetOrthoSize;
            else if (!_cam.orthographic && _targetFOV > 0f) _cam.fieldOfView = _targetFOV;
            return;
        }

        if (_cam.orthographic)
        {
            if (_targetOrthoSize <= 0f) _targetOrthoSize = _cam.orthographicSize;
            float current = _cam.orthographicSize;
            current = Mathf.SmoothDamp(current, _targetOrthoSize, ref _zoomVel, zoomSmoothTime, zoomMaxSpeed, dt);
            _cam.orthographicSize = Mathf.Clamp(current, minOrthoSize, maxOrthoSize);
        }
        else
        {
            if (_targetFOV <= 0f) _targetFOV = _cam.fieldOfView;
            float current = _cam.fieldOfView;
            current = Mathf.SmoothDamp(current, _targetFOV, ref _zoomVel, zoomSmoothTime, zoomMaxSpeed, dt);
            _cam.fieldOfView = Mathf.Clamp(current, minFOV, maxFOV);
        }
    }

    /// <summary>0 -> zoomed out, 1 -> zoomed in.</summary>
    private float GetZoomT()
    {
        if (_cam.orthographic)
            return Mathf.InverseLerp(maxOrthoSize, minOrthoSize, _cam.orthographicSize);
        else
            return Mathf.InverseLerp(maxFOV, minFOV, _cam.fieldOfView);
    }

    /// <summary>Smoothly adjusts camera pitch (X rotation) based on zoom.</summary>
    private void UpdateTiltFromZoom(float dt)
    {
        float t = GetZoomT();
        float targetPitch = Mathf.Lerp(maxPitch, minPitch, t); // more top-down when zoomed out
        Vector3 e = transform.eulerAngles;
        float smooth = 1f - Mathf.Exp(-tiltLerpSpeed * dt);
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(targetPitch, e.y, 0f), smooth);
    }

    /// <summary>Smoothly adjusts world Y height based on zoom (down when zoomed in, up when zoomed out).</summary>
    private void UpdateHeightFromZoom(float dt)
    {
        float t = GetZoomT();
        float targetY = Mathf.Lerp(maxHeight, minHeight, t); // lowest at zoom-in
        Vector3 p = transform.position;
        float smooth = 1f - Mathf.Exp(-heightLerpSpeed * dt);
        p.y = Mathf.Lerp(p.y, targetY, smooth);
        transform.position = p;
    }
    #endregion

    #region UI Helpers
    private static bool IsPointerOverUI()
    {
        if (EventSystem.current == null) return false;
        return EventSystem.current.IsPointerOverGameObject();
    }

    private static bool IsPointerOverUI(int fingerId)
    {
        if (EventSystem.current == null) return false;
        return EventSystem.current.IsPointerOverGameObject(fingerId);
    }
    #endregion
}
