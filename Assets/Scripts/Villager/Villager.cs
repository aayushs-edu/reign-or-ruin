using UnityEngine;
using System.Collections;
using System.Collections.Generic;

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
    [SerializeField] private Sprite selectedSprite; // NEW: Sprite to show when hovered/selected
    
    [Header("Role Constants")]
    [SerializeField] public Settings settings;
    
    [Header("Rebellion System V1")]
    [SerializeField] private float baseDiscontentRate = 1f; // Base discontent gain per second
    [SerializeField] private float powerDiscontentReduction = 0.5f; // Reduction per power tier
    [SerializeField] private float lowFoodDiscontentMultiplier = 2f; // Multiplier when food < 0.3
    [SerializeField] private float angryCombatEfficiencyMultiplier = 0.5f; // Combat reduction when angry
    
    [Header("Food System")]
    [SerializeField] private float baseFoodDecayRate = 1f; // Base food loss per second
    [SerializeField] private float powerFoodDecayReduction = 0.3f; // Reduction per power tier
    [SerializeField] private float lowFoodEfficiencyThreshold = 0.5f; // Food level below which efficiency drops
    [SerializeField] private float lowFoodEfficiencyMultiplier = 0.7f; // Efficiency at low food
    
    // State
    private VillagerState currentState = VillagerState.Loyal;
    private bool isFlashing = false;
    private bool isSelected = false; // NEW: Track selection state
    private Color originalColor;
    private Sprite originalSprite; // NEW: Store original sprite for restoration
    private float lastDiscontentUpdate = 0f;
    private float lastFoodUpdate = 0f;
    
    // Events
    public System.Action<Villager> OnVillagerRebel;
    public System.Action<Villager> OnVillagerDeath;
    public System.Action<Villager, float> OnDiscontentChanged;
    public System.Action<Villager, VillagerState> OnStateChanged;
    
    // References
    private VillageManager villageManager;
    private VillagerCombat combatComponent;
    private StatusIndicatorManager statusIndicators;
    private VillagerHealth healthComponent;
    
    // UI state
    private bool statsUIVisible = false;
    
    private void Start()
    {
        InitializeVillager();
        SetupReferences();
        SetupCombatComponent();
        UpdateVisuals();
    }
    
    private void Update()
    {
        // Update discontent based on current state
        UpdateDiscontentOverTime();
        
        // Update food consumption
        UpdateFoodConsumption();
    }
    
    private void InitializeVillager()
    {
        // Auto-find components if not assigned
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        
        if (statsUI == null)
            statsUI = GetComponentInChildren<VillagerStatsUI>();
        
        // Get health component
        healthComponent = GetComponent<VillagerHealth>();
        if (healthComponent == null)
        {
            Debug.LogError($"Villager {gameObject.name} is missing VillagerHealth component!");
        }
        
        // Store original color and sprite for restoration
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
            originalSprite = spriteRenderer.sprite; // NEW: Store original sprite
            
            // Safety check - if color is black or transparent, set to white
            if (originalColor.r == 0f && originalColor.g == 0f && originalColor.b == 0f)
            {
                originalColor = Color.white;
                spriteRenderer.color = originalColor;
                Debug.LogWarning($"Villager {gameObject.name} had black color, reset to white");
            }
        }
        
        // Ensure proper initial state
        currentState = VillagerState.Loyal;
        UpdateVisuals();
        
        // Initialize status indicators
        statusIndicators = GetComponentInChildren<StatusIndicatorManager>();
        if (statusIndicators == null)
        {
            GameObject indicatorObj = new GameObject("StatusIndicators");
            indicatorObj.transform.SetParent(transform);
            indicatorObj.transform.localPosition = new Vector3(0, 1.5f, 0);
            statusIndicators = indicatorObj.AddComponent<StatusIndicatorManager>();
        }
    }
    
    // NEW: Method to handle selection/hover state
    public void SetSelected(bool selected)
    {
        if (isSelected == selected) return;
        
        // Rebels cannot be selected
        if (currentState == VillagerState.Rebel && selected)
        {
            Debug.Log($"Cannot select rebel villager {gameObject.name}");
            return;
        }
        
        isSelected = selected;
        
        Debug.Log($"Villager {gameObject.name} selection set to: {selected}");
        
        // Immediately update the visual
        UpdateSelectionVisual();
    }
    
    // NEW: Update sprite based on selection state
    private void UpdateSelectionVisual()
    {
        if (spriteRenderer == null) return;
        
        if (isSelected && selectedSprite != null && currentState != VillagerState.Rebel)
        {
            // Show selected sprite when hovering (but not for rebels)
            spriteRenderer.sprite = selectedSprite;
            Debug.Log($"Setting selected sprite for {gameObject.name}");
        }
        else
        {
            // Restore appropriate state sprite
            UpdateStateSprite();
        }
    }
    
    // NEW: Check if villager can be interacted with
    public bool CanBeInteracted()
    {
        return currentState != VillagerState.Rebel;
    }
    
    private void UpdateFoodConsumption()
    {
        if (Time.time - lastFoodUpdate < 0.1f) return; // Update every 0.1 seconds
        lastFoodUpdate = Time.time;
        
        // Calculate food decay rate based on power tier
        float foodDecayRate = baseFoodDecayRate * 0.1f; // Per 0.1 second
        
        // Power reduces food consumption
        if (stats.tier > 0)
        {
            foodDecayRate -= (stats.tier * powerFoodDecayReduction * 0.1f);
        }
        
        // Ensure food decay rate doesn't go negative
        foodDecayRate = Mathf.Max(0f, foodDecayRate);
        
        // Decrease food
        stats.food = Mathf.Max(0f, stats.food - foodDecayRate / 100f); // Divide by 100 to make it percentage
        
        // Update status indicators
        if (statusIndicators != null)
        {
            statusIndicators.UpdateFoodStatus(stats.food < 0.3f);
        }
        
        // Update combat efficiency based on food
        UpdateFoodEfficiency();
        
        UpdateVisuals();
    }
    
    private void UpdateFoodEfficiency()
    {
        if (combatComponent == null) return;

        float baseEfficiency = 1f;

        // Apply food efficiency penalty if food is low
        if (stats.food < lowFoodEfficiencyThreshold)
        {
            // Linear interpolation between low efficiency and full efficiency
            float foodEfficiency = Mathf.Lerp(lowFoodEfficiencyMultiplier, 1f, stats.food / lowFoodEfficiencyThreshold);
            baseEfficiency *= foodEfficiency;
        }

        // Apply angry state penalty on top of food penalty
        if (currentState == VillagerState.Angry)
        {
            baseEfficiency *= angryCombatEfficiencyMultiplier;
        }

        combatComponent.SetCombatEfficiency(baseEfficiency);
    }

    private void SetupCombatComponent()
    {
        // Check if combat component already exists
        combatComponent = GetComponent<VillagerCombat>();
        if (combatComponent != null) return; // Already has combat component

        // Add appropriate combat component based on role
        switch (role)
        {
            case VillagerRole.Commoner:
                combatComponent = gameObject.AddComponent<CommonerCombat>();
                Debug.Log($"Added CommonerCombat to {gameObject.name}");
                break;

            case VillagerRole.Captain:
                combatComponent = gameObject.AddComponent<CaptainCombat>();
                Debug.Log($"Added CaptainCombat to {gameObject.name}");
                break;

            case VillagerRole.Mage:
                // combatComponent = gameObject.AddComponent<MageCombat>();
                Debug.Log($"MageCombat not yet implemented for {gameObject.name}");
                break;

            case VillagerRole.Builder:
                // Builders don't have combat
                Debug.Log($"Builders don't engage in combat: {gameObject.name}");
                break;

            case VillagerRole.Farmer:
                // Farmers don't have combat
                Debug.Log($"Farmers don't engage in combat: {gameObject.name}");
                break;
        }
    }
    
    private void SetupReferences()
    {
        villageManager = FindObjectOfType<VillageManager>();
        if (villageManager == null)
        {
            Debug.LogWarning($"VillageManager not found for villager {gameObject.name}");
        }
    }
    
    private void UpdateDiscontentOverTime()
    {
        if (Time.time - lastDiscontentUpdate < 0.1f) return; // Update every 0.1 seconds
        lastDiscontentUpdate = Time.time;
        
        // Don't update discontent for rebels
        if (currentState == VillagerState.Rebel) return;
        
        // Calculate discontent rate based on power and food
        float discontentRate = baseDiscontentRate * 0.1f; // Per 0.1 second
        
        // Power reduces discontent rate
        if (stats.tier > 0)
        {
            discontentRate -= (stats.tier * powerDiscontentReduction * 0.1f);
        }
        
        // Low food increases discontent rate
        if (stats.food < 0.3f)
        {
            discontentRate *= lowFoodDiscontentMultiplier;
        }
        
        // Only increase discontent if rate is positive
        if (discontentRate > 0)
        {
            AddDiscontent(discontentRate);
        }
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
        
        // Update health through VillagerHealth component
        if (healthComponent != null)
        {
            healthComponent.UpdateHealthForTier(stats.tier);
        }
        
        Debug.Log($"{role} {gameObject.name}: Allocated {powerAmount} power, Tier {stats.tier}");
        
        // Update combat stats if combat component exists
        if (combatComponent != null)
        {
            combatComponent.UpdateCombatStats();
            
            // Update efficiency considering both food and state
            UpdateFoodEfficiency();
        }
        
        // Update status indicators
        if (statusIndicators != null)
        {
            statusIndicators.UpdatePowerTier(stats.tier);
        }
        
        UpdateVisuals();
    }
    
    public void SetFoodLevel(float foodPercent)
    {
        stats.food = Mathf.Clamp01(foodPercent);
        
        // Update status indicators
        if (statusIndicators != null)
        {
            statusIndicators.UpdateFoodStatus(stats.food < 0.3f);
        }
        
        // Update combat efficiency when food changes
        UpdateFoodEfficiency();
        
        UpdateVisuals();
    }
    
    public void ProcessDiscontentAtAllocation()
    {
        if (settings == null)
        {
            Debug.LogWarning("Settings not assigned!");
            return;
        }
        
        // Calculate power shortage
        int tierCost = stats.tier * 2; // Tier 1 = 2 power, Tier 2 = 4 power
        int powerShortage = Mathf.Max(0, tierCost - stats.power);
        
        // Calculate food shortage
        float foodShortage = Mathf.Max(0, 1f - stats.food);
        
        // Add discontent
        float discontentIncrease = (powerShortage * settings.powerPenalty) + 
                                  (foodShortage * settings.foodPenalty);
        
        AddDiscontent(discontentIncrease);
        
        Debug.Log($"{role} {gameObject.name}: Power shortage: {powerShortage}, Food shortage: {foodShortage:F2}, Discontent added: {discontentIncrease:F1}");
    }
    
    public void ProcessNightlyRecovery()
    {
        if (settings == null) return;
        
        // Recovery if well-fed and full power
        int tierCost = stats.tier * 2;
        if (stats.food >= 1f && stats.power >= tierCost)
        {
            stats.discontent = Mathf.Max(0, stats.discontent - settings.recoveryRate);
            OnDiscontentChanged?.Invoke(this, stats.discontent);
            UpdateVisuals();
        }
    }
    
    public void AddDiscontent(float amount)
    {
        float oldDiscontent = stats.discontent;
        stats.discontent = Mathf.Clamp(stats.discontent + amount, 0f, 100f);
        
        OnDiscontentChanged?.Invoke(this, stats.discontent);
        
        // Check for state changes based on discontent
        UpdateStateBasedOnDiscontent();
        
        UpdateVisuals();
    }
    
    private void UpdateStateBasedOnDiscontent()
    {
        VillagerState newState = currentState;
        
        if (stats.discontent >= 100f)
        {
            if (currentState != VillagerState.Rebel)
            {
                TriggerRebellion();
            }
        }
        else if (stats.discontent >= 50f)
        {
            newState = VillagerState.Angry;
        }
        else
        {
            newState = VillagerState.Loyal;
        }
        
        if (newState != currentState && currentState != VillagerState.Rebel)
        {
            ChangeState(newState);
        }
    }
    
    private void ChangeState(VillagerState newState)
    {
        VillagerState oldState = currentState;
        currentState = newState;
        
        // NEW: If becoming a rebel, clear selection and disable interaction
        if (newState == VillagerState.Rebel)
        {
            SetSelected(false);
        }
        
        // Update combat efficiency considering both state and food
        UpdateFoodEfficiency();
        
        OnStateChanged?.Invoke(this, newState);
        UpdateVisuals();
        
        Debug.Log($"{role} {gameObject.name} state changed: {oldState} -> {newState}");
    }
    
    public void OnHitByPlayerFriendlyFire()
    {
        if (settings != null)
        {
            AddDiscontent(settings.friendlyFirePenalty);
            Debug.Log($"{role} {gameObject.name} hit by friendly fire! +{settings.friendlyFirePenalty} discontent");
        }
    }
    
    public void OnWitnessedVillagerDeath(Villager killedVillager, bool killedByPlayer)
    {
        if (!killedByPlayer) return;
        
        // If a villager witnesses player killing another villager, instant rebellion
        if (currentState != VillagerState.Rebel)
        {
            Debug.Log($"{role} {gameObject.name} witnessed player kill {killedVillager.name}! Instant rebellion!");
            stats.discontent = 100f;
            TriggerRebellion();
        }
    }
    
    public void OnBuildingDestroyed()
    {
        stats.isActive = false;
        
        if (settings != null)
        {
            AddDiscontent(settings.buildingDestroyedPenalty);
            Debug.Log($"{role} {gameObject.name} building destroyed! +{settings.buildingDestroyedPenalty} discontent");
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
        
        // Update AI to rebel mode
        VillagerAI ai = GetComponent<VillagerAI>();
        if (ai != null)
        {
            ai.SetRebel(true);
        }
        
        // Update combat component if exists
        if (combatComponent != null)
        {
            combatComponent.SetCombatEfficiency(1f); // Rebels fight at full efficiency
            combatComponent.UpdateCombatStats();
        }
        
        // Update health component
        if (healthComponent != null)
        {
            healthComponent.ConvertToRebel();
        }
        
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
        
        // Update sprite based on state and selection
        UpdateSelectionVisual(); // NEW: This handles both state and selection
        
        // Update sprite transparency based on active state (but preserve color)
        if (spriteRenderer != null && !isFlashing && !isSelected)
        {
            Color color = originalColor;
            // Only modify alpha, preserve RGB values
            color.a = stats.isActive ? originalColor.a : originalColor.a * 0.5f;
            spriteRenderer.color = color;
        }
        
        // Update status indicators
        if (statusIndicators != null)
        {
            statusIndicators.UpdateDiscontentIndicator(stats.discontent, currentState);
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
    
    // Called by VillagerHealth when villager dies
    public void OnDeath()
    {
        OnVillagerDeath?.Invoke(this);
        Debug.Log($"{role} {gameObject.name} death event triggered");
    }
    
    // Public getters
    public VillagerRole GetRole() => role;
    public VillagerState GetState() => currentState;
    public VillagerStats GetStats() => stats;
    public bool IsActive() => stats.isActive;
    public bool IsLoyal() => currentState == VillagerState.Loyal;
    public bool IsRebel() => currentState == VillagerState.Rebel;
    public bool IsAngry() => currentState == VillagerState.Angry;
    public bool IsSelected() => isSelected; // NEW: Getter for selection state
    public float GetDiscontentPercentage() => stats.discontent / 100f;
    public float GetCombatEfficiency()
    {
        float efficiency = 1f;
        
        // Food efficiency
        if (stats.food < lowFoodEfficiencyThreshold)
        {
            efficiency *= Mathf.Lerp(lowFoodEfficiencyMultiplier, 1f, stats.food / lowFoodEfficiencyThreshold);
        }
        
        // State efficiency
        if (currentState == VillagerState.Angry)
        {
            efficiency *= angryCombatEfficiencyMultiplier;
        }
        
        return efficiency;
    }
    
    // Public setters for testing
    public void SetDiscontent(float amount)
    {
        stats.discontent = Mathf.Clamp(amount, 0f, 100f);
        OnDiscontentChanged?.Invoke(this, stats.discontent);
        UpdateStateBasedOnDiscontent();
        UpdateVisuals();
    }
    
    public void SetRole(VillagerRole newRole)
    {
        role = newRole;
        UpdateVisuals();
    }
    
    // Sprite management methods
    public void SetStateSprites(Sprite loyal, Sprite rebel, Sprite angry = null, Sprite selected = null)
    {
        loyalSprite = loyal;
        rebelSprite = rebel;
        angrySprite = angry;
        if (selected != null) selectedSprite = selected; // NEW: Set selected sprite
        UpdateVisuals();
    }
    
    public void ForceStateUpdate()
    {
        UpdateStateSprite();
    }
    
    // Public methods for UI visibility
    public void SetStatsUIVisible(bool visible)
    {
        statsUIVisible = visible;
        
        // Toggle status indicators - hide when stats UI is visible
        if (statusIndicators != null)
        {
            statusIndicators.SetIndicatorsVisible(!visible);
        }
        
        // NEW: Update selection state based on UI visibility
        SetSelected(visible);
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