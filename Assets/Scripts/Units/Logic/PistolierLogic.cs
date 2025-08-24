using System.Collections;
using Helpers;
using Units.Data;
using Units.Logic;
using UnityEngine;
using UnityEngine.AI;

namespace Units.Logic
{
	/// <summary>
	/// Ranged pistol unit that uses UnitLogic state machine and NavMeshAgent.
	/// </summary>
	[RequireComponent(typeof(NavMeshAgent))]
	public class PistolierLogic : UnitLogic
	{
		[Header("FX")]
		[Tooltip("Assign the muzzle flash GameObject (child on the unit prefab). Keep it inactive in prefab.")]
		public GameObject muzzleFlashInstance;
		
		private ReusableEffect _muzzleEffect;

		private PistolierData pistolierData => unitData as PistolierData;

		protected override void Awake()
		{
			base.Awake();
			
			if (pistolierData == null)
				Debug.LogWarning($"{name}: assigned UnitData is not PistolierData (or is null).", this);
			
			// locate ReusableEffect on the assigned instance (or add it)
			if (muzzleFlashInstance != null)
			{
				_muzzleEffect = muzzleFlashInstance.GetComponent<ReusableEffect>();
				if (_muzzleEffect == null)
				{
					// If user assigned just a ParticleSystem object, add ReusableEffect automatically
					_muzzleEffect = muzzleFlashInstance.AddComponent<ReusableEffect>();
				}

				// ensure instance is initially inactive so it doesn't play until requested
				muzzleFlashInstance.SetActive(false);
			}
		}

		protected override IEnumerator PerformAttackCoroutine(Transform target)
		{
			// optional small delay to simulate fire time
			yield return new WaitForSeconds(0.3f);

			if (_muzzleEffect != null)
			{
				_muzzleEffect.Play();
			}

			if (target == null) yield break;

			float roll = Random.value;
			if (roll <= stats.accuracy)
			{
				var unit = target.GetComponent<UnitLogic>();
				if (unit != null)
				{
					unit.ReceiveDamage(stats.damage);
				}
			}
		}

		public int GetSpawnCountForLevel()
		{
			if (pistolierData == null) return 1;
			return pistolierData.GetSpawnCountForLevel(level);
		}
	}
}