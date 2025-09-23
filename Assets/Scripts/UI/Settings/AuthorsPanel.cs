using UnityEngine;
using UnityEngine.UI;

public class AuthorsPanel : MonoBehaviour
{
    [SerializeField] private Button returnButton;
    

    private void OnEnable()
    {
        returnButton.onClick.AddListener(ReturnToMainMenu);
    }

    private void OnDisable()
    {
        returnButton.onClick.RemoveListener(ReturnToMainMenu);

    }

    private void ReturnToMainMenu()
    {
        gameObject.SetActive(false);
    }
}
