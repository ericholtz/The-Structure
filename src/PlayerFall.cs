using UnityEngine;

public class PlayerFall : MonoBehaviour
{
    [Header("Pole rb")]
    public Transform rb;

    private void OnTriggerEnter()
    {
        FindObjectOfType<PlayerMovement>().canRide = true;
        FindObjectOfType<PlayerMovement>().pole = rb;
    }

    private void OnTriggerExit()
    {
        FindObjectOfType<PlayerMovement>().canRide = false;
    }
}
