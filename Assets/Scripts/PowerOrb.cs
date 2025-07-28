using UnityEngine;
using System.Collections;

public class PowerOrb : MonoBehaviour
{
    [Header("Orb Configuration")]
    [SerializeField] private int powerValue = 1;
    [SerializeField] private float lifetime = 30f; // Orb disappears after 30 seconds if not collected
    
    [Header("Movement Settings")]
    [SerializeField] private float attractionRange = 3f;
    [SerializeField] private float attractionSpeed = 8f;
    [SerializeField] private float popForce = 5f;
    [SerializeField] private float popDuration = 0.5f;
    [SerializeField] private float popDeceleration = 8f;
    
    [Header("Visual Effects")]
    [SerializeField] private float bobSpeed = 2f;
    [SerializeField] private float bobHeight = 0.2f;
    [SerializeField] private float rotationSpeed = 90f;
    [SerializeField] private Color orbColor = Color.cyan;
    [SerializeField] private AnimationCurve scaleOverTime = AnimationCurve.EaseInOut(0, 1, 1, 0.8f);
    
    [Header("Audio")]
    [SerializeField] private AudioClip collectSound;
    [SerializeField] private float volume = 0.7f;
    
    // Components
    private SpriteRenderer spriteRenderer;
    private Collider2D orbCollider;
    private Rigidbody2D rb;
    private AudioSource audioSource;
    
    // State
    private bool isBeingAttracted = false;
    private bool isCollected = false;
    private bool isPopping = true; // Start in popping state
    private Transform player;
    private Vector3 initialPosition;
    private float spawnTime;
    private Vector3 popVelocity;
    
    // Events
    public System.Action<PowerOrb> OnOrbCollected;
    
    private void Awake()
    {
        InitializeComponents();
        spawnTime = Time.time;
        initialPosition = transform.position;
        
        // Start with pop movement
        ApplyPopForce();
    }
    
    private void InitializeComponents()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        orbCollider = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();
        audioSource = GetComponent<AudioSource>();
        
        // Setup components if they don't exist
        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }
        
        if (orbCollider == null)
        {
            CircleCollider2D circleCollider = gameObject.AddComponent<CircleCollider2D>();
            circleCollider.isTrigger = true;
            circleCollider.radius = 0.3f;
            orbCollider = circleCollider;
        }
        
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.drag = 2f;
        }
        
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.volume = volume;
        }
        
        // Set initial visual properties
        if (spriteRenderer != null)
        {
            // FIXED: Ensure alpha is always 1
            Color fixedColor = orbColor;
            fixedColor.a = 1f;
            spriteRenderer.color = fixedColor;
            spriteRenderer.sortingOrder = 10;
        }
        
        // Find player
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            player = playerObject.transform;
        }
    }
    
    private void ApplyPopForce()
    {
        // Generate random direction for the pop
        Vector2 randomDirection = Random.insideUnitCircle.normalized;
        
        // Add slight upward bias so orbs don't go straight down
        randomDirection.y = Mathf.Abs(randomDirection.y) * 0.5f + 0.3f;
        
        // Set initial pop velocity
        popVelocity = randomDirection * popForce;
        
        if (rb != null)
        {
            rb.velocity = popVelocity;
        }
    }
    
    private void Update()
    {
        if (isCollected) return;
        
        // Check lifetime
        if (Time.time - spawnTime > lifetime)
        {
            DestroyOrb();
            return;
        }
        
        // Handle popping phase
        if (isPopping)
        {
            UpdatePoppingMovement();
        }
        
        // Update visual effects
        UpdateVisualEffects();
        
        // Check for player attraction (only after popping is done)
        if (!isPopping && player != null && !isBeingAttracted)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, player.position);
            if (distanceToPlayer <= attractionRange)
            {
                StartAttraction();
            }
        }
        
        // Handle attraction movement
        if (isBeingAttracted && player != null)
        {
            MoveTowardsPlayer();
        }
    }
    
    private void UpdateVisualEffects()
    {
        // Only apply bobbing if not popping and not being attracted
        if (!isPopping && !isBeingAttracted)
        {
            float bobOffset = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            Vector3 targetPosition = initialPosition + Vector3.up * bobOffset;
            transform.position = new Vector3(transform.position.x, targetPosition.y, transform.position.z);
        }
        
        // Rotation
        transform.Rotate(0, 0, rotationSpeed * Time.deltaTime);
        
        // Scale animation over time
        float normalizedTime = (Time.time - spawnTime) / lifetime;
        float scaleMultiplier = scaleOverTime.Evaluate(normalizedTime);
        transform.localScale = Vector3.one * scaleMultiplier;
        
        // Pulsing glow effect when being attracted
        if (isBeingAttracted && spriteRenderer != null)
        {
            float pulse = Mathf.Sin(Time.time * 10f) * 0.3f + 0.7f;
            Color glowColor = orbColor;
            glowColor.a = 1f; // FIXED: Keep alpha at 1, only change RGB for pulse
            spriteRenderer.color = Color.Lerp(glowColor, Color.white, pulse * 0.3f);
        }
    }
    
    private void UpdatePoppingMovement()
    {
        float popTime = Time.time - spawnTime;

        if (popTime >= popDuration)
        {
            // End popping phase
            isPopping = false;

            // Store current position as new initial position for bobbing
            initialPosition = transform.position;

            // Gradually slow down the rigidbody
            if (rb != null)
            {
                rb.velocity = Vector2.Lerp(rb.velocity, Vector2.zero, popDeceleration * Time.deltaTime);

                // Stop completely when velocity is very low
                if (rb.velocity.magnitude < 0.1f)
                {
                    rb.velocity = Vector2.zero;
                }
            }
        }
        else
        {
            // Continue popping with deceleration
            if (rb != null)
            {
                float decelerationFactor = 1f - (popTime / popDuration);
                Vector2 currentVelocity = popVelocity * decelerationFactor;
                rb.velocity = currentVelocity;
            }
        }
    }
    
    private void StartAttraction()
    {
        isBeingAttracted = true;
        
        // Disable physics during attraction
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.isKinematic = true;
        }
        
        // Start attraction coroutine
        StartCoroutine(AttractionCoroutine());
    }
    
    private IEnumerator AttractionCoroutine()
    {
        while (isBeingAttracted && player != null && !isCollected)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, player.position);
            
            // Check if close enough to collect
            if (distanceToPlayer < 0.5f)
            {
                CollectOrb();
                yield break;
            }
            
            yield return null;
        }
    }
    
    private void MoveTowardsPlayer()
    {
        if (player == null) return;
        
        Vector3 direction = (player.position - transform.position).normalized;
        float distance = Vector3.Distance(transform.position, player.position);
        
        // Increase speed as we get closer
        float speedMultiplier = Mathf.Lerp(1f, 2f, 1f - (distance / attractionRange));
        float currentSpeed = attractionSpeed * speedMultiplier;
        
        transform.position = Vector3.MoveTowards(transform.position, player.position, currentSpeed * Time.deltaTime);
        
        // Add slight curve to the movement for more organic feel
        Vector3 perpendicular = Vector3.Cross(direction, Vector3.forward).normalized;
        float curveStrength = Mathf.Sin(Time.time * 5f) * 0.5f * (distance / attractionRange);
        transform.position += perpendicular * curveStrength * Time.deltaTime;
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isCollected) return;
        
        if (other.CompareTag("Player"))
        {
            CollectOrb();
        }
    }
    
    private void CollectOrb()
    {
        if (isCollected) return;
        
        isCollected = true;
        
        // Play collection sound
        PlayCollectionSound();
        
        // Add power to the system
        if (PowerSystem.Instance != null)
        {
            PowerSystem.Instance.AddPowerFromEnemy(powerValue);
        }
        
        // Notify collection system
        PowerOrbCollectionManager.Instance?.OnOrbCollected(powerValue, transform.position);
        
        // Fire event
        OnOrbCollected?.Invoke(this);
        
        // Start collection animation
        StartCoroutine(CollectionAnimation());
    }
    
    private void PlayCollectionSound()
    {
        if (collectSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(collectSound);
        }
    }
    
    private IEnumerator CollectionAnimation()
    {
        // Quick scale up then disappear
        Vector3 originalScale = transform.localScale;
        Vector3 targetScale = originalScale * 1.5f;
        
        float animationTime = 0.2f;
        float elapsed = 0f;
        
        while (elapsed < animationTime)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / animationTime;
            
            // Scale up then down
            float scaleProgress = Mathf.Sin(progress * Mathf.PI);
            transform.localScale = Vector3.Lerp(originalScale, targetScale, scaleProgress);
            
            // Fade out - but ensure we don't set alpha to 0 accidentally elsewhere
            if (spriteRenderer != null)
            {
                Color color = spriteRenderer.color;
                color.a = 1f - progress; // This is fine since we're destroying the orb
                spriteRenderer.color = color;
            }
            
            yield return null;
        }
        
        // Wait a moment for sound to finish
        yield return new WaitForSeconds(0.1f);
        
        DestroyOrb();
    }
    
    private void DestroyOrb()
    {
        // Clean up
        OnOrbCollected = null;
        Destroy(gameObject);
    }
    
    // Public methods for customization
    public void SetPowerValue(int value)
    {
        powerValue = value;
    }
    
    public void SetOrbColor(Color color)
    {
        orbColor = color;
        orbColor.a = 1f; // FIXED: Always ensure alpha is 1
        if (spriteRenderer != null)
        {
            spriteRenderer.color = orbColor;
        }
    }
    
    public void SetLifetime(float time)
    {
        lifetime = time;
    }
    
    public int GetPowerValue() => powerValue;
    
    // Gizmos for debugging
    private void OnDrawGizmosSelected()
    {
        // Draw attraction range
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, attractionRange);
        
        // Draw collection range
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
    }
}