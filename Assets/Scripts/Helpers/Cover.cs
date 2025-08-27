using UnityEngine;
using Units.Logic; 

namespace Helpers
{
	/// <summary>
	/// Mark an object as cover. Units will search for nearest Cover component or objects with tag "Cover".
	/// This component also holds a simple 'protection' value (0..1) which is added to unit vulnerability while in cover.
	/// Additionally it tracks the current occupant (UnitLogic) so multiple units don't occupy same cover.
	/// </summary>
	[DisallowMultipleComponent]
	public class Cover : MonoBehaviour
	{
		[Tooltip("Protection added to unit vulnerability while standing behind this cover (0..1).")]
		[Range(0f, 1f)]
		public float protection = 0.5f;

		// current occupant of this cover (null when free)
		[HideInInspector]
		public UnitLogic occupant = null;

		/// <summary>
		/// Is the cover currently occupied by some unit?
		/// </summary>
		public bool IsOccupied => occupant != null;

		/// <summary>
		/// Attempt to occupy this cover. Returns true if successful.
		/// Note: UnitLogic.OccupyCover already sets occupant directly — this method is available if you prefer
		/// to request occupation from Cover side.
		/// </summary>
		public bool TryOccupy(UnitLogic u)
		{
			if (occupant == null)
			{
				occupant = u;
				return true;
			}

			// already occupied
			return occupant == u;
		}

		/// <summary>
		/// Release occupant if it matches provided unit.
		/// </summary>
		public void Release(UnitLogic u)
		{
			if (occupant == u)
				occupant = null;
		}
	}
}