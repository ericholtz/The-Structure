using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class EnterOptionsMenu : MonoBehaviour
{
    public GameObject canvas;

    public GameObject optionsMenuUI;
    private bool inOptionsUI = false;

    public void EnterOptions()
    {
        GetComponent<HoverEnlarge>().Delarge();
        if (SceneManager.GetActiveScene().buildIndex == 0)
            canvas.GetComponent<MenuCanvasHandler>().ForwardOneLayer(MenuCanvasHandler.UIState.options);
        else
            canvas.GetComponent<EnablePause>().ForwardOneLayer(MenuCanvasHandler.UIState.options);
        optionsMenuUI.SetActive(true);
        inOptionsUI = true;
    }
}