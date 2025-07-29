using UnityEngine;

public class Fireball : MonoBehaviour
{
    [Header("Fireball Settings")]
    [SerializeField] private float lifetime = 5f;
    [SerializeField] private bool destroyOnHit = true;
    [SerializeField] private LayerMask hitLayers = -1;
    
    [Header("Visual Effects")]
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private bool rotateInFlight = true;
    [SerializeField] private float rotationSpeed = 360f;
    
    [Header("Audio")]
    [SerializeField] private AudioClip launchSound;
    [SerializeField] private AudioClip hitSound;
    
    // Movement variables
    private Vector3 direction;
    private float speed;
    private int damage;
    private GameObject shooter;
    
    // Components
    private Rigidbody2D rb;
    private Collider2D col;
    private AudioSource audioSource;
    private SpriteRenderer spriteRenderer;
    
    // State
    private bool hasHit = false;
    private float spawnTime;
    
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        audioSource = GetComponent<AudioSource>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        
        // Ensure we have required components
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.isKinematic = true; // We'll move it manually
        }
        
        if (col == null)
        {
            col = gameObject.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
        }
        
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        spawnTime = Time.time;
    }
    
    public void Initialize(Vector3 dir, float spd, int dmg, GameObject source)
    {
        direction = dir.normalized;
        speed = spd;
        damage = dmg;
        shooter = source;
        
        // Play launch sound
        if (launchSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(launchSound);
        }
        
        // Start moving
        if (rb != null)
        {
            rb.velocity = direction * speed;
        }
    }
    
    private void Update()
    {
        // Check lifetime
        if (Time.time - spawnTime > lifetime)
        {
            DestroyFireball();
            return;
        }
        
        // Rotate if enabled
        if (rotateInFlight && !hasHit)
        {
            transform.Rotate(0, 0, rotationSpeed * Time.deltaTime);
        }
        
        // Manual movement if no rigidbody velocity
        if (rb == null || rb.velocity.magnitude < 0.1f)
        {
            transform.position += direction * speed * Time.deltaTime;
        }
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHit) return;
        
        // Don't hit the shooter
        if (other.gameObject == shooter) return;
        
        // Check if we should hit this layer
        if (hitLayers != -1 && !IsInLayerMask(other.gameObject.layer, hitLayers)) return;
        
        // Try to damage the target
        bool hitSomething = TryDamageTarget(other.gameObject);
        
        if (hitSomething)
        {
            OnHit(other.transform.position);
        }
    }
    
    private bool IsInLayerMask(int layer, LayerMask layerMask)
    {
        return (layerMask.value & (1 << layer)) != 0;
    }
    
    private bool TryDamageTarget(GameObject target)
    {
        // Check for various health component types
        // Try generic Health component first
        Health health = target.GetComponent<Health>();
        if (health != null)
        {
            health.TakeDamage(damage);
            return true;
        }
        
        // Try EnemyHealth (though enemies shouldn't hit each other)
        EnemyHealth enemyHealth = target.GetComponent<EnemyHealth>();
        if (enemyHealth != null)
        {
            enemyHealth.TakeDamage(damage);
            return true;
        }
        
        // Try PlayerHealth
        PlayerHealth playerHealth = target.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(damage);
            return true;
        }
        
        // Try VillagerHealth
        VillagerHealth villagerHealth = target.GetComponent<VillagerHealth>();
        if (villagerHealth != null)
        {
            villagerHealth.TakeDamage(damage);
            return true;
        }
        
        // Try BuildingHealth
        BuildingHealth buildingHealth = target.GetComponent<BuildingHealth>();
        if (buildingHealth != null)
        {
            buildingHealth.TakeDamage(damage);
            return true;
        }
        
        // Check if it's a valid target even if no health component
        if (target.CompareTag("Player") || target.CompareTag("Villager") || target.CompareTag("Building"))
        {
            Debug.LogWarning($"Fireball hit {target.name} but no health component found!");
            return true;
        }
        
        return false;
    }
    
    private void OnHit(Vector3 hitPosition)
    {
        hasHit = true;
        
        // Stop movement
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
        }
        
        // Play hit effect
        if (hitEffectPrefab != null)
        {
            GameObject effect = Instantiate(hitEffectPrefab, hitPosition, Quaternion.identity);
            Destroy(effect, 2f); // Clean up effect after 2 seconds
        }
        
        // Play hit sound
        if (hitSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(hitSound);
        }
        
        // Destroy or hide fireball
        if (destroyOnHit)
        {
            // Hide visuals immediately
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = false;
            }
            
            // Destroy after sound finishes
            float destroyDelay = (hitSound != null && audioSource != null) ? hitSound.length : 0.1f;
            Destroy(gameObject, destroyDelay);
        }
    }
    
    private void DestroyFireball()
    {
        // Could add fade out effect here
        Destroy(gameObject);
    }
    
    private void OnDrawGizmos()
    {
        if (Application.isPlaying && direction != Vector3.zero)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position, direction * 2f);
        }
    }
}