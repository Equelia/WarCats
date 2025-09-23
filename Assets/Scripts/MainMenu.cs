using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
	[Header("Panels")]
	[SerializeField] private GameObject authorsPanel;
	
	public string sceneToLoad = "TownLevel";

	public void OnPlayButton( )
	{
		SceneManager.LoadScene(sceneToLoad);
	}

	public void OnAuthorsButton()
	{
		if (authorsPanel != null)
		{
			authorsPanel.SetActive(true);
		}
	}

	public void OnLeaveButton()
	{
		Application.Quit();

#if UNITY_EDITOR
		UnityEditor.EditorApplication.isPlaying = false;
#endif
	}
}