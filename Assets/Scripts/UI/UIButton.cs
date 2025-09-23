using System;
using UnityEngine;
using UnityEngine.UI;

public class UIButton : MonoBehaviour
{
	private Button button;
	
	private void Awake()
	{
		button = GetComponent<Button>();
	}

	private void OnEnable()
	{
		button.onClick.AddListener(PlayButtonSound);
	}

	private void OnDisable()
	{
		button.onClick.RemoveListener(PlayButtonSound);

	}

	private void PlayButtonSound()
	{
		SoundManager.Instance.PlaySound("UIButton");
	}
}
