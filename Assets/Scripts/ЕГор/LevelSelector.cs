using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LevelSelecter : MonoBehaviour
{
    [SerializeField] private ClickableObject[] clickableObjects;
    [SerializeField] private Button startButton;
    private int currentIndex;
    private void Awake()
    {
        startButton.onClick.AddListener(HandleButtonClick);
    }

    private void HandleButtonClick()
    {
        SceneManager.LoadScene(currentIndex);
    }

    private void OnEnable() 
    {
        for (int i = 0; i < clickableObjects.Length; i++)
        {
      
            int index = i; // важно замкнуть переменную!
            clickableObjects[i].OnClick += () => HandleClick(index);

        }
       
    }
    private void OnDisable()
    {
        for (int i = 0; i < clickableObjects.Length; i++)
        {

            int index = i; // важно замкнуть переменную!
            clickableObjects[i].OnClick -= () => HandleClick(index);

        }
        startButton.gameObject.SetActive(false);
        for (int i = 0; i < clickableObjects.Length; i++)
        {

            clickableObjects[i].gameObject.GetComponent<MeshRenderer>().material.color = Color.white;

        } 
    }
    private void HandleClick(int index)
    {
        currentIndex = clickableObjects[index].SceneIndex;
        startButton.gameObject.SetActive(true);
        for (int i = 0; i < clickableObjects.Length; i++)
        {

            clickableObjects[i].gameObject.GetComponent<MeshRenderer>().material.color = Color.white;

        }
        clickableObjects[index].gameObject.GetComponent<MeshRenderer>().material.color = Color.red; 
    }
   
}
