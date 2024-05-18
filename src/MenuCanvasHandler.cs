using UnityEngine;

public class MenuCanvasHandler : MonoBehaviour
{
    public GameObject[] layers;
    public UIState layer;

    public enum UIState
    {
        main,
        options,
        controlsOptions,
        visualOptions,
        audioOptions
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            BackOneLayer();
        }
    }

    private void BackOneLayer()
    {
        // disable current layer
        layers[StateToIndex(layer)].SetActive(false);

        // update layer
        switch (layer)
        {
            case UIState.main:
                break;
            case UIState.options:
                layer = UIState.main;
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

    public void ForwardOneLayer(UIState state)
    {
        switch (state)
        {
            case UIState.options:
                layer = state;
                layers[StateToIndex(UIState.main)].SetActive(false);
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
