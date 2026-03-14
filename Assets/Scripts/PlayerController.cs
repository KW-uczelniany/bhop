using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public CharacterController controller;
    public Transform cameraTransform;

    [Header("Movement Settings")]
    public float gravity = 20.0f;
    public float friction = 6.0f;
    public float moveSpeed = 7.0f;
    public float runAcceleration = 14.0f;
    public float runDeacceleration = 10.0f;
    public float airAcceleration = 15.0f;
    public float airDecceleration = 2.0f;
    public float sideStrafeAcceleration = 200.0f;
    public float sideStrafeSpeed = 1.0f;
    public float jumpSpeed = 8.0f;
    public bool holdJumpToBhop = true;

    [Header("UI")]
    public TMP_Text speedText;
    public TMP_Text maxSpeedText;
    public TMP_Text scoreText;
    public GameObject gameOverPanel;

    private Vector3 moveDirectionNorm = Vector3.zero;
    private Vector3 playerVelocity = Vector3.zero;
    private bool jumpQueued = false;
    
    // Scoring & Tracking
    private float maxSpeed = 0f;
    private float currentScore = 0f;
    private bool isGameOver = false;

    // Camera look
    public float lookSensitivity = 0.5f; // Decreased sensitivity, Mouse.delta provides larger values than GetAxisRaw
    private float cameraPitch = 0f;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
        
        if (cameraTransform == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null) cameraTransform = cam.transform;
        }
    }

    [Header("Audio")]
    public AudioSource speedMusicSource;
    public float minSpeedForAudio = 5f;
    public float maxSpeedForAudio = 30f; // Adjusted for new higher speeds

    void Update()
    {
        if (isGameOver)
            return;

        // Look
        float mouseX = 0f;
        float mouseY = 0f;

        if (Mouse.current != null)
        {
            // Read delta directly from the Input System mouse device
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            mouseX = mouseDelta.x * lookSensitivity;
            mouseY = mouseDelta.y * lookSensitivity;
        }

        transform.Rotate(0, mouseX, 0);
        cameraPitch -= mouseY;
        cameraPitch = Mathf.Clamp(cameraPitch, -90f, 90f);
        if (cameraTransform != null)
        {
            cameraTransform.localEulerAngles = new Vector3(cameraPitch, 0, 0);
        }

        QueueJump();
        
        if (controller.isGrounded)
        {
            GroundMove();
        }
        else
        {
            AirMove();
            // Score purely based on air-time and speed (Bhop reward)
            Vector3 hSpeed = new Vector3(playerVelocity.x, 0, playerVelocity.z);
            currentScore += hSpeed.magnitude * Time.deltaTime * 10f; 
        }

        // Apply movement
        controller.Move(playerVelocity * Time.deltaTime);

        // Update UI & Audio
        Vector3 horizSpeed = new Vector3(playerVelocity.x, 0, playerVelocity.z);
        float currentSpeed = horizSpeed.magnitude;

        if (currentSpeed > maxSpeed)
        {
            maxSpeed = currentSpeed;
        }

        if (speedText != null)
            speedText.text = "Speed: " + currentSpeed.ToString("F1");
            
        if (maxSpeedText != null)
            maxSpeedText.text = "Max Speed: " + maxSpeed.ToString("F1");
            
        if (scoreText != null)
            scoreText.text = "Score: " + Mathf.FloorToInt(currentScore).ToString();

        if (speedMusicSource != null && speedMusicSource.isPlaying)
        {
            // Calculate a normalized speed value between 0 and 1
            float t = Mathf.InverseLerp(minSpeedForAudio, maxSpeedForAudio, currentSpeed);
            
            // Map speed to volume (e.g., quiet to loud)
            speedMusicSource.volume = Mathf.Lerp(0.1f, 1.0f, t);
        }
    }

    public void GameOver()
    {
        isGameOver = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }
        
        if (speedMusicSource != null)
        {
            speedMusicSource.Stop();
        }
    }

    private void GetInputAxes(out float forward, out float right)
    {
        forward = 0f;
        right = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed) forward += 1f;
            if (Keyboard.current.sKey.isPressed) forward -= 1f;
            if (Keyboard.current.dKey.isPressed) right += 1f;
            if (Keyboard.current.aKey.isPressed) right -= 1f;
        }
    }

    private void SetMovementDir()
    {
        GetInputAxes(out float forward, out float right);

        Vector3 moveInput = new Vector3(right, 0, forward);
        moveDirectionNorm = transform.TransformDirection(moveInput).normalized;
        
        if (moveInput.sqrMagnitude < 0.01f)
            moveDirectionNorm = Vector3.zero;
    }

    private void QueueJump()
    {
        bool jumpPressed = Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
        bool jumpDown = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        bool jumpUp = Keyboard.current != null && Keyboard.current.spaceKey.wasReleasedThisFrame;

        if (holdJumpToBhop)
        {
            jumpQueued = jumpPressed;
        }
        else
        {
            if (jumpDown && !jumpQueued)
            {
                jumpQueued = true;
            }
            if (jumpUp)
            {
                jumpQueued = false;
            }
        }
    }

    private void GroundMove()
    {
        ApplyFriction(1.0f);
        SetMovementDir();

        var wishDir = moveDirectionNorm;
        var wishSpeed = (wishDir != Vector3.zero) ? moveSpeed : 0f;

        Accelerate(wishDir, wishSpeed, runAcceleration);

        playerVelocity.y = -gravity * Time.deltaTime;

        if (jumpQueued)
        {
            playerVelocity.y = jumpSpeed;
            jumpQueued = false;
        }
    }

    private void AirMove()
    {
        SetMovementDir();

        var wishDir = moveDirectionNorm;
        var wishSpeed = (wishDir != Vector3.zero) ? moveSpeed : 0f;

        float wishSpd = wishSpeed;
        float accel;

        if (Vector3.Dot(playerVelocity, wishDir) < 0)
        {
            accel = airDecceleration;
        }
        else
        {
            accel = airAcceleration;
        }

        // Strafe optimization (Source style)
        GetInputAxes(out float forwardInput, out float rightInput);
        if (forwardInput == 0 && rightInput != 0)
        {
            if (wishSpd > sideStrafeSpeed)
            {
                wishSpd = sideStrafeSpeed;
            }
            accel = sideStrafeAcceleration;
        }

        Accelerate(wishDir, wishSpd, accel);

        playerVelocity.y -= gravity * Time.deltaTime;
    }

    private void Accelerate(Vector3 wishDir, float wishSpeed, float accel)
    {
        float currentSpeed = Vector3.Dot(playerVelocity, wishDir);
        float addSpeed = wishSpeed - currentSpeed;

        if (addSpeed <= 0)
            return;

        float accelSpeed = accel * Time.deltaTime * wishSpeed;

        if (accelSpeed > addSpeed)
            accelSpeed = addSpeed;

        playerVelocity.x += accelSpeed * wishDir.x;
        playerVelocity.z += accelSpeed * wishDir.z;
    }

    private void ApplyFriction(float t)
    {
        Vector3 vec = playerVelocity;
        vec.y = 0.0f;
        float speed = vec.magnitude;
        float drop = 0.0f;

        if (controller.isGrounded)
        {
            float control = speed < runDeacceleration ? runDeacceleration : speed;
            drop = control * friction * Time.deltaTime * t;
        }

        float newSpeed = speed - drop;
        if (newSpeed < 0)
            newSpeed = 0;

        if (speed > 0)
        {
            newSpeed /= speed;
        }

        playerVelocity.x *= newSpeed;
        playerVelocity.z *= newSpeed;
    }
}
