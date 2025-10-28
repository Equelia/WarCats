using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class TutorialManager : MonoBehaviour
{
    [SerializeField] private List<GameObject> slides;
    [SerializeField] private Button closeButton;

    private int currentSlide = 0;

    void OnEnable()
    {
        // Подписываемся на событие (на случай, если отписались)
        closeButton.onClick.AddListener(OnCloseButton);

        // Сбрасываем обучение
        currentSlide = 0;
        ShowSlide(currentSlide);
    }

    void OnDisable()
    {
        // Отписываемся, чтобы избежать утечек
        closeButton.onClick.RemoveListener(OnCloseButton);
    }

    void OnCloseButton()
    {
        currentSlide++;
        if (currentSlide < slides.Count)
        {
            ShowSlide(currentSlide);
        }
        else
        {
            gameObject.SetActive(false); // скрываем обучение
        }
    }

    void ShowSlide(int index)
    {
        for (int i = 0; i < slides.Count; i++)
        {
            slides[i].SetActive(i == index);
        }
    }
}