using System;

[Serializable]
public class ArmyState
{
	public int level = 1;

	public int credits = 0;
	public int points = 0;

	public int maxSlots = 8; // total capacity for concurrent summoned units
	public int occupiedSlots = 0;  // currently used slots

	public int currentXp = 0;
	public int xpToLevel = 100; // simple linear by default

	/// <summary>
	/// Progress ratio 0..1 for XP bar.
	/// </summary>
	public float XpFill => xpToLevel <= 0 ? 0f : (float)currentXp / xpToLevel;
}