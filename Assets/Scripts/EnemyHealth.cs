using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 3;
    [SerializeField] private int currentHealth;
    
    [Header("Visual Feedback")]
    [SerializeField] private float flashDuration = 0.1f;
    [SerializeField] private Color damageColor = Color.red;
    
    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private bool isFlashing = false;
    private bool isDead = false;
    
    // Events
    public System.Action OnDeath;
    public System.Action<int> OnHealthChanged; // Current health
    
    private void Start()
    {
        currentHealth = maxHealth;
        spriteRenderer = GetComponent<SpriteRenderer>();
        
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }
    }
    
    public void TakeDamage(int damage)
    {
        if (isDead) return; // Already dead, ignore further damage
        
        currentHealth -= damage;
        currentHealth = Mathf.Max(0, currentHealth);
        
        OnHealthChanged?.Invoke(currentHealth);
        
        // Visual feedback
        if (spriteRenderer != null && !isFlashing)
        {
            StartCoroutine(FlashDamage());
        }
        
        if (currentHealth <= 0)
        {
            Die();
        }
    }
    
    private void Die()
    {
        if (isDead) return; // Prevent multiple death calls
        
        isDead = true;
        OnDeath?.Invoke();
        
        // Add death effect here if desired
        // Instantiate(deathEffect, transform.position, Quaternion.identity);
        
        Destroy(gameObject);
    }
    
    private System.Collections.IEnumerator FlashDamage()
    {
        isFlashing = true;
        spriteRenderer.color = damageColor;
        yield return new WaitForSeconds(flashDuration);
        spriteRenderer.color = originalColor;
        isFlashing = false;
    }
    
    // Public getters
    public int GetCurrentHealth() => currentHealth;
    public int GetMaxHealth() => maxHealth;
    public float GetHealthPercentage() => (float)currentHealth / maxHealth;
    public bool IsDead() => isDead;
    
    // For collision detection with player attacks
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isDead) return; // Don't process collisions if already dead
        
        // Debug what's triggering
        Debug.Log($"Enemy {gameObject.name} triggered by: {other.gameObject.name} with tag: {other.tag}");
        
        if (other.CompareTag("Weapon"))
        {
            // Check if it's a throwable weapon
            ThrowableWeaponSystem throwableWeapon = other.GetComponent<ThrowableWeaponSystem>();
            if (throwableWeapon != null)
            {
                // Throwable weapon handles its own damage in HandleWeaponCollision
                // Don't apply damage here to avoid double damage
                return;
            }
            
            // Handle regular weapon system or basic weapon damage
            TakeDamage(1); // Basic damage, you can modify this
        }
        
        // IMPORTANT: Don't damage from player collision
        // Remove this if you had it:
        // if (other.CompareTag("Player")) { TakeDamage(999); }
    }
}