using UnityEngine;

public class FollowCanvas : MonoBehaviour
{
    private RectTransform rectTransform;
    public RectTransform canvasTransform;
    // Start is called before the first frame update
    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    // Update is called once per frame
    void Update()
    {
        rectTransform.sizeDelta = canvasTransform.sizeDelta;
    }
}
