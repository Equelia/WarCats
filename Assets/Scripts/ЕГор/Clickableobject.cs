using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ClickableObject : MonoBehaviour
{
    [SerializeField] private int sceneIndex;
    private bool isWork;
    public int SceneIndex  => sceneIndex;
    public event Action OnClick;
    void OnMouseDown()
    {
        OnClick?.Invoke();
    }
}