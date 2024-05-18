using UnityEngine;
using static MenuCanvasHandler;

public class EnablePause : MonoBehaviour
{
    public GameObject player;
    public GameObject pauseMenuUI;
    public GameObject cam;
    public GameObject flashlight;
    public GameObject optionsMenuUI;
    public GameObject crosshair;
    public GameObject background;
    public GameObject[] buttons;

    public static bool inPauseMenuUI = false;
    public static bool paused = false;

    public GameObject[] layers;
    public UIState layer;

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (inPauseMenuUI)
            {
                Resume();
            }
            else if (!inPauseMenuUI && !paused)
            {
                Pause();
            }
            else
            {
                BackOneLayer();
            }
        }
    }

    public void Pause()
    {
        crosshair.SetActive(false);
        background.SetActive(true);
        pauseMenuUI.SetActive(true);
        inPauseMenuUI = true;
        paused = true;
        cam.GetComponent<MouseMovement>().enabled = false;
        flashlight.GetComponent<DelayedFlashlightFollow>().enabled = false;
        player.GetComponent<PlayerMovement>().enabled = false;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        Time.timeScale = 0f;
    }

    public void Resume()
    {
        // Delarge() buttons
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i].GetComponent<HoverEnlarge>().enlarged)
            {
                buttons[i].GetComponent<HoverEnlarge>().Delarge();
                break;
            }
        }

        // enable all other visuals
        crosshair.SetActive(true);
        background.SetActive(false);
        cam.GetComponent<MouseMovement>().enabled = true;
        flashlight.GetComponent<DelayedFlashlightFollow>().enabled = true;
        player.GetComponent<PlayerMovement>().enabled = true;
        inPauseMenuUI = false;
        paused = false;
        pauseMenuUI.SetActive(false);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        Time.timeScale = 1f;
    }

    public void ForwardOneLayer(UIState state)
    {
        switch (state)
        {
            case UIState.options:
                layer = state;
                layers[StateToIndex(UIState.main)].SetActive(false);
                inPauseMenuUI = false;
                break;
            case UIState.controlsOptions:
                layer = state;
                layers[StateToIndex(UIState.options)].SetActive(false);
                break;
            case UIState.visualOptions:
                layer = state;
                layers[StateToIndex(UIState.options)].SetActive(false);
                break;
            case UIState.audioOptions:
                layer = state;
                layers[StateToIndex(UIState.options)].SetActive(false);
                break;
            default:
                layer = state;
                break;
        }
    }

    private void BackOneLayer()
    {
        // disable current layer
        layers[StateToIndex(layer)].SetActive(false);

        // update layer
        switch (layer)
        {
            case UIState.options:
                layer = UIState.main;
                inPauseMenuUI = true;
                break;
            case UIState.controlsOptions:
                layer = UIState.options;
                break;
            case UIState.visualOptions:
                layer = UIState.options;
                break;
            case UIState.audioOptions:
                layer = UIState.options;
                break;
            default:
                layer = UIState.main;
                break;
        }

        // enable back layer
        layers[StateToIndex(layer)].SetActive(true);
    }

    private int StateToIndex(UIState state)
    {
        switch (state)
        {
            case UIState.main:
                return 0;
            case UIState.options:
                return 1;
            case UIState.controlsOptions:
                return 2;
            case UIState.visualOptions:
                return 3;
            case UIState.audioOptions:
                return 4;
            default:
                return -1;
        }
    }
}
