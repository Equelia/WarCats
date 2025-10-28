using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TutorialButton3D : MonoBehaviour
{
    [SerializeField] private GameObject tutorialUI;

    public event Action OnClick;

    void OnMouseDown()
    {
        OnClick?.Invoke();

        if (tutorialUI != null)
        {
            tutorialUI.SetActive(true);
        }
    }
}