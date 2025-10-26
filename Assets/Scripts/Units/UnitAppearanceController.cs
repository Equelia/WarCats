using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Модульная кастомизация без префабов: включает нужные дочерние варианты Body/Head/Weapon
/// и красит только активное Body через MaterialPropertyBlock.
/// </summary>
[DisallowMultipleComponent]
public class UnitAppearanceToggle : MonoBehaviour
{
    public enum UnitKind { Pistol, Automata, Sniper, Rocket, Shield, CatGirl }
    public enum Team     { Ally, Enemy }

    [Header("Variant Roots (parents that contain child variants)")]
    public Transform bodyRoot;   // содержит детей: Body_pistol, Body_sniper, ...
    public Transform headRoot;   // содержит детей: Head_pistol, Head_sniper, Head_Fox, ...
    public Transform weaponRoot; // содержит детей: Pistol, Sniper, Rocket, ...

    [Header("Name keys (matched by Contains, case-insensitive)")]
    [Tooltip("Префикс для детей тела (например Body_)")]
    public string bodyKeyPrefix   = "Body_";
    [Tooltip("Префикс для детей головы (например Head_)")]
    public string headKeyPrefix   = "Head_";
    [Tooltip("Префикс для детей оружия (можно пусто если имена 'Pistol', 'Sniper', ...)")]
    public string weaponKeyPrefix = "";

    [Header("Enemy head override (optional)")]
    [Tooltip("Если true — у врага всегда включаем enemyHeadKey (например, Head_Fox)")]
    public bool useEnemyDefaultHead = true;
    public string enemyHeadKey      = "Head_Fox";

    [Header("Body tint")]
    public Color allyBodyColor  = new Color(0.25f, 0.7f, 1f);
    public Color enemyBodyColor = new Color(1f, 0.35f, 0.35f);

    [Tooltip("URP Lit: _BaseColor, Built-in Standard: _Color")]
    public string colorPropertyName = "_BaseColor";

    // ---- runtime ----
    private GameObject _activeBody;
    private GameObject _activeHead;
    private GameObject _activeWeapon;

    private readonly List<Renderer> _bodyRenderers = new();
    private MaterialPropertyBlock _mpb;
    private int _colorId;

    // ====================== PUBLIC API ======================

    /// <summary>Применить лоадаут по типу и команде.</summary>
    public void ApplyLoadout(UnitKind kind, Team team)
    {
        // BODY
        _activeBody = ActivateByKey(bodyRoot, BuildBodyKey(kind));
        CacheBodyRenderers();

        // HEAD (для врага можно принудительно включить фиксированную голову)
        string headKey = (team == Team.Enemy && useEnemyDefaultHead && !string.IsNullOrEmpty(enemyHeadKey))
            ? enemyHeadKey
            : BuildHeadKey(kind);
        _activeHead = ActivateByKey(headRoot, headKey);

        // WEAPON
        _activeWeapon = ActivateByKey(weaponRoot, BuildWeaponKey(kind));

        // COLOR (только Body)
        SetBodyColor(team == Team.Ally ? allyBodyColor : enemyBodyColor);
    }

    /// <summary>Поставить цвет тела вручную (например, уникальная окраска).</summary>
    public void SetBodyColor(Color color)
    {
        if (_colorId == 0) _colorId = Shader.PropertyToID(colorPropertyName);
        if (_mpb == null) _mpb = new MaterialPropertyBlock();

        foreach (var r in _bodyRenderers)
        {
            if (!r) continue;
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(_colorId, color);
            // На всякий случай ещё и _Color поддержим (Built-in Standard)
            _mpb.SetColor(Shader.PropertyToID("_Color"), color);
            r.SetPropertyBlock(_mpb);
        }
    }

    /// <summary>Включить явного ребёнка тела по части имени (быстрая ручная подмена).</summary>
    public void SetBodyByKey(string key)
    {
        _activeBody = ActivateByKey(bodyRoot, key);
        CacheBodyRenderers();
    }

    /// <summary>Включить явного ребёнка головы по части имени.</summary>
    public void SetHeadByKey(string key) => _activeHead = ActivateByKey(headRoot, key);

    /// <summary>Включить явного ребёнка оружия по части имени.</summary>
    public void SetWeaponByKey(string key) => _activeWeapon = ActivateByKey(weaponRoot, key);

    // ====================== INTERNALS ======================

    private string BuildBodyKey(UnitKind kind)   => bodyKeyPrefix   + NameKey(kind);
    private string BuildHeadKey(UnitKind kind)   => headKeyPrefix   + NameKey(kind);
    private string BuildWeaponKey(UnitKind kind) => weaponKeyPrefix + WeaponNameKey(kind);

    // Подбираем ключи под ожидаемые имена детей в твоём проекте
    private string NameKey(UnitKind k)
    {
        switch (k)
        {
            case UnitKind.Pistol:   return "pistol";
            case UnitKind.Sniper:   return "sniper";
            case UnitKind.Rocket:   return "rocket";
            case UnitKind.Automata: return "automata";
            case UnitKind.CatGirl:  return "CatGirl"; // в ассетах с заглавной — оставим именно так
            default:                return k.ToString();
        }
    }

    private string WeaponNameKey(UnitKind k)
    {
        // У оружия обычно без префикса: "Pistol", "Sniper", "Rocket", "Automata"
        switch (k)
        {
            case UnitKind.Pistol:   return "Pistol";
            case UnitKind.Sniper:   return "Sniper";
            case UnitKind.Rocket:   return "Rocket";
            case UnitKind.Automata: return "Automata";
            case UnitKind.CatGirl:  return "Pistol";   // если для CatGirl тоже пистолет — подстрой при нужде
            default:                return k.ToString();
        }
    }

    /// <summary>
    /// Активирует единственного ребёнка чьё имя содержит key (регистронезависимо),
    /// все остальные выключает. Возвращает активированный объект (или null).
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
            Debug.LogWarning("[UnitAppearanceToggle] Empty key.", this);
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
        {
            Debug.LogWarning($"[UnitAppearanceToggle] No child under '{root.name}' matched key '{key}'.", this);
        }

        return activated;
    }

    private void CacheBodyRenderers()
    {
        _bodyRenderers.Clear();
        if (_activeBody == null) return;

        // Соберём все виды Renderer (MeshRenderer / SkinnedMeshRenderer и т.п.)
        var tmp = _activeBody.GetComponentsInChildren<Renderer>(true);
        _bodyRenderers.AddRange(tmp);
    }

#if UNITY_EDITOR
    [ContextMenu("Auto-Find Roots By Name")]
    private void AutoFindRoots()
    {
        if (!bodyRoot)   bodyRoot   = transform.Find("BodyRoot")   ?? transform.Find("Body");
        if (!headRoot)   headRoot   = transform.Find("HeadRoot")   ?? transform.Find("Head");
        if (!weaponRoot) weaponRoot = transform.Find("WeaponRoot") ?? transform.Find("Weapon");
        Debug.Log("[UnitAppearanceToggle] Auto-find complete.", this);
    }
#endif
}
