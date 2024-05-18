using UnityEngine;

public class PlayerClimb : MonoBehaviour
{
    private void OnTriggerEnter()
    {
        FindObjectOfType<PlayerMovement>().canClimb = true;
    }

    private void OnTriggerExit()
    {
        FindObjectOfType<PlayerMovement>().ExitMount();
    }
}
