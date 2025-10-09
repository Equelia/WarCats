using UnityEngine;
using TMPro;

public class FPSCounter : MonoBehaviour
{
    [SerializeField] private TMP_Text fpsText; // —юда перетащите ваш TMP_Text в инспекторе
    [SerializeField] private float updateInterval = 0.5f; //  ак часто обновл€ть значение (в секундах)

    private float timeLeft;
    private int frameCount;

    void Start()
    {
        if (fpsText == null)
        {
            Debug.LogError("FPS Counter: TMP_Text не назначен в инспекторе!");
            enabled = false;
            return;
        }

        timeLeft = updateInterval;
    }

    void Update()
    {
        frameCount++;
        timeLeft -= Time.deltaTime;

        if (timeLeft <= 0f)
        {
            float fps = frameCount / updateInterval;
            fpsText.text = Mathf.RoundToInt(fps).ToString() + " FPS";

            frameCount = 0;
            timeLeft = updateInterval;
        }
    }
}