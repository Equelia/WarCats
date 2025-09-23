using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class OpenURLButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
	[SerializeField] private string url = "https://t.me/your_telegram_link";
	
	private Button button;
	private TMP_Text text;
	
	private FontStyles originalStyle;

	
	private void Awake()
	{
		button = GetComponent<Button>();
		text = GetComponent<TMP_Text>();
		originalStyle = text.fontStyle;
	}

	private void OnEnable()
	{
		button.onClick.AddListener(OpenLink);
	}

	private void OnDisable()
	{
		button.onClick.RemoveListener(OpenLink);

	}
	
	public void OnPointerEnter(PointerEventData eventData)
	{
		text.fontStyle |= FontStyles.Italic;
	}

	public void OnPointerExit(PointerEventData eventData)
	{
		text.fontStyle = originalStyle;
	}

	public void OpenLink()
	{
		Application.OpenURL(url);
	}
}