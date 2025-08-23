using UnityEngine;

namespace Units.Data
{
	[CreateAssetMenu(menuName = "Units/PistolierData", fileName = "PistolierData")]
	public class PistolierData : UnitData
	{
		[Header("Pistolier-specific")]
		[Tooltip("How many actual unit instances are created per one 'spawn slot' at each level (index 0 = level1, index1 = level2, ...).")]
		public int[] spawnCountPerSlot = new int[3] { 1, 2, 3 };

		/// <summary>
		/// Returns how many instances this data will spawn for given level (1..3).
		/// </summary>
		public int GetSpawnCountForLevel(int level)
		{
			int idx = Mathf.Clamp(level, 1, 3) - 1;
			if (spawnCountPerSlot == null || spawnCountPerSlot.Length < 3)
			{
				// fallback to 1,2,3
				return idx + 1;
			}

			return Mathf.Max(1, spawnCountPerSlot[idx]);
		}
	}
}