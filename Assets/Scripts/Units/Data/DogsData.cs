using UnityEngine;

namespace Units.Data
{
	[CreateAssetMenu(menuName = "Units/DogsData (Pack Melee)", fileName = "DogsData")]
	public class DogsData : UnitData
	{
		[Header("Pack size per level (1..3)")]
		[Tooltip("How many dogs are spawned per one summon at level 1..3")]
		public int[] packCountPerLevel = new int[3] { 3, 4, 5 };

		[Header("Melee VFX/SFX")]
		[Tooltip("Played when a bite connects")]
		public AudioClip biteSfx;
		[Tooltip("Spawned at hit point when a bite connects")]
		public GameObject biteVfx;
		[Tooltip("Optional: small push applied to target on bite (impulse)")]
		public float biteNudgeForce = 0f;

		/// <summary>Returns how many instances to spawn for the given level (1..3).</summary>
		public override int GetSpawnCountForLevel(int level)
		{
			int idx = Mathf.Clamp(level, 1, 3) - 1;
			if (packCountPerLevel == null || packCountPerLevel.Length < 3)
				return Mathf.Clamp(level, 1, 3); // fallback: 1,2,3
			return Mathf.Max(1, packCountPerLevel[idx]);
		}
	}
}