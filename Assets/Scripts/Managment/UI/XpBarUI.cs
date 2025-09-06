using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// XP bar with smooth animated fill (ease-in / ease-out).
/// </summary>
public class XpBarUI : MonoBehaviour
{
	[SerializeField] private Image fill;

	[Header("Animation")]
	[SerializeField] private float smoothTime = 0.3f; // чем больше, тем более "тягучая" анимация
	[SerializeField] private float maxSpeed = 2f;     // ограничение скорости

	private float _targetFill;
	private float _currentVelocity; // используется SmoothDamp

	private void Awake()
	{
		if (fill)
		{
			_targetFill = fill.fillAmount;
		}
	}

	/// <summary>
	/// Sets XP target [0..1]. The bar animates smoothly toward it.
	/// </summary>
	public void Set01(float v)
	{
		_targetFill = Mathf.Clamp01(v);
	}

	private void Update()
	{
		if (!fill) return;

		float newValue = Mathf.SmoothDamp(
			fill.fillAmount,
			_targetFill,
			ref _currentVelocity,
			smoothTime,
			maxSpeed,
			Time.deltaTime
		);

		fill.fillAmount = newValue;
	}
}