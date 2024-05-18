using UnityEngine;

public class FollowPlayer : MonoBehaviour
{
    public Transform player;
    public Vector3 offset;
    public Vector3 rotationOffset;

    void Start()
    {
        transform.Rotate(rotationOffset);
    }

    // Update is called once per frame
    void Update()
    {
        if (player.localScale.y < 1)
            transform.position = player.position + offset + new Vector3(0, -0.5f, 0);
        else
            transform.position = player.position + offset;
    }
}