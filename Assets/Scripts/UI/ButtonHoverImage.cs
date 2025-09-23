using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ButtonHoverImage : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
	[Header("Изображения кнопки")]
	public Sprite defaultSprite;
	public Sprite hoverSprite;

	private Image buttonImage;
	private Button button;

	private void Awake()
	{
		buttonImage = GetComponent<Image>();
		button = GetComponent<Button>();
	}

	private void OnEnable()
	{
		if (buttonImage != null && defaultSprite != null)
		{
			buttonImage.sprite = defaultSprite;
		}
	}

	public void OnPointerEnter(PointerEventData eventData)
	{
		if (buttonImage != null && hoverSprite != null && button.interactable)
		{
			buttonImage.sprite = hoverSprite;
		}
	}

	public void OnPointerExit(PointerEventData eventData)
	{
		if (buttonImage != null && defaultSprite != null)
		{
			buttonImage.sprite = defaultSprite;
		}
	}
}