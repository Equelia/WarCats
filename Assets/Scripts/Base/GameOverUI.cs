using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class GameOverUI : MonoBehaviour
{
  [Header("Panels (hook your objects)")]
    [SerializeField] private GameObject finalRoot;   // объект 'Final' (не обязательно)
    [SerializeField] private GameObject losePanel;   // Final/LOSE
    [SerializeField] private GameObject winPanel;    // Final/Win

    [Header("Teams / Flow")]
    [Tooltip("ID команды игрока (для определения победы при OnGameOver).")]
    [SerializeField] private int playerTeamId = 0;
    [Tooltip("Ставить Time.timeScale=0 при показе результата.")]
    [SerializeField] private bool pauseOnShow = true;

    [Header("Scenes")]
    [Tooltip("Имя сцены главного меню/лобби.")]
    [SerializeField] private string menuSceneName = "Lobby";
    [Tooltip("Использовать кастомный загрузчик LoadingScene.LoadScene(string) если он есть в проекте.")]
    [SerializeField] private bool useCustomLoaderIfAvailable = true;

    private bool _shown;

    private void Awake()
    {
        // Стартово всё скрыто
        SetActiveSafe(losePanel, false);
        SetActiveSafe(winPanel,  false);
        SetActiveSafe(finalRoot, false);
    }

    private void OnEnable()
    {
        UnitEvents.OnGameOver += OnGameOver;
    }

    private void OnDisable()
    {
        UnitEvents.OnGameOver -= OnGameOver;
    }

    // Слушаем событие окончания игры: приходит teamId победителя
    private void OnGameOver(int winnerTeamId)
    {
        if (_shown) return;
        bool playerWon = (winnerTeamId == playerTeamId);
        Show(playerWon);
    }

    /// <summary>Показать один из экранов вручную (если нужно вызвать из кода).</summary>
    public void Show(bool victory)
    {
        if (_shown) return;
        _shown = true;

        SetActiveSafe(finalRoot, true);
        SetActiveSafe(losePanel, !victory);
        SetActiveSafe(winPanel,  victory);

        if (pauseOnShow) Time.timeScale = 0f;
    }

    // ===================== BUTTON HANDLERS =====================

    /// <summary>Перезапустить текущую сцену (кнопки: LOSE/Again, Win/Restart).</summary>
    public void OnRestartPressed()
    {
        UnpauseIfNeeded();
        var scene = SceneManager.GetActiveScene().name;
        SceneManager.LoadScene(scene);
    }

    /// <summary>Выйти в меню/лобби (кнопки: LOSE/Home, Win/Menu).</summary>
    public void OnMenuPressed()
    {
        if (string.IsNullOrEmpty(menuSceneName))
        {
            Debug.LogWarning("FinalUI: menuSceneName не задан.");
            return;
        }

        UnpauseIfNeeded();

        if (useCustomLoaderIfAvailable && TryUseCustomLoader(menuSceneName))
            return;

        SceneManager.LoadScene(menuSceneName);
    }

    /// <summary>Выход из игры (кнопки: LOSE/Leave, Win/Leave).</summary>
    public void OnQuitPressed()
    {
        UnpauseIfNeeded();

#if UNITY_EDITOR
        // В редакторе просто останавливаем Play Mode
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ===================== Helpers =====================

    private void UnpauseIfNeeded()
    {
        if (pauseOnShow) Time.timeScale = 1f;
    }

    private static void SetActiveSafe(GameObject go, bool v)
    {
        if (go && go.activeSelf != v) go.SetActive(v);
    }

    /// <summary>Пытаемся найти статический класс LoadingScene с методом LoadScene(string).</summary>
    private bool TryUseCustomLoader(string sceneName)
    {
        try
        {
            var loaderType = Type.GetType("LoadingScene");
            var m = loaderType?.GetMethod("LoadScene", new[] { typeof(string) });
            if (m != null)
            {
                m.Invoke(null, new object[] { sceneName });
                return true;
            }
        }
        catch { /* ignore and fallback */ }
        return false;
    }
}