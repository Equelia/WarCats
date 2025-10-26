using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Modular appearance toggle: enables desired Body/Head/Weapon variants
/// and tints only the active Body via MaterialPropertyBlock.
/// Also selects the correct Animator: UnitVisuals animator for regular units,
/// and DogBones animator for dogs.
/// </summary>
[DisallowMultipleComponent]
public class UnitAppearanceToggle : MonoBehaviour
{
	public enum UnitKind { Pistol, Automata, Sniper, Rocket, Shield, Dog, Mine, CatGirl }
	public enum Team { Ally, Enemy }

	[Header("Variant Roots (parents that contain child variants)")]
	public Transform bodyRoot;
	public Transform headRoot;
	public Transform weaponRoot;

	[Header("Name keys (matched by Contains, case-insensitive)")]
	public string bodyKeyPrefix   = "Body_";
	public string headKeyPrefix   = "Head_";
	public string weaponKeyPrefix = "";

	[Header("Enemy head override (optional)")]
	public bool   useEnemyDefaultHead = true;
	public string enemyHeadKey        = "Head_Fox";

	[Header("Body tint")]
	public Color  allyBodyColor  = new Color(0.25f, 0.7f, 1f);
	public Color  enemyBodyColor = new Color(1f, 0.35f, 0.35f);
	public string colorPropertyName = "_BaseColor";

	[Header("Animators")]
	[Tooltip("Animator that lives on UnitVisuals (used by regular humanoids). If empty, will be auto-found on this GameObject.")]
	[SerializeField] private Animator unitVisualsAnimator;

	// ---- runtime ----
	private GameObject _activeBody;
	private GameObject _activeHead;
	private GameObject _activeWeapon;

	private readonly List<Renderer> _bodyRenderers = new();
	private MaterialPropertyBlock _mpb;
	private int _colorId;

	private void Awake()
	{
		// Auto-cache UnitVisuals animator if not assigned
		if (!unitVisualsAnimator) unitVisualsAnimator = GetComponent<Animator>();
	}

	/// <summary>Apply loadout by kind and team.</summary>
	public void ApplyLoadout(UnitKind kind, Team team)
	{
		// BODY
		_activeBody = ActivateByKey(bodyRoot, BuildBodyKey(kind));
		CacheBodyRenderers();

		// DOG: full model is in Body; disable Head/Weapon completely
		if (kind == UnitKind.Dog)
		{
			DeactivateAllChildren(headRoot);
			_activeHead = null;

			DeactivateAllChildren(weaponRoot);
			_activeWeapon = null;

			SetBodyColor(team == Team.Ally ? allyBodyColor : enemyBodyColor);

			// Select Dog animator on DogBones
			EnableCorrectAnimator(kind);
			return;
		}

		// Regular units: select Head/Weapon by key
		string headKey = (team == Team.Enemy && useEnemyDefaultHead && !string.IsNullOrEmpty(enemyHeadKey))
			? enemyHeadKey
			: BuildHeadKey(kind);
		_activeHead = ActivateByKey(headRoot, headKey);

		_activeWeapon = ActivateByKey(weaponRoot, BuildWeaponKey(kind));

		SetBodyColor(team == Team.Ally ? allyBodyColor : enemyBodyColor);

		// Select UnitVisuals animator for non-dog units
		EnableCorrectAnimator(kind);
	}

	public void SetBodyColor(Color color)
	{
		if (_colorId == 0) _colorId = Shader.PropertyToID(colorPropertyName);
		if (_mpb == null) _mpb = new MaterialPropertyBlock();

		foreach (var r in _bodyRenderers)
		{
			if (!r) continue;
			r.GetPropertyBlock(_mpb);
			_mpb.SetColor(_colorId, color);
			_mpb.SetColor(Shader.PropertyToID("_Color"), color); // Built-in fallback
			r.SetPropertyBlock(_mpb);
		}
	}

	public void SetBodyByKey(string key)
	{
		_activeBody = ActivateByKey(bodyRoot, key);
		CacheBodyRenderers();
	}

	public void SetHeadByKey(string key)   => _activeHead   = ActivateByKey(headRoot, key);
	public void SetWeaponByKey(string key) => _activeWeapon = ActivateByKey(weaponRoot, key);

	// ============== Internals ==============

	private string BuildBodyKey(UnitKind kind)   => bodyKeyPrefix   + NameKey(kind);
	private string BuildHeadKey(UnitKind kind)   => headKeyPrefix   + NameKey(kind);
	private string BuildWeaponKey(UnitKind kind) => weaponKeyPrefix + WeaponNameKey(kind);

	private string NameKey(UnitKind k)
	{
		switch (k)
		{
			case UnitKind.Pistol:   return "pistol";
			case UnitKind.Sniper:   return "sniper";
			case UnitKind.Rocket:   return "rocket";
			case UnitKind.Automata: return "automata";
			case UnitKind.CatGirl:  return "CatGirl";
			case UnitKind.Dog:      return "Dog";
			default:                return k.ToString();
		}
	}

	private string WeaponNameKey(UnitKind k)
	{
		switch (k)
		{
			case UnitKind.Pistol:   return "Pistol";
			case UnitKind.Sniper:   return "Sniper";
			case UnitKind.Rocket:   return "Rocket";
			case UnitKind.Automata: return "Automata";
			case UnitKind.CatGirl:  return "Pistol";
			case UnitKind.Dog:      return ""; // unused for dogs
			default:                return k.ToString();
		}
	}

	/// <summary>
	/// Activates a single child whose name contains 'key' (case-insensitive),
	/// deactivates others, and returns the activated object (or null).
	/// </summary>
	private GameObject ActivateByKey(Transform root, string key)
	{
		if (!root)
		{
			Debug.LogError("[UnitAppearanceToggle] Root is not assigned.", this);
			return null;
		}
		if (string.IsNullOrEmpty(key))
		{
			DeactivateAllChildren(root);
			return null;
		}

		GameObject activated = null;
		string keyLower = key.ToLowerInvariant();

		for (int i = 0; i < root.childCount; i++)
		{
			var child = root.GetChild(i).gameObject;
			bool match = child.name.ToLowerInvariant().Contains(keyLower);
			child.SetActive(match);
			if (match) activated = child;
		}

		if (activated == null)
			Debug.LogWarning($"[UnitAppearanceToggle] No child under '{root.name}' matched key '{key}'.", this);

		return activated;
	}

	private static void DeactivateAllChildren(Transform root)
	{
		if (!root) return;
		for (int i = 0; i < root.childCount; i++)
			root.GetChild(i).gameObject.SetActive(false);
	}

	private void CacheBodyRenderers()
	{
		_bodyRenderers.Clear();
		if (_activeBody == null) return;
		var tmp = _activeBody.GetComponentsInChildren<Renderer>(true);
		_bodyRenderers.AddRange(tmp);
	}

	/// <summary>
	/// Enables the proper Animator:
	/// - For regular units: enable UnitVisuals animator
	/// - For Dog: enable Animator found under the active body (DogBones), disable others.
	/// </summary>
	private void EnableCorrectAnimator(UnitKind kind)
	{
		// Disable all animators under UnitVisuals tree first
		var all = GetComponentsInChildren<Animator>(true);
		foreach (var a in all) a.enabled = false;

		if (kind == UnitKind.Dog)
		{
			// Find any animator under the active dog body (DogBones) and enable it
			if (_activeBody)
			{
				var dogAnimator = _activeBody.GetComponentInChildren<Animator>(true);
				if (dogAnimator) dogAnimator.enabled = true;
				else Debug.LogWarning("[UnitAppearanceToggle] Dog body has no Animator (DogBones)?", this);
			}
			else
			{
				Debug.LogWarning("[UnitAppearanceToggle] Active dog body is null.", this);
			}
		}
		else
		{
			// Enable UnitVisuals animator (humanoid rig)
			if (!unitVisualsAnimator) unitVisualsAnimator = GetComponent<Animator>();
			if (unitVisualsAnimator) unitVisualsAnimator.enabled = true;
			else Debug.LogWarning("[UnitAppearanceToggle] UnitVisuals Animator not found.", this);
		}
	}

#if UNITY_EDITOR
	[ContextMenu("Auto-Find Roots By Name")]
	private void AutoFindRoots()
	{
		if (!bodyRoot)   bodyRoot   = transform.Find("BodyRoot")   ?? transform.Find("Body")   ?? transform.Find("UnitVisuals/BodySocket");
		if (!headRoot)   headRoot   = transform.Find("HeadRoot")   ?? transform.Find("Head")   ?? transform.Find("UnitVisuals/HeadSocket");
		if (!weaponRoot) weaponRoot = transform.Find("WeaponRoot") ?? transform.Find("Weapon") ?? transform.Find("UnitVisuals/WeaponSocket");
		if (!unitVisualsAnimator) unitVisualsAnimator = GetComponent<Animator>();
		Debug.Log("[UnitAppearanceToggle] Auto-find complete.", this);
	}
#endif
}
