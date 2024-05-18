using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class DelayedFlashlightFollow : MonoBehaviour
{
    public float sensX;
    public float sensY;

    public Transform orientation;

    public float xRotation;
    public float yRotation;

    public int flashlightMoveFrames;

    private float mouseX;
    private float mouseY;

    private LinkedList<float[]> inputs;

    public void Start()
    {
        // get mouse input
        mouseX = Input.GetAxisRaw("Mouse X") * Time.fixedDeltaTime * sensX;
        mouseY = Input.GetAxisRaw("Mouse Y") * Time.fixedDeltaTime * sensY;

        // initialize LinkedList
        inputs = new LinkedList<float[]>();
        for (int i = 0; i < flashlightMoveFrames; i++)
        {
            inputs.AddFirst(new float[2] { mouseX, mouseY });
        }
    }

    // Update is called once per frame
    public void Update()
    {
        // get mouse input
        mouseX = Input.GetAxisRaw("Mouse X") * Time.fixedDeltaTime * sensX;
        mouseY = Input.GetAxisRaw("Mouse Y") * Time.fixedDeltaTime * sensY;

        // add current mouse input to list
        inputs.AddFirst(new float[2] { mouseX, mouseY });

        // call UpdateFlashlight with last element of LinkedList, then remove that last element
        UpdateFlashlight(inputs.Last.Value[0], inputs.Last.Value[1]);
        inputs.RemoveLast();

        //StartCoroutine(UpdateFlashlight(mouseX, mouseY));
    }

    private void UpdateFlashlight(float mouseX, float mouseY)
    {
        yRotation += mouseX;
        if (Mathf.Abs(yRotation) > 360)
            yRotation = yRotation % 360; // restrict horizontal mouse movement to ±360°

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        // rotate flashlight
        transform.rotation = Quaternion.Euler(xRotation, yRotation, 0);
    }
}