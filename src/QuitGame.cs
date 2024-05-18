using UnityEngine;
using UnityEditor;

public class QuitGame : MonoBehaviour
{
    public void Quit()
    {
#if UNITY_EDITOR
EditorApplication.isPlaying = false;
#else
Application.Quit();
#endif
    }
}
