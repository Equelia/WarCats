using UnityEngine;

/// <summary>
/// Controls an array of lamp GameObjects. 
/// Each lamp has a "lamp off" sprite on the root object, 
/// and a child GameObject with the "lamp on" sprite.
/// </summary>
public class LampRowUI : MonoBehaviour
{
	[Tooltip("Lamp root objects (with OFF sprite). Each should have a child with ON sprite.")]
	public GameObject[] lamps;

	/// <summary>
	/// Updates lamps with (occupied, max) values.
	/// - Root object (OFF sprite) always stays active.
	/// - Child object (ON sprite) is enabled if slot is occupied.
	/// - Lamps outside max capacity are hidden.
	/// </summary>
	public void SetState(int occupied, int max)
	{
		if (lamps == null) return;

		for (int i = 0; i < lamps.Length; i++)
		{
			var lampRoot = lamps[i];
			if (!lampRoot) continue;

			bool withinCapacity = i < max;
			lampRoot.SetActive(withinCapacity);

			if (!withinCapacity) continue;

			// child assumed index 0 = ON sprite
			if (lampRoot.transform.childCount > 0)
			{
				var onObj = lampRoot.transform.GetChild(0).gameObject;
				bool isOn = i < occupied;
				onObj.SetActive(isOn);
			}
		}
	}
}