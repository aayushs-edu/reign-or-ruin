// Enhanced VillagerHealth.cs - Better damage source tracking
using UnityEngine;

public class VillagerHealth : Health
{
    [Header("Villager Specific")]
    [SerializeField] private int powerDropOnDeath = 5;
    [SerializeField] private bool canRebel = true;
    [SerializeField] private float rebelHealthMultiplier = 1.5f;
    [SerializeField] private float witnessDeathRadius = 5f;
    
    private bool isRebel = false;
    private VillagerAI villagerAI;
    private Villager villagerComponent;
    private GameObject lastDamageSource;
    
    protected override void Awake()
    {
        base.Awake();
        villagerAI = GetComponent<VillagerAI>();
        villagerComponent = GetComponent<Villager>();
        
        // Set initial health if villager component exists
        if (villagerComponent != null)
        {
            SetInitialHealthByRole();
        }
    }
    
    private void SetInitialHealthByRole()
    {
        if (villagerComponent == null) return;
        
        VillagerRole role = villagerComponent.GetRole();
        switch (role)
        {
            case VillagerRole.Captain:
                maxHealth = 150;
                break;
            case VillagerRole.Farmer:
                maxHealth = 80;
                break;
            case VillagerRole.Mage:
                maxHealth = 70;
                break;
            case VillagerRole.Builder:
                maxHealth = 120;
                break;
            case VillagerRole.Commoner:
                maxHealth = 20;
                break;
        }
        
        currentHealth = maxHealth;
        UpdateHealthBar();
    }
    
    public override void TakeDamage(int damage)
    {
        TakeDamage(damage, null);
    }
    
    public void TakeDamage(int damage, GameObject damageSource)
    {
        if (isDead) return;
        
        lastDamageSource = damageSource;
        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        
        OnDamaged?.Invoke(damage);
        OnHealthChanged?.Invoke(currentHealth);
        
        UpdateHealthBar();
        
        // Enhanced damage source detection
        bool isPlayerDamage = IsPlayerDamage(damageSource);
        
        Debug.Log($"{gameObject.name} took {damage} damage from {(damageSource != null ? damageSource.name : "unknown")} (IsPlayer: {isPlayerDamage})");
        
        // If damaged by player and villager component exists, trigger friendly fire response
        if (isPlayerDamage && villagerComponent != null && !isRebel)
        {
            villagerComponent.OnHitByPlayerFriendlyFire();
        }
        
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
    
    private bool IsPlayerDamage(GameObject damageSource)
    {
        if (damageSource == null) return false;
        
        // Method 1: Direct player tag check
        if (damageSource.CompareTag("Player"))
        {
            return true;
        }
        
        // Method 2: Check if damage source is player weapon
        if (damageSource.name.Contains("Weapon") || damageSource.name.Contains("weapon"))
        {
            // Check if weapon belongs to player
            Transform parent = damageSource.transform.parent;
            while (parent != null)
            {
                if (parent.CompareTag("Player"))
                {
                    return true;
                }
                parent = parent.parent;
            }
        }
        
        // Method 3: Check for ThrowableWeaponSystem (player's throwable weapon)
        ThrowableWeaponSystem weaponSystem = damageSource.GetComponent<ThrowableWeaponSystem>();
        if (weaponSystem != null)
        {
            return true;
        }
        
        // Method 4: Check if damage source has player as root
        Transform root = damageSource.transform.root;
        if (root != null && root.CompareTag("Player"))
        {
            return true;
        }
        
        return false;
    }
    
    protected override void Die()
    {
        if (isDead) return;
        
        // Check if killed by player
        bool killedByPlayer = IsPlayerDamage(lastDamageSource);
        
        Debug.Log($"{gameObject.name} has died! Killed by player: {killedByPlayer}, Last damage source: {(lastDamageSource != null ? lastDamageSource.name : "none")}");
        
        // Notify nearby villagers if killed by player
        if (killedByPlayer)
        {
            NotifyNearbyVillagersOfDeath();
        }
        
        // Drop power if killed
        if (PowerSystem.Instance != null && powerDropOnDeath > 0)
        {
            PowerSystem.Instance.AddPowerFromEnemy(powerDropOnDeath);
        }
        
        // Notify villager component of death
        if (villagerComponent != null)
        {
            villagerComponent.OnDeath();
        }
        
        base.Die();
    }
    
    private void NotifyNearbyVillagersOfDeath()
    {
        // Find all villagers within witness radius
        Collider2D[] nearbyColliders = Physics2D.OverlapCircleAll(transform.position, witnessDeathRadius);
        
        foreach (var collider in nearbyColliders)
        {
            if (collider.gameObject == gameObject) continue; // Skip self
            
            Villager witness = collider.GetComponent<Villager>();
            if (witness != null && witness.IsLoyal())
            {
                witness.OnWitnessedVillagerDeath(villagerComponent, true);
            }
        }
    }
    
    public void ConvertToRebel()
    {
        if (!canRebel || isRebel) return;
        
        isRebel = true;
        gameObject.tag = "Enemy"; // Change tag so enemies don't attack rebels
        
        // Increase health as rebel
        // int newMaxHealth = Mathf.RoundToInt(maxHealth * rebelHealthMultiplier);
        // SetMaxHealth(newMaxHealth, true);
        
        // Visual indicator
        if (spriteRenderer != null)
        {
            spriteRenderer.color = new Color(1f, 0.5f, 0.5f); // Reddish tint
        }
        
        // Change AI behavior
        if (villagerAI != null)
        {
            villagerAI.SetRebel(true);
        }
    }
    
    public void UpdateHealthForTier(int tier)
    {
        if (villagerComponent == null) return;
        
        // Get base health for role
        int baseHP = GetBaseHealthForRole(villagerComponent.GetRole());
        
        // Update max HP based on tier (+20% per tier)
        int newMaxHealth = Mathf.RoundToInt(baseHP * (1f + tier * 0.2f));
        
        // If health increased, heal to full
        bool healToFull = newMaxHealth > maxHealth;
        SetMaxHealth(newMaxHealth, healToFull);
    }
    
    private int GetBaseHealthForRole(VillagerRole role)
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
    
    public bool IsRebel() => isRebel;
    public int GetCurrentHP() => currentHealth;
    public int GetMaxHP() => maxHealth;
    
    private void OnDrawGizmosSelected()
    {
        // Draw witness death radius
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, witnessDeathRadius);
    }
}