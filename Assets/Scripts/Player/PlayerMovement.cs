using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private bool smoothMovement = true;
    [SerializeField] private float smoothTime = 0.1f;
    
    [Header("Sprite Settings")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private bool autoFindSpriteRenderer = true;
    
    private Rigidbody2D rb;
    private Vector2 movement;
    private Vector2 smoothedMovement;
    private Vector2 movementVelocity;
    private bool facingRight = true;
    
    private void Start()
    {
        // Get Rigidbody2D component
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            Debug.LogError("PlayerMovement: No Rigidbody2D found! Please add a Rigidbody2D component.");
        }
        else
        {
            // Configure Rigidbody2D for top-down movement
            rb.gravityScale = 0f;
            rb.drag = 10f; // High drag for snappy movement
            rb.freezeRotation = true;
        }
        
        // Auto-find SpriteRenderer if not assigned and auto-find is enabled
        if (autoFindSpriteRenderer && spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                Debug.LogWarning("PlayerMovement: No SpriteRenderer found! Sprite flipping will not work.");
            }
        }
    }
    
    private void Update()
    {
        HandleInput();
        HandleSpriteFlipping();
    }
    
    private void FixedUpdate()
    {
        HandleMovement();
    }
    
    private void HandleInput()
    {
        // Get input from both WASD and arrow keys
        float horizontal = Input.GetAxisRaw("Horizontal"); // A/D and Left/Right arrows
        float vertical = Input.GetAxisRaw("Vertical");     // W/S and Up/Down arrows
        
        // Create movement vector
        movement = new Vector2(horizontal, vertical);
        
        // Normalize diagonal movement to prevent faster movement
        if (movement.magnitude > 1f)
        {
            movement = movement.normalized;
        }
    }
    
    private void HandleMovement()
    {
        if (rb == null) return;
        
        Vector2 targetMovement;
        
        if (smoothMovement)
        {
            // Smooth movement using SmoothDamp
            smoothedMovement = Vector2.SmoothDamp(
                smoothedMovement, 
                movement, 
                ref movementVelocity, 
                smoothTime
            );
            targetMovement = smoothedMovement * moveSpeed;
        }
        else
        {
            // Direct movement (more responsive, less smooth)
            targetMovement = movement * moveSpeed;
        }
        
        // Use MovePosition for collision-aware movement
        Vector2 newPosition = rb.position + targetMovement * Time.fixedDeltaTime;
        rb.MovePosition(newPosition);
    }
    
    private void HandleSpriteFlipping()
    {
        if (spriteRenderer == null) return;
        
        // Only flip when actually moving horizontally
        if (Mathf.Abs(movement.x) > 0.1f)
        {
            if (movement.x > 0 && !facingRight)
            {
                // Moving right and currently facing left
                FlipSprite();
            }
            else if (movement.x < 0 && facingRight)
            {
                // Moving left and currently facing right
                FlipSprite();
            }
        }
    }
    
    private void FlipSprite()
    {
        facingRight = !facingRight;
        spriteRenderer.flipX = !facingRight;
    }
    
    // Public methods for external access
    public Vector2 GetMovementDirection()
    {
        return movement;
    }
    
    public bool IsMoving()
    {
        return movement.magnitude > 0.1f;
    }
    
    public void SetMoveSpeed(float newSpeed)
    {
        moveSpeed = newSpeed;
    }
    
    public float GetMoveSpeed()
    {
        return moveSpeed;
    }
    
    // Optional: Add a method to temporarily disable movement (useful for cutscenes, menus, etc.)
    public void SetMovementEnabled(bool enabled)
    {
        this.enabled = enabled;
        if (!enabled && rb != null)
        {
            rb.velocity = Vector2.zero;
        }
    }
    
    // Get current position (useful for other systems)
    public Vector2 GetPosition()
    {
        return rb.position;
    }
    
    // Get current facing direction
    public bool IsFacingRight()
    {
        return facingRight;
    }
    
    // Manually set facing direction (useful for other systems)
    public void SetFacingDirection(bool shouldFaceRight)
    {
        if (facingRight != shouldFaceRight)
        {
            FlipSprite();
        }
    }
}