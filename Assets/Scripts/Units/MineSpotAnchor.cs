using UnityEngine;

[DisallowMultipleComponent]
public class MineSpotAnchor : MonoBehaviour
{
	private MineSpot _spot;

	public void Bind(MineSpot spot)
	{
		_spot = spot;
		_spot.RefreshOccupation();
	}

	private void OnDestroy()
	{
		if (_spot)
		{
			_spot.RefreshOccupation();
		}
	}
}