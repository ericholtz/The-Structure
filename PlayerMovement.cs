using System.Collections;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed;
    public float walkSpeed;
    public float sprintSpeed;

    public float groundDrag;

    [Header("Jumping")]
    public float jumpForce;
    public float jumpCooldown;
    public float airMultiplier;
    bool readyToJump;

    [Header("Crouching")]
    public float crouchSpeed;
    public GameObject camOffset;
    public bool crouching;

    [Header("Climbing")]
    public float ascendSpeed;
    public float descendSpeed;
    public bool climbing;
    public bool wasClimbing;
    public bool canClimb;
    public bool climbedWhileNotAtRope;
    private Vector3 climbingDirection;

    [Header("Riding")]
    public bool canRide;
    public bool riding;
    public Transform pole;

    [Header("Keybinds")]
    public KeyCode jumpKey = KeyCode.Space;
    public KeyCode sprintKey = KeyCode.LeftShift;
    public KeyCode[] crouchKey;
    public KeyCode mountKey = KeyCode.E;
    public KeyCode ascendKey = KeyCode.Space;
    public KeyCode descendKey = KeyCode.LeftShift;

    [Header("Ground Check")]
    public float legsHeight;
    public LayerMask whatIsGround;
    public bool grounded;

    [Header("Backwards")]
    public bool movingBackwards;
    public float backwardsSpeed;
    private float backwardsCrouchingMultiplier;

    [Header("Slope Handling")]
    public float maxSlopeAngle;
    private RaycastHit slopeHit;
    private bool exitingSlope;
    public bool onDaSlope;

    public Transform orientation;
    public Transform cam;

    float horizontalInput;
    float verticalInput;

    Vector3 moveDirection;

    public Rigidbody rb;
    public GameObject rbTop;
    public GameObject rbBottom;

    public MovementState moveState;

    public enum MovementState
    {
        walking,
        backwards,
        sprinting,
        crouching,
        crouchingBackwards,
        climbing,
        riding,
        air
    }

    // Start is called before the first frame update
    void Start()
    {
        rb.freezeRotation = true;

        readyToJump = true;

        backwardsCrouchingMultiplier = crouchSpeed / walkSpeed;

        climbing = false;
        canClimb = false;
        wasClimbing = false;

        riding = false;
        canRide = false;

        crouchKey = new KeyCode[2] { KeyCode.C, KeyCode.LeftControl };
        crouching = false;
    }

    private void Update()
    {
        grounded = Physics.Raycast(rbBottom.transform.position, Vector3.down, (legsHeight * 0.5f) + 0.2f, whatIsGround);

        MyInput();
        SpeedControl();
        StateHandler();
        DragHandler();
        BackwardsHandler();

        onDaSlope = OnSlope();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        MovePlayer();
    }

    private void MyInput()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");
        verticalInput = Input.GetAxisRaw("Vertical");

        // when to jump
        if (Input.GetKey(jumpKey) && readyToJump && grounded && !climbing)
        {
            readyToJump = false;

            Jump();

            StartCoroutine(WaitForLanding());
        }

        if (((Input.GetKeyDown(crouchKey[0]) && !Input.GetKey(crouchKey[1])) || (Input.GetKeyDown(crouchKey[1]) && !Input.GetKey(crouchKey[0]))) && !crouching)
        {
            rbTop.transform.localPosition -= new Vector3(0, 1, 0);
            crouching = true;
        }

        if (((Input.GetKeyUp(crouchKey[0]) && !Input.GetKey(crouchKey[1])) || (Input.GetKeyUp(crouchKey[1]) && !Input.GetKey(crouchKey[0]))) && crouching)
        {
            rbTop.transform.localPosition += new Vector3(0, 1, 0);
            crouching = false;
        }

        if (Input.GetKeyDown(mountKey) && canClimb) // mounting toggles climbing bool
        {
            climbedWhileNotAtRope = false;
            climbing = !climbing;
            if (climbing)
            {
                Mount();
            }
        }

        if (Input.GetKeyDown(mountKey) && !canClimb) // mounting toggles climbing bool
        {
            climbedWhileNotAtRope = true;
        }

        if (Input.GetKeyUp(mountKey) && !climbing && canClimb && !climbedWhileNotAtRope) // If pressed mount key and not climbing anymore, give boost
        {
            Boost();
            wasClimbing = false;
        }

        if (climbing && !grounded) // if climbing and not grounded, wasClimbing is true
        {
            wasClimbing = true;
        }

        if (climbing && grounded && wasClimbing)
        {
            climbing = !climbing;
            wasClimbing = false;
        }

        // Riding 
        if ((Input.GetKeyDown(mountKey) && canRide))
        {
            riding = true;
            rb.velocity = new Vector3(0, 0, 0);
            float newX = (pole.position.x - rb.position.x) / 2 + rb.position.x;
            float newZ = (pole.position.z - rb.position.z) / 2 + rb.position.z;
            rb.MovePosition(new Vector3(newX, rb.position.y, newZ));

            FindObjectOfType<MouseMovement>().xRotation = 0;
            Vector3 targetDirection = new Vector3(pole.position.x, rb.position.y, pole.position.z) - rb.position;
            FindObjectOfType<MouseMovement>().yRotation -= Vector3.SignedAngle(targetDirection, orientation.forward, Vector3.up);
            FindObjectOfType<MouseMovement>().Update(); // update mouse movement script before turning it off

            FindObjectOfType<MouseMovement>().enable = false; // disable mouse movement
        }

        if (riding && grounded)
        {
            riding = false;
            FindObjectOfType<MouseMovement>().enable = true; // re-enable mouse movement
        }
    }

    private void StateHandler()
    {
        // Mode - Climbing
        if (climbing)
        {
            moveState = MovementState.climbing;
            moveSpeed = 0;
        }

        // Mode - Sliding down pole
        else if (riding)
        {
            moveState = MovementState.riding;
            moveSpeed = 0;
        }

        // Mode - Crouching Backwards
        else if ((Input.GetKey(crouchKey[0]) && movingBackwards) || (Input.GetKey(crouchKey[1]) && movingBackwards))
        {
            moveState = MovementState.crouchingBackwards;
            moveSpeed = backwardsSpeed * backwardsCrouchingMultiplier;
        }

        // Mode - Crouching
        else if (Input.GetKey(crouchKey[0]) || Input.GetKey(crouchKey[1]))
        {
            moveState = MovementState.crouching;
            moveSpeed = crouchSpeed;
        }

        // Mode - Sprinting
        else if (grounded && Input.GetKey(sprintKey) && !movingBackwards)
        {
            moveState = MovementState.sprinting;
            moveSpeed = sprintSpeed;
        }

        // Mode - Walking Backwards
        else if (grounded && movingBackwards)
        {
            moveState = MovementState.backwards;
            moveSpeed = backwardsSpeed;
        }

        // Mode - Walking
        else if (grounded)
        {
            moveState = MovementState.walking;
            moveSpeed = walkSpeed;
        }

        // Mode - Air
        else
        {
            moveState = MovementState.air;
            moveSpeed = walkSpeed;
        }
    }

    private void DragHandler()
    {
        if (grounded)
            rb.drag = groundDrag;
        else if (!grounded && climbing)
            rb.drag = 10f;
        else
            rb.drag = 2f;
    }

    private void BackwardsHandler()
    {
        if (verticalInput < 0)
            movingBackwards = true;
        else
            movingBackwards = false;
    }

    private void MovePlayer()
    {
        // calculate movement direction
        moveDirection = orientation.forward * verticalInput + orientation.right * horizontalInput;

        // on slope
        if (OnSlope() && !exitingSlope)
        {
            if (Input.GetKey(crouchKey[0]) || Input.GetKey(crouchKey[1]))
                rb.AddForce(GetSlopeMoveDirection() * moveSpeed * 45f, ForceMode.Force);
            else
                    rb.AddForce(GetSlopeMoveDirection() * moveSpeed * 25f, ForceMode.Force);


            if (rb.velocity.y > 0)
                rb.AddForce(Vector3.down * 80f, ForceMode.Force);
        }

        // ascending climbing
        else if (climbing && Input.GetKey(ascendKey) && !Input.GetKey(descendKey))
        {
            rb.AddForce(10f * ascendSpeed * Vector3.up, ForceMode.Force);
        }

        // descending climbing
        else if (climbing && Input.GetKey(descendKey) && !Input.GetKey(ascendKey))
        {
            rb.AddForce(10f * descendSpeed * Vector3.down, ForceMode.Force);
        }

        // descending riding pole
        else if (riding)
        {
            rb.AddForce(10f * descendSpeed * Vector3.down, ForceMode.Force);
        }

        // on ground
        else if (grounded)
            rb.AddForce(10f * moveSpeed * moveDirection.normalized, ForceMode.Force);

        // in air
        else
            rb.AddForce(10f * moveSpeed * airMultiplier * moveDirection.normalized, ForceMode.Force);

        if (OnSlope() || climbing)
            rb.useGravity = false;
        else
            rb.useGravity = true;
    }

    private void SpeedControl()
    {
        // limiting speed on slope
        if (OnSlope() && !exitingSlope)
        {
            if (rb.velocity.magnitude > moveSpeed)
                rb.velocity = rb.velocity.normalized * moveSpeed;
        }

        // limiting speed when climbing
        else if (climbing)
        {
            if (rb.velocity.magnitude > ascendSpeed)
                rb.velocity = rb.velocity.normalized * ascendSpeed;
        }

        else if (riding)
        {
            if (rb.velocity.magnitude > (Mathf.Abs(descendSpeed)))
                rb.velocity = rb.velocity.normalized * 3f * Mathf.Abs(descendSpeed);
        }

        //limiting speed on ground or in air
        else
        {
            Vector3 flatVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z);

            // limit velocity if needed, for both on ground and in air
            if (flatVel.magnitude > moveSpeed)
            {
                Vector3 limitedVel = flatVel.normalized * moveSpeed;
                rb.velocity = new Vector3(limitedVel.x, rb.velocity.y, limitedVel.z);
            }
        }
    }

    private void Jump()
    {
        exitingSlope = true;

        // reset y velocity
        rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);

        rb.AddForce(transform.up * jumpForce, ForceMode.Impulse);
    }

    IEnumerator WaitForLanding()
    {
        yield return new WaitForSeconds(jumpCooldown);
        if (grounded)
        {
            ResetJump();
            yield break;
        }
        while (!grounded)
        {
            yield return null;
        }
        yield return new WaitForSeconds(jumpCooldown);
        ResetJump();
    }

    private void ResetJump()
    {
        readyToJump = true;
        exitingSlope = false;
    }

    private bool OnSlope()
    {
        if (Physics.Raycast(transform.position, Vector3.down, out slopeHit, legsHeight * 0.5f + 0.5f))
        {
            float angle = Vector3.Angle(Vector3.up, slopeHit.normal);
            return angle < maxSlopeAngle && angle != 0;
        }

        return false;
    }

    private Vector3 GetSlopeMoveDirection()
    {
        return Vector3.ProjectOnPlane(moveDirection, slopeHit.normal).normalized;
    }

    private void Boost()
    {
        rb.AddForce(orientation.forward.normalized * 30f, ForceMode.Impulse);
    }

    private void Mount()
    {
        rb.velocity = new Vector3(0, 0, 0);
        rb.AddForce(Vector3.up * 10f, ForceMode.Impulse);
    }

    public void ExitMount()
    {
        canClimb = false;
        if (climbing)
        {
            climbing = false;
            wasClimbing = false;
            Boost();
        }
    }
}
