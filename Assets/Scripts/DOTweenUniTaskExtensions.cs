using Cysharp.Threading.Tasks;
using DG.Tweening;

public static class DOTweenUniTaskExtensions
{
	public static UniTask ToUniTask(this Tween tween)
	{
		var completionSource = new UniTaskCompletionSource();

		tween.OnComplete(() => completionSource.TrySetResult());
		tween.OnKill(() => completionSource.TrySetCanceled());

		return completionSource.Task;
	}
}