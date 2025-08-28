using System.Threading;
using Cysharp.Threading.Tasks;

namespace Units.Logic.Fsm
{
	/// <summary>
	/// Minimal async-friendly state contract.
	/// </summary>
	public interface IState
	{
		/// <summary>Called when the state becomes active.</summary>
		UniTask EnterAsync(CancellationToken ct);

		/// <summary>Called every frame by the runner.</summary>
		void Tick();

		/// <summary>Called when the state is about to be replaced by another one.</summary>
		void Exit();
	}
}