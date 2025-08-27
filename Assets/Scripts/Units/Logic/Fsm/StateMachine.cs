using System.Threading;
using Cysharp.Threading.Tasks;

namespace Units.Logic.Fsm
{
	/// <summary>
	/// Lightweight state machine which owns a single IState at a time.
	/// </summary>
	public sealed class StateMachine
	{
		private IState _current;
		private CancellationToken _ct;

		public StateMachine(CancellationToken ct) => _ct = ct;

		public IState Current => _current;

		public async UniTask SetStateAsync(IState next)
		{
			if (_current == next) return;
			_current?.Exit();
			_current = next;
			if (_current != null)
				await _current.EnterAsync(_ct);
		}

		public void Tick() => _current?.Tick();
	}
}