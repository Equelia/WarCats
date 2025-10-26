using UnityEngine;

namespace Units.Data
{
	[CreateAssetMenu(menuName = "Units/ShieldData", fileName = "ShieldData")]
	public class ShieldData : UnitData
	{
		[Header("Shieldman-specific")]
		[Tooltip("Сколько юнитов спавнится на слот для уровней 1..3.")]
		public int[] spawnCountPerSlot = new int[3] { 1, 1, 2 };

		public override int GetSpawnCountForLevel(int level)
		{
			int idx = Mathf.Clamp(level, 1, 3) - 1;
			if (spawnCountPerSlot == null || spawnCountPerSlot.Length < 3)
				return 1;
			return Mathf.Max(1, spawnCountPerSlot[idx]);
		}
	}
}