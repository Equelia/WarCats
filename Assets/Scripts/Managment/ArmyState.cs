using System;

[System.Serializable]
public class ArmyState
{
	public int level = 1;            // army level
	public int credits = 0;
	public int points = 0;

	public int maxSlots = 8;
	public int occupiedSlots = 0;

	// XP-based player level (progress bar)
	public int xpLevel = 1;          
	public int currentXp = 0;
	public int xpToLevel = 100;

	// Progress for army upgrade
	public int armyUpgradeProgress = 0; 
	public int armyUpgradeStepsRequired = 5;
	
	public float XpFill => xpToLevel <= 0 ? 0f : (float)currentXp / xpToLevel;

}
