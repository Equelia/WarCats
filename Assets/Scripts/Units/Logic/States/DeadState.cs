using System.Threading;
using Cysharp.Threading.Tasks;
using Units.Logic.Core;
using Units.Logic.Fsm;

namespace Units.Logic.States
{
	/// <summary>
	/// Terminal state. Plays death anim and lets the object be destroyed.
	/// </summary>
	public sealed class DeadState : IState
	{
		private readonly UnitContext _ctx;

		public DeadState(UnitContext ctx) { _ctx = ctx; }

		public UniTask EnterAsync(CancellationToken ct) => UniTask.CompletedTask;
		public void Tick() { }
		public void Exit() { }
	}
}