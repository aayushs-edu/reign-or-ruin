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
        if (currentHealth <= 0) return; // Already dead
        
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
    
    // For collision detection with player attacks
    private void OnTriggerEnter2D(Collider2D other)
    {
        // Debug what's triggering
        Debug.Log($"Enemy {gameObject.name} triggered by: {other.gameObject.name} with tag: {other.tag}");
        
        if (other.CompareTag("Weapon"))
        {
            TakeDamage(1); // Basic damage, you can modify this
        }
        
        // IMPORTANT: Don't damage from player collision
        // Remove this if you had it:
        // if (other.CompareTag("Player")) { TakeDamage(999); }
    }
}