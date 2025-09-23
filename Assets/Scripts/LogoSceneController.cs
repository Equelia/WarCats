using UnityEngine;
using UnityEngine.SceneManagement;

public class LogoSceneController : MonoBehaviour
{
	[SerializeField] private float logoDuration = 3.5f; 

	private void Start()
	{
		Invoke(nameof(LoadNextScene), logoDuration);
	}

	private void LoadNextScene()
	{
		SceneManager.LoadScene("MainMenu");
	}
}