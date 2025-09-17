using UnityEngine;
using Units.Data;

[CreateAssetMenu(menuName = "Units/Archetype", fileName = "UnitArchetype")]
public class UnitArchetype : ScriptableObject
{
	[Header("UI / Name")]
	public string displayName = "Unit";

	[Header("Visuals")]
	public UnitAppearanceToggle.UnitKind appearanceKind;
	public bool enemyUsesFixedHead = true;
	public string enemyHeadKey = "Head_Fox";

	[Header("Logic")]
	public LogicKind logicKind;
	public enum LogicKind { Pistol, Automata, Sniper, Rocket, CatGirl }

	[Header("Data (stats/behaviour)")]
	public UnitData unitData;

	[Header("Animator")]
	[Tooltip("Base controller with shared states (Idle, Walk, Shoot, Death, etc).")]
	public RuntimeAnimatorController baseController;

	[Tooltip("Optional override that replaces only some clips (e.g., Walk/Shoot for pistol).")]
	public AnimatorOverrideController overrideController;
}