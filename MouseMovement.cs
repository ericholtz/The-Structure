using UnityEngine;

public class MouseMovement : MonoBehaviour
{
    public float sensX;
    public float sensY;

    public bool enable;

    public Transform orientation;

    public float xRotation;
    public float yRotation;

    // Start is called before the first frame update
    void Start()
    {
        enable = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // Update is called once per frame
    public void Update()
    {
        if (enable)
        {
            // get mouse input
            float mouseX = Input.GetAxisRaw("Mouse X") * Time.fixedDeltaTime * sensX;
            float mouseY = Input.GetAxisRaw("Mouse Y") * Time.fixedDeltaTime * sensY;

            yRotation += mouseX;
            if (Mathf.Abs(yRotation) > 360)
                yRotation = yRotation % 360; // restrict horizontal mouse movement to ±360°

            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -90f, 90f);

            // rotate cam and orientation
            transform.rotation = Quaternion.Euler(xRotation, yRotation, 0);
            orientation.rotation = Quaternion.Euler(0, yRotation, 0);
        }
    }
}
