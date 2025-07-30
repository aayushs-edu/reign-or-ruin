using UnityEngine;
using System.Collections;

public class Health : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] protected int maxHealth = 10;
    [SerializeField] protected int currentHealth;
    [SerializeField] protected bool destroyOnDeath = true;
    [SerializeField] protected float deathDelay = 0f;
    
    [Header("Visual Feedback")]
    [SerializeField] protected bool useFlashEffect = true;
    [SerializeField] protected float flashDuration = 0.1f;
    [SerializeField] protected Color damageColor = Color.red;
    [SerializeField] protected Color healColor = Color.green;
    
    [Header("Effects")]
    [SerializeField] protected GameObject deathEffectPrefab;
    [SerializeField] protected AudioClip damageSound;
    [SerializeField] protected AudioClip deathSound;
    [SerializeField] protected AudioClip healSound;
    
    [Header("UI")]
    [SerializeField] protected bool showHealthBar = true;
    [SerializeField] protected Transform healthBarContainer; // Where to look for health bar
    
    // Components
    protected SpriteRenderer spriteRenderer;
    protected AudioSource audioSource;
    protected Color originalColor;
    protected HealthBar healthBar;
    
    // State
    protected bool isDead = false;
    protected bool isFlashing = false;
    
    // Events
    public System.Action<int> OnDamaged; // damage amount
    public System.Action<int> OnHealed; // heal amount
    public System.Action<int> OnHealthChanged; // current health
    public System.Action OnDeath;
    
    protected virtual void Awake()
    {
        currentHealth = maxHealth;
        InitializeComponents();
    }
    
    protected virtual void Start()
    {
        FindHealthBar();
        UpdateHealthBar();
    }
    
    protected virtual void InitializeComponents()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }
        
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }
    
    protected virtual void FindHealthBar()
    {
        if (!showHealthBar) return;
        
        // If health bar container is specified, look there first
        if (healthBarContainer != null)
        {
            healthBar = healthBarContainer.GetComponentInChildren<HealthBar>();
        }
        
        // Otherwise, look in the entity's canvas
        if (healthBar == null)
        {
            Canvas canvas = GetComponentInChildren<Canvas>();
            if (canvas != null)
            {
                healthBar = canvas.GetComponentInChildren<HealthBar>();
            }
        }
        
        // Last resort: look anywhere in children
        if (healthBar == null)
        {
            healthBar = GetComponentInChildren<HealthBar>();
        }
        
        if (healthBar != null)
        {
            healthBar.SetMaxHealth(maxHealth);
            healthBar.SetHealth(currentHealth);
            
            if (!showHealthBar)
            {
                healthBar.Hide();
            }
        }
        else if (showHealthBar)
        {
            Debug.LogWarning($"Health component on {gameObject.name} is set to show health bar, but no HealthBar component was found in children!");
        }
    }
    
    public virtual void TakeDamage(int damage)
    {
        if (isDead) return;
        
        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        
        OnDamaged?.Invoke(damage);
        OnHealthChanged?.Invoke(currentHealth);
        
        UpdateHealthBar();
        
        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            // Visual and audio feedback
            OnDamageTaken();
        }
    }
    
    public virtual void Heal(int amount)
    {
        if (isDead) return;
        
        int actualHeal = Mathf.Min(amount, maxHealth - currentHealth);
        currentHealth += actualHeal;
        
        OnHealed?.Invoke(actualHeal);
        OnHealthChanged?.Invoke(currentHealth);
        
        UpdateHealthBar();
        
        // Visual and audio feedback
        if (actualHeal > 0)
        {
            OnHealReceived();
        }
    }
    
    protected virtual void OnDamageTaken()
    {
        // Flash effect
        if (useFlashEffect && spriteRenderer != null && !isFlashing)
        {
            StartCoroutine(FlashEffect(damageColor));
        }
        
        // Sound
        if (damageSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(damageSound);
        }
    }
    
    protected virtual void OnHealReceived()
    {
        // Flash effect
        if (useFlashEffect && spriteRenderer != null && !isFlashing)
        {
            StartCoroutine(FlashEffect(healColor));
        }
        
        // Sound
        if (healSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(healSound);
        }
    }
    
    protected virtual void Die()
    {
        if (isDead) return;
        
        isDead = true;
        OnDeath?.Invoke();
        
        // Death effect
        if (deathEffectPrefab != null)
        {
            GameObject effect = Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);
            Destroy(effect, 3f);
        }
        
        // Sound
        if (deathSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(deathSound);
        }
        
        // Hide health bar immediately if it exists
        if (healthBar != null)
        {
            healthBar.Hide();
        }
        
        // Handle destruction
        if (destroyOnDeath)
        {
            // Disable components to prevent further interactions
            DisableComponents();
            
            // Destroy after delay
            Destroy(gameObject, deathDelay);
        }
        else
        {
            // Just disable the object
            gameObject.SetActive(false);
        }
    }
    
    protected virtual void DisableComponents()
    {
        // Disable colliders
        Collider2D[] colliders = GetComponents<Collider2D>();
        foreach (var col in colliders)
        {
            col.enabled = false;
        }
        
        // Disable movement/AI
        MonoBehaviour[] scripts = GetComponents<MonoBehaviour>();
        foreach (var script in scripts)
        {
            if (script != this && script.GetType() != typeof(AudioSource))
            {
                script.enabled = false;
            }
        }
        
        // Fade out sprite
        if (spriteRenderer != null && deathDelay > 0)
        {
            StartCoroutine(FadeOut());
        }
    }
    
    protected IEnumerator FlashEffect(Color flashColor)
    {
        isFlashing = true;
        spriteRenderer.color = flashColor;
        yield return new WaitForSeconds(flashDuration);
        spriteRenderer.color = originalColor;
        isFlashing = false;
    }
    
    protected IEnumerator FadeOut()
    {
        float elapsed = 0f;
        Color startColor = spriteRenderer.color;
        
        while (elapsed < deathDelay)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / deathDelay);
            spriteRenderer.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            yield return null;
        }
    }
    
    protected void UpdateHealthBar()
    {
        if (healthBar != null)
        {
            healthBar.SetHealth(currentHealth);
        }
    }
    
    // Public getters
    public int GetCurrentHealth() => currentHealth;
    public int GetMaxHealth() => maxHealth;
    public float GetHealthPercentage() => maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;
    public bool IsDead() => isDead;
    public bool IsFullHealth() => currentHealth >= maxHealth;
    
    // Public setters
    public void SetMaxHealth(int newMax, bool healToFull = false)
    {
        maxHealth = newMax;
        if (healToFull)
        {
            currentHealth = maxHealth;
        }
        else
        {
            currentHealth = Mathf.Min(currentHealth, maxHealth);
        }
        
        if (healthBar != null)
        {
            healthBar.SetMaxHealth(maxHealth);
        }
        UpdateHealthBar();
    }
    
    public void Revive(int healthAmount = -1)
    {
        if (!isDead) return;
        
        isDead = false;
        currentHealth = healthAmount > 0 ? Mathf.Min(healthAmount, maxHealth) : maxHealth;
        
        // Re-enable components
        gameObject.SetActive(true);
        MonoBehaviour[] scripts = GetComponents<MonoBehaviour>();
        foreach (var script in scripts)
        {
            script.enabled = true;
        }
        
        Collider2D[] colliders = GetComponents<Collider2D>();
        foreach (var col in colliders)
        {
            col.enabled = true;
        }
        
        // Reset visuals
        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalColor;
        }
        
        FindHealthBar();
        UpdateHealthBar();
        OnHealthChanged?.Invoke(currentHealth);
    }
}