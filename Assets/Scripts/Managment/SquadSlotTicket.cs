// SquadSlotTicket.cs (full)
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SquadSlotTicket : MonoBehaviour
{
	private IArmyEconomy _economy;
	private bool _slotReserved;
	private int _alive;
	private readonly List<GameObject> _members = new();

	public void Init(IArmyEconomy economy) => _economy = economy;

	/// <summary>Marks the ticket as already having a reserved slot (when reserved upstream).</summary>
	public void ForceMarkReserved() => _slotReserved = true;

	/// <summary>Try to reserve one army slot for the whole squad.</summary>
	public bool TryReserveOneSlot()
	{
		if (_slotReserved) return true;
		if (_economy == null) return false;
		_slotReserved = _economy.TryReserveSlot();
		return _slotReserved;
	}

	/// <summary>Register a unit under this ticket; releases the slot when the last member dies.</summary>
	public void RegisterMember(GameObject go, MonoBehaviour host)
	{
		if (!go) return;
		_members.Add(go);
		_alive++;
		host.StartCoroutine(Watch(go));
	}

	private IEnumerator Watch(GameObject go)
	{
		while (go != null) yield return null;
		_alive--;
		TryRelease();
	}

	private void TryRelease()
	{
		if (_slotReserved && _alive <= 0)
		{
			_slotReserved = false;
			_economy?.ReleaseSlot();
			// Destroy the ticket GameObject once the squad is gone to avoid reuse/zombie state.
			Destroy(gameObject);
		}
	}
}