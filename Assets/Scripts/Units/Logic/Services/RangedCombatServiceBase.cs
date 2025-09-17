using Units.Logic.Core;
using Units.Logic.Services;

namespace Units.Logic
{
	public abstract class RangedCombatServiceBase : CombatService
	{
		protected readonly RangedUnitController Owner;
		protected RangedCombatServiceBase(RangedUnitController owner) { Owner = owner; }

		protected override void OnBeforeAttackFx(UnitContext ctx)
		{
			Owner.PlayMuzzleFx();
		}
	}
}