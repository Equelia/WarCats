using UnityEngine;

namespace Helpers	
{
	/// <summary>
	/// Mark an object as cover. Units will search for nearest Cover component or objects with tag "Cover".
	/// </summary>
	public class Cover : MonoBehaviour
	{
		/// <summary>
		/// Returns nearest Transform of a cover object within radius, or null if none found.
		/// </summary>
		public static Transform FindNearestCoverTransform(Vector3 fromPosition, float radius)
		{
			Collider[] hits = Physics.OverlapSphere(fromPosition, radius);
			Transform best = null;
			float bestDist = float.MaxValue;

			foreach (var c in hits)
			{
				// check component or tag
				if (c.GetComponent<Cover>() != null || c.CompareTag("Cover"))
				{
					float d = Vector3.Distance(fromPosition, c.transform.position);
					if (d < bestDist)
					{
						bestDist = d;
						best = c.transform;
					}
				}
			}

			return best;
		}
	}
}