using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple wrapper around a fill Image to represent XP progress.
/// </summary>
public class XpBarUI : MonoBehaviour
{
	[SerializeField] private Image fill;

	public void Set01(float v)
	{
		if (!fill) return;
		fill.fillAmount = Mathf.Clamp01(v);
	}
}