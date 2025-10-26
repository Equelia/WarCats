using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PlacementOverlayUI : MonoBehaviour
{
	[SerializeField] private Image dimImage;
	[SerializeField, Range(0f, 1f)] private float targetAlpha = 0.6f;
	[SerializeField] private float fadeTime = 0.15f;

	private Coroutine _fadeCo;

	private void Reset()
	{
		dimImage = GetComponent<Image>();
		if (dimImage)
		{
			dimImage.raycastTarget = false; // клики не блокируем
			dimImage.color = new Color(0f, 0f, 0f, 0f);
		}
	}

	public void Show()
	{
		if (!dimImage) return;
		gameObject.SetActive(true);
		StartFadeTo(targetAlpha);
	}

	public void Hide()
	{
		if (!dimImage) return;
		StartFadeTo(0f, () => gameObject.SetActive(false));
	}

	private void StartFadeTo(float a, System.Action onEnd = null)
	{
		if (_fadeCo != null) StopCoroutine(_fadeCo);
		_fadeCo = StartCoroutine(FadeTo(a, onEnd));
	}

	private IEnumerator FadeTo(float a, System.Action onEnd)
	{
		var c = dimImage.color;
		float start = c.a;
		float t = 0f;

		while (t < fadeTime)
		{
			t += Time.unscaledDeltaTime;
			float k = Mathf.Clamp01(t / fadeTime);
			c.a = Mathf.Lerp(start, a, k);
			dimImage.color = c;
			yield return null;
		}

		c.a = a;
		dimImage.color = c;
		onEnd?.Invoke();
	}
}