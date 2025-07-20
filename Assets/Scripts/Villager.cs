using UnityEngine;
using System.Collections;

public enum VillagerRole
{
    Captain,
    Farmer,
    Mage,
    Builder,
    Commoner
}

public enum VillagerState
{
    Loyal,
    Angry,
    Rebel
}

[System.Serializable]
public class VillagerStats
{
    [Header("Core Stats")]
    public int power = 0;           // Power allocated this allocation phase
    public float food = 1f;         // Food percentage (0-1)
    public float discontent = 0f;   // Discontent level (0-100)
    
    [Header("Role Stats")]
    public int maxHP = 100;
    public int currentHP = 100;
    public int tier = 0;            // 0 = base, 1 = tier 1, 2 = tier 2
    public bool isActive = true;    // False if building destroyed
}

public class Villager : MonoBehaviour
{
    [Header("Villager Configuration")]
    [SerializeField] private VillagerRole role = VillagerRole.Commoner;
    [SerializeField] private VillagerStats stats = new VillagerStats();
    [SerializeField] private Transform buildingTransform; // Reference to building this villager occupies
    
    [Header("Visual Components")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private VillagerStatsUI statsUI;
    
    [Header("State Sprites")]
    [SerializeField] private Sprite loyalSprite;
    [SerializeField] private Sprite rebelSprite;
    [SerializeField] private Sprite angrySprite;
    
    [Header("Role Constants")]
    [SerializeField] private DiscontentConstants discontentConstants;
    
    // State
    private VillagerState currentState = VillagerState.Loyal;
    private bool isFlashing = false;
    private Color originalColor;
    
    // Events
    public System.Action<Villager> OnVillagerRebel;
    public System.Action<Villager> OnVillagerDeath;
    public System.Action<Villager, float> OnDiscontentChanged;
    
    // References
    private VillageManager villageManager;
    
    private void Start()
    {
        InitializeVillager();
        SetupReferences();
        UpdateVisuals();
    }
    
    private void InitializeVillager()
    {
        // Auto-find components if not assigned
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        
        if (statsUI == null)
            statsUI = GetComponentInChildren<VillagerStatsUI>();
        
        // Store original color for flashing - ENSURE it's not black
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
            // Safety check - if color is black or transparent, set to white
            if (originalColor.r == 0f && originalColor.g == 0f && originalColor.b == 0f)
            {
                originalColor = Color.white;
                spriteRenderer.color = originalColor;
                Debug.LogWarning($"Villager {gameObject.name} had black color, reset to white");
            }
        }
        
        // Set initial HP based on role
        SetInitialStats();
        
        // Ensure proper initial state
        currentState = VillagerState.Loyal;
        UpdateVisuals();
    }
    
    private void SetupReferences()
    {
        villageManager = FindObjectOfType<VillageManager>();
        if (villageManager == null)
        {
            Debug.LogWarning($"VillageManager not found for villager {gameObject.name}");
        }
    }
    
    private void SetInitialStats()
    {
        // Set base HP by role
        switch (role)
        {
            case VillagerRole.Captain:
                stats.maxHP = 150;
                break;
            case VillagerRole.Farmer:
                stats.maxHP = 80;
                break;
            case VillagerRole.Mage:
                stats.maxHP = 70;
                break;
            case VillagerRole.Builder:
                stats.maxHP = 120;
                break;
            case VillagerRole.Commoner:
                stats.maxHP = 100;
                break;
        }
        
        stats.currentHP = stats.maxHP;
    }
    
    public void AllocatePower(int powerAmount)
    {
        stats.power = powerAmount;
        
        // Determine tier based on power allocated
        if (powerAmount >= 4)
            stats.tier = 2;
        else if (powerAmount >= 2)
            stats.tier = 1;
        else
            stats.tier = 0;
        
        // Update max HP based on tier (+20% per tier)
        int baseHP = GetBaseMaxHP();
        stats.maxHP = Mathf.RoundToInt(baseHP * (1f + stats.tier * 0.2f));
        
        // Heal to full if HP increased
        if (stats.currentHP < stats.maxHP)
            stats.currentHP = stats.maxHP;
        
        Debug.Log($"{role} {gameObject.name}: Allocated {powerAmount} power, Tier {stats.tier}, MaxHP {stats.maxHP}");
        
        UpdateVisuals();
    }
    
    private int GetBaseMaxHP()
    {
        switch (role)
        {
            case VillagerRole.Captain: return 150;
            case VillagerRole.Farmer: return 80;
            case VillagerRole.Mage: return 70;
            case VillagerRole.Builder: return 120;
            case VillagerRole.Commoner: return 100;
            default: return 100;
        }
    }
    
    public void SetFoodLevel(float foodPercent)
    {
        stats.food = Mathf.Clamp01(foodPercent);
        UpdateVisuals();
    }
    
    public void ProcessDiscontentAtAllocation()
    {
        if (discontentConstants == null)
        {
            Debug.LogWarning("Discontent constants not assigned!");
            return;
        }
        
        // Calculate power shortage
        int tierCost = stats.tier * 2; // Tier 1 = 2 power, Tier 2 = 4 power
        int powerShortage = Mathf.Max(0, tierCost - stats.power);
        
        // Calculate food shortage
        float foodShortage = Mathf.Max(0, 1f - stats.food);
        
        // Add discontent
        float discontentIncrease = (powerShortage * discontentConstants.powerPenalty) + 
                                  (foodShortage * discontentConstants.foodPenalty);
        
        AddDiscontent(discontentIncrease);
        
        Debug.Log($"{role} {gameObject.name}: Power shortage: {powerShortage}, Food shortage: {foodShortage:F2}, Discontent added: {discontentIncrease:F1}");
    }
    
    public void ProcessNightlyRecovery()
    {
        if (discontentConstants == null) return;
        
        // Recovery if well-fed and full power
        int tierCost = stats.tier * 2;
        if (stats.food >= 1f && stats.power >= tierCost)
        {
            stats.discontent = Mathf.Max(0, stats.discontent - discontentConstants.recoveryRate);
            OnDiscontentChanged?.Invoke(this, stats.discontent);
            UpdateVisuals();
        }
    }
    
    public void AddDiscontent(float amount)
    {
        float oldDiscontent = stats.discontent;
        stats.discontent = Mathf.Clamp(stats.discontent + amount, 0f, 100f);
        
        OnDiscontentChanged?.Invoke(this, stats.discontent);
        
        // Check for rebellion trigger
        if (oldDiscontent < 100f && stats.discontent >= 100f && currentState == VillagerState.Loyal)
        {
            TriggerRebellion();
        }
        
        UpdateVisuals();
    }
    
    public void OnHitByPlayerFriendlyFire()
    {
        if (discontentConstants != null)
        {
            AddDiscontent(discontentConstants.friendlyFirePenalty);
            Debug.Log($"{role} {gameObject.name} hit by friendly fire! +{discontentConstants.friendlyFirePenalty} discontent");
        }
    }
    
    public void OnBuildingDestroyed()
    {
        stats.isActive = false;
        
        if (discontentConstants != null)
        {
            AddDiscontent(discontentConstants.buildingDestroyedPenalty);
            Debug.Log($"{role} {gameObject.name} building destroyed! +{discontentConstants.buildingDestroyedPenalty} discontent");
        }
        
        UpdateVisuals();
    }
    
    public void OnBuildingRepaired()
    {
        stats.isActive = true;
        UpdateVisuals();
    }
    
    private void TriggerRebellion()
    {
        Debug.Log($"{role} {gameObject.name} is starting rebellion process!");
        StartCoroutine(RebellionSequence());
    }
    
    private IEnumerator RebellionSequence()
    {
        // Start angry state with flashing
        currentState = VillagerState.Angry;
        UpdateVisuals();
        
        // Flash for 1 second as telegraph
        yield return StartCoroutine(FlashAngry(1f));
        
        // Flip to rebel
        currentState = VillagerState.Rebel;
        UpdateVisuals();
        
        // Change faction/tag for combat system
        gameObject.tag = "Enemy"; // Or whatever tag your combat system uses for enemies
        
        // Notify rebellion event
        OnVillagerRebel?.Invoke(this);
        
        Debug.Log($"{role} {gameObject.name} has rebelled!");
    }
    
    private IEnumerator FlashAngry(float duration)
    {
        isFlashing = true;
        float flashSpeed = 10f; // flashes per second
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            // Alternate between original color and red
            Color flashColor = Mathf.Sin(elapsed * flashSpeed * Mathf.PI) > 0 ? Color.red : originalColor;
            
            if (spriteRenderer != null)
                spriteRenderer.color = flashColor;
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // Restore original color
        if (spriteRenderer != null)
            spriteRenderer.color = originalColor;
        
        isFlashing = false;
    }
    
    private void UpdateVisuals()
    {
        // Update UI
        if (statsUI != null)
        {
            statsUI.UpdateStats(stats);
        }
        
        // Update sprite based on state
        UpdateStateSprite();
        
        // Update sprite transparency based on active state (but preserve color)
        if (spriteRenderer != null && !isFlashing)
        {
            Color color = originalColor;
            // Only modify alpha, preserve RGB values
            color.a = stats.isActive ? originalColor.a : originalColor.a * 0.5f;
            spriteRenderer.color = color;
        }
    }
    
    private void UpdateStateSprite()
    {
        if (spriteRenderer == null) return;
        
        Sprite spriteToUse = null;
        
        switch (currentState)
        {
            case VillagerState.Loyal:
                spriteToUse = loyalSprite;
                break;
                
            case VillagerState.Angry:
                spriteToUse = angrySprite != null ? angrySprite : loyalSprite; // Fallback to loyal if no angry sprite
                break;
                
            case VillagerState.Rebel:
                spriteToUse = rebelSprite != null ? rebelSprite : loyalSprite; // Fallback to loyal if no rebel sprite
                break;
        }
        
        if (spriteToUse != null)
        {
            spriteRenderer.sprite = spriteToUse;
        }
    }
    
    public void TakeDamage(int damage)
    {
        stats.currentHP = Mathf.Max(0, stats.currentHP - damage);
        
        if (stats.currentHP <= 0)
        {
            Die();
        }
        
        UpdateVisuals();
    }
    
    private void Die()
    {
        OnVillagerDeath?.Invoke(this);
        
        // Visual death effect could go here
        Debug.Log($"{role} {gameObject.name} has died!");
        
        // For now, just deactivate
        gameObject.SetActive(false);
    }
    
    // Public getters
    public VillagerRole GetRole() => role;
    public VillagerState GetState() => currentState;
    public VillagerStats GetStats() => stats;
    public bool IsActive() => stats.isActive;
    public bool IsLoyal() => currentState == VillagerState.Loyal;
    public bool IsRebel() => currentState == VillagerState.Rebel;
    public float GetDiscontentPercentage() => stats.discontent / 100f;
    
    // Public setters for testing
    public void SetDiscontent(float amount)
    {
        stats.discontent = Mathf.Clamp(amount, 0f, 100f);
        OnDiscontentChanged?.Invoke(this, stats.discontent);
        UpdateVisuals();
    }
    
    public void SetRole(VillagerRole newRole)
    {
        role = newRole;
        SetInitialStats();
        UpdateVisuals();
    }
    
    // Sprite management methods
    public void SetStateSprites(Sprite loyal, Sprite rebel, Sprite angry = null)
    {
        loyalSprite = loyal;
        rebelSprite = rebel;
        angrySprite = angry;
        UpdateVisuals();
    }
    
    public void ForceStateUpdate()
    {
        UpdateStateSprite();
    }
    
    // Editor helper
    private void OnValidate()
    {
        if (Application.isPlaying) return;
        
        // Clamp values in editor
        stats.power = Mathf.Max(0, stats.power);
        stats.food = Mathf.Clamp01(stats.food);
        stats.discontent = Mathf.Clamp(stats.discontent, 0f, 100f);
    }
}