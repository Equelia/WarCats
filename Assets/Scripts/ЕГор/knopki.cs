using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneChanger : MonoBehaviour
{
    // Метод для загрузки сцены по её индексу (build index)
    public void LoadScene(int sceneBuildIndex)
    {
        // Сохраняем номер сцены в PlayerPrefs


        // Загружаем сцену
        SceneManager.LoadScene(sceneBuildIndex);
    }}