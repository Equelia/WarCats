using UnityEngine;
using Zenject;

public class GameModeController : MonoBehaviour
{
	[Header("Teams")]
	[Tooltip("Команда игрока.")]
	public int playerTeamId = 0;
	[Tooltip("Команда противника.")]
	public int enemyTeamId = 1;

	[Header("UI / Flow (optional)")]
	public GameObject victoryPanel;
	public GameObject defeatPanel;
	public bool pauseOnGameOver = true;

	[Inject] ITeamBaseProvider _baseProvider; // если нужно

	private bool _ended;

	private void OnEnable()
	{
		UnitEvents.OnBaseDestroyed += OnBaseDestroyed;
	}

	private void OnDisable()
	{
		UnitEvents.OnBaseDestroyed -= OnBaseDestroyed;
	}

	private void OnBaseDestroyed(int destroyedTeamId)
	{
		if (_ended) return;
		_ended = true;

		int winner;
		if (destroyedTeamId == playerTeamId)
		{
			winner = enemyTeamId;
			ShowDefeat();
		}
		else if (destroyedTeamId == enemyTeamId)
		{
			winner = playerTeamId;
			ShowVictory();
		}
		else
		{
			// На случай других команд — считаем, что выигрывает противоположная
			winner = (destroyedTeamId == 0) ? 1 : 0;
			// Можно сделать более общий маппинг через _baseProvider
		}

		UnitEvents.RaiseGameOver(winner);
	}

	private void ShowVictory()
	{
		if (victoryPanel) victoryPanel.SetActive(true);
		if (pauseOnGameOver) Time.timeScale = 0f;
		Debug.Log("VICTORY");
	}

	private void ShowDefeat()
	{
		if (defeatPanel) defeatPanel.SetActive(true);
		if (pauseOnGameOver) Time.timeScale = 0f;
		Debug.Log("DEFEAT");
	}
}