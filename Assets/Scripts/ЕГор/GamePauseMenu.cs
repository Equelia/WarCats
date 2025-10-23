using UnityEngine;
using UnityEngine.SceneManagement;

public class GamePauseMenu : MonoBehaviour
{
private void OnEnable()
    {
        Time.timeScale = 0;
    }
private void OnDisable()
    {
        Time.timeScale = 1;
    }
    private void OnDestroy()
    {
        Time.timeScale = 1;
    }
    private void Awake()
    {
        Time.timeScale = 1;
    }
    public void MainMenu()
    {
        SceneManager.LoadScene(0);
    }
}
