using UnityEngine;

public class HoverEnlarge : MonoBehaviour
{
    public GameObject button;
    public bool enlarged;

    public void Enlarge()
    {
        button.GetComponent<RectTransform>().localScale *= 1.1f;
        enlarged = true;
    }
    public void Delarge()
    {
        button.GetComponent<RectTransform>().localScale /= 1.1f;
        enlarged = false;
    }
}
