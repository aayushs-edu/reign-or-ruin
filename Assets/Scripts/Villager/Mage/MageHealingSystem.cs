using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;
using System.Collections;

public class MageHealingSystem : MonoBehaviour
{
    [Header("AoE Healing Configuration")]
    [SerializeField] private float baseHealRange = 5f;
    [SerializeField] private float healRangePerTier = 1f;
    [SerializeField] private LayerMask villagerLayer = -1;
    [SerializeField] private float healUpdateRate = 0.5f; // Heal every 0.5 seconds
    
    [Header("Tier 1 - Periodic AoE Heal")]
    [SerializeField] private int tier1HealPerSecond = 2;
    [SerializeField] private float tier1HealInterval = 1f; // Every 1 second
    
    [Header("Tier 2 - AoE Heal + Damage Buff")]
    [SerializeField] private int tier2HealPerSecond = 3;
    [SerializeField] private float tier2HealInterval = 1f;
    [SerializeField] private float tier2DamageBuffPercentage = 0.15f; // 15% damage buff
    [SerializeField] private float buffDuration = 2f; // Buff lasts 2 seconds after leaving range
    
    [Header("AoE Visual System")]
    [SerializeField] private Transform aoeContainer; // Parent object containing AoE Circle and AoE Particles
    [SerializeField] private Transform aoeCircle; // The visual circle
    [SerializeField] private ParticleSystem aoeParticles; // Particles emitting from boundary
    [SerializeField] private float circleScaleOffset = 0.5f; // Circle is 0.5 units larger than particle radius
    [SerializeField] private Color tier1Color = new Color(0f, 1f, 0f, 0.3f); // Green for healing
    [SerializeField] private Color tier2Color = new Color(0f, 0.8f, 1f, 0.3f); // Blue-cyan for heal+buff
    [SerializeField] private bool autoFindAoEComponents = true;
    [SerializeField] private bool showHealingRange = true;
    
    [Header("Efficiency Requirements")]
    [SerializeField] private float minimumEfficiencyForHealing = 0.3f; // Need 30% efficiency to heal
    [SerializeField] private float minimumEfficiencyForBuffs = 0.5f; // Need 50% efficiency for damage buffs
    
    [Header("Debug")]
    [SerializeField] private bool debugHealing = false;
    
    // References
    private Villager mage;
    private MageCombat mageCombat;
    
    // Healing system
    private List<VillagerHealth> healTargets = new List<VillagerHealth>();
    private Dictionary<VillagerCombat, float> buffedVillagers = new Dictionary<VillagerCombat, float>(); // VillagerCombat -> buff expiry time
    private float currentHealRange;
    private int currentTier;
    private float lastHealUpdate = 0f;
    private bool isHealingActive = false;
    
    // AoE Visual Components
    private SpriteRenderer aoeCircleRenderer;
    
    private void Awake()
    {
        mage = GetComponent<Villager>();
        mageCombat = GetComponent<MageCombat>();
        
        if (mage == null)
        {
            Debug.LogError($"MageHealingSystem: No Villager component found on {gameObject.name}!");
        }
        
        if (mage.GetRole() != VillagerRole.Mage)
        {
            Debug.LogWarning($"MageHealingSystem: Villager {gameObject.name} is not a Mage!");
        }
    }
    
    private void Start()
    {
        SetupAoEVisualSystem();
        UpdateHealingStats();
    }
    
    private void Update()
    {
        // Update healing system periodically
        if (Time.time - lastHealUpdate >= healUpdateRate)
        {
            UpdateHealingSystem();
            lastHealUpdate = Time.time;
        }
        
        // Update buff expiration
        UpdateBuffExpiration();
    }
    
    public void Initialize(MageCombat combat, Villager villagerComponent)
    {
        mageCombat = combat;
        mage = villagerComponent;
    }
    
    private void SetupAoEVisualSystem()
    {
        // Auto-find AoE components if enabled
        if (autoFindAoEComponents)
        {
            FindAoEComponents();
        }
        
        // Initialize AoE visual components
        InitializeAoEComponents();
        
        // Set initial color based on tier
        UpdateAoEColor();
    }
    
    private void FindAoEComponents()
    {
        // Find AoE container
        if (aoeContainer == null)
        {
            Transform found = transform.Find("AoE");
            if (found == null)
            {
                // Try alternative names
                foreach (Transform child in transform)
                {
                    if (child.name.ToLower().Contains("aoe") || child.name.ToLower().Contains("heal"))
                    {
                        found = child;
                        break;
                    }
                }
            }
            aoeContainer = found;
        }
        
        if (aoeContainer != null)
        {
            // Find AoE Circle
            if (aoeCircle == null)
            {
                Transform circleFound = aoeContainer.Find("AoE Circle");
                if (circleFound == null)
                {
                    // Try alternative names
                    foreach (Transform child in aoeContainer)
                    {
                        if (child.name.ToLower().Contains("circle"))
                        {
                            circleFound = child;
                            break;
                        }
                    }
                }
                aoeCircle = circleFound;
            }
            
            // Find AoE Particles
            if (aoeParticles == null)
            {
                ParticleSystem particlesFound = aoeContainer.GetComponentInChildren<ParticleSystem>();
                if (particlesFound == null)
                {
                    Transform particlesTransform = aoeContainer.Find("AoE Particles");
                    if (particlesTransform != null)
                    {
                        particlesFound = particlesTransform.GetComponent<ParticleSystem>();
                    }
                }
                aoeParticles = particlesFound;
            }
        }
        
        if (debugHealing)
        {
            Debug.Log($"MageHealingSystem: AoE components found - Container: {aoeContainer != null}, " +
                     $"Circle: {aoeCircle != null}, Particles: {aoeParticles != null}");
        }
    }
    
    private void InitializeAoEComponents()
    {
        // Initialize circle renderer
        if (aoeCircle != null)
        {
            aoeCircleRenderer = aoeCircle.GetComponent<SpriteRenderer>();
            if (aoeCircleRenderer == null)
            {
                Debug.LogWarning($"MageHealingSystem: AoE Circle {aoeCircle.name} has no SpriteRenderer!");
            }
        }
        
        // Initialize particle system modules
        if (aoeParticles != null)
        {
            var shape = aoeParticles.shape;
            
            // Ensure shape is set to circle
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radiusThickness = 0f; // Emit from edge only
        }
    }
    
    private void UpdateHealingStats()
    {
        if (mage == null) return;
        
        VillagerStats stats = mage.GetStats();
        currentTier = stats.tier;
        
        // Calculate current heal range based on tier
        currentHealRange = baseHealRange + (stats.tier * healRangePerTier);
        
        // Determine if healing should be active
        float efficiency = mageCombat != null ? mageCombat.GetEfficiency() : 1f;
        isHealingActive = efficiency >= minimumEfficiencyForHealing && currentTier >= 1;
        
        // Update visual indicator
        UpdateAoEVisualSystem();
        
        if (debugHealing)
        {
            Debug.Log($"Mage {gameObject.name} (Tier {currentTier}): Heal range {currentHealRange:F1}, Active: {isHealingActive}, Efficiency: {efficiency:P0}");
        }
    }
    
    private void UpdateHealingSystem()
    {
        UpdateHealingStats();
        
        if (!isHealingActive || currentTier < 1)
        {
            // Clear heal targets and hide visuals
            healTargets.Clear();
            if (aoeContainer != null)
            {
                aoeContainer.gameObject.SetActive(false);
            }
            return;
        }
        
        // Show visuals
        if (aoeContainer != null)
        {
            aoeContainer.gameObject.SetActive(showHealingRange);
        }
        
        // Find all villagers in heal range
        List<VillagerHealth> nearbyVillagers = FindNearbyVillagers();
        
        // Apply healing based on tier
        if (currentTier >= 1)
        {
            ApplyPeriodicHealing(nearbyVillagers);
        }
        
        if (currentTier >= 2)
        {
            ApplyDamageBuffs(nearbyVillagers);
        }
        
        healTargets = nearbyVillagers;
    }
    
    private List<VillagerHealth> FindNearbyVillagers()
    {
        List<VillagerHealth> nearbyVillagers = new List<VillagerHealth>();
        
        // Find all colliders within heal range
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, currentHealRange, villagerLayer);
        
        foreach (var collider in colliders)
        {
            // Skip self
            if (collider.gameObject == gameObject) continue;
            
            // Check if it's a villager
            Villager villagerComponent = collider.GetComponent<Villager>();
            if (villagerComponent != null)
            {
                // Only heal same faction
                bool shouldHeal = false;
                
                if (mage.IsRebel())
                {
                    // Rebel mages heal other rebels
                    shouldHeal = villagerComponent.IsRebel();
                }
                else
                {
                    // Loyal mages heal loyal and angry villagers
                    shouldHeal = villagerComponent.IsLoyal() || villagerComponent.IsAngry();
                }
                
                if (shouldHeal)
                {
                    VillagerHealth villagerHealth = collider.GetComponent<VillagerHealth>();
                    if (villagerHealth != null && villagerHealth.GetCurrentHP() < villagerHealth.GetMaxHP())
                    {
                        nearbyVillagers.Add(villagerHealth);
                    }
                }
            }
        }
        
        return nearbyVillagers;
    }
    
    private void ApplyPeriodicHealing(List<VillagerHealth> targets)
    {
        if (targets.Count == 0) return;
        
        float healInterval = currentTier >= 2 ? tier2HealInterval : tier1HealInterval;
        int healAmount = currentTier >= 2 ? tier2HealPerSecond : tier1HealPerSecond;
        
        // Apply efficiency to heal amount
        if (mageCombat != null)
        {
            healAmount = Mathf.RoundToInt(healAmount * mageCombat.GetEfficiency());
        }
        
        healAmount = Mathf.Max(1, healAmount); // Minimum 1 heal
        
        // Scale heal amount by update rate (since we don't heal every frame)
        int actualHealAmount = Mathf.RoundToInt(healAmount * healUpdateRate / healInterval);
        actualHealAmount = Mathf.Max(1, actualHealAmount);
        
        foreach (var villagerHealth in targets)
        {
            if (villagerHealth != null)
            {
                villagerHealth.Heal(actualHealAmount);
                
                if (debugHealing)
                {
                    Debug.Log($"Mage {gameObject.name} healed {villagerHealth.name} for {actualHealAmount} HP");
                }
            }
        }
        
        if (debugHealing && targets.Count > 0)
        {
            Debug.Log($"Mage {gameObject.name} healed {targets.Count} villagers for {actualHealAmount} HP each");
        }
    }
    
    private void ApplyDamageBuffs(List<VillagerHealth> targets)
    {
        if (targets.Count == 0 || currentTier < 2) return;
        
        // Check if efficiency is high enough for buffs
        float efficiency = mageCombat != null ? mageCombat.GetEfficiency() : 1f;
        if (efficiency < minimumEfficiencyForBuffs) return;
        
        foreach (var villagerHealth in targets)
        {
            if (villagerHealth == null) continue;
            
            // Get the villager's combat component
            VillagerCombat combat = villagerHealth.GetComponent<VillagerCombat>();
            if (combat != null)
            {
                // Apply or refresh damage buff
                float buffExpiryTime = Time.time + buffDuration + healUpdateRate; // Add update rate for buffer
                
                if (!buffedVillagers.ContainsKey(combat))
                {
                    // New buff
                    ApplyBuffToVillager(combat);
                    buffedVillagers[combat] = buffExpiryTime;
                    
                    if (debugHealing)
                    {
                        Debug.Log($"Mage {gameObject.name} applied damage buff to {combat.name}");
                    }
                }
                else
                {
                    // Refresh existing buff
                    buffedVillagers[combat] = buffExpiryTime;
                }
            }
        }
    }
    
    private void ApplyBuffToVillager(VillagerCombat combat)
    {
        // The buff is handled by tracking in the dictionary
        // The actual damage modification would be applied in the villager's combat system
        // For now, we'll use a simple approach where the combat system can query this
        
        // You could extend VillagerCombat to have a method like:
        // combat.ApplyMageDamageBuff(tier2DamageBuffPercentage, buffDuration);
    }
    
    private void UpdateBuffExpiration()
    {
        if (buffedVillagers.Count == 0) return;
        
        // Check for expired buffs
        var expiredBuffs = new List<VillagerCombat>();
        
        foreach (var kvp in buffedVillagers)
        {
            if (kvp.Value <= Time.time || kvp.Key == null)
            {
                expiredBuffs.Add(kvp.Key);
            }
        }
        
        // Remove expired buffs
        foreach (var combat in expiredBuffs)
        {
            buffedVillagers.Remove(combat);
            
            if (debugHealing && combat != null)
            {
                Debug.Log($"Mage {gameObject.name} damage buff expired on {combat.name}");
            }
        }
    }
    
    private void UpdateAoEVisualSystem()
    {
        if (aoeContainer == null) return;
        
        // Show/hide based on settings and activity
        bool shouldShow = showHealingRange && isHealingActive && currentTier >= 1;
        aoeContainer.gameObject.SetActive(shouldShow);
        
        if (!shouldShow) return;
        
        // Update AoE Circle scale
        if (aoeCircle != null)
        {
            float circleScale = currentHealRange + circleScaleOffset; // Diameter
            aoeCircle.localScale = Vector3.one * circleScale;
        }
        
        // Update particle system radius
        if (aoeParticles != null)
        {
            var shape = aoeParticles.shape;
            shape.radius = currentHealRange;
        }
        
        // Update color based on tier
        UpdateAoEColor();
    }
    
    private void UpdateAoEColor()
    {
        Color targetColor = currentTier >= 2 ? tier2Color : tier1Color;
        
        // Update circle color
        if (aoeCircleRenderer != null)
        {
            aoeCircleRenderer.color = targetColor;
        }
        
        // Update particle color
        if (aoeParticles != null)
        {
            var main = aoeParticles.main;
            main.startColor = new ParticleSystem.MinMaxGradient(targetColor);
        }
    }
    
    // Called when mage's tier changes
    public void OnMageTierChanged()
    {
        UpdateHealingStats();
        
        if (debugHealing)
        {
            Debug.Log($"Mage {gameObject.name} tier changed - updating healing system");
        }
    }
    
    // Called when mage becomes rebel
    public void OnMageBecameRebel()
    {
        // Clear all current buffs from loyal villagers
        var allBuffed = new List<VillagerCombat>(buffedVillagers.Keys);
        foreach (var combat in allBuffed)
        {
            if (combat != null)
            {
                Villager v = combat.GetComponent<Villager>();
                if (v != null && !v.IsRebel())
                {
                    buffedVillagers.Remove(combat);
                }
            }
        }
        
        // Update color to rebel colors (could add rebel-specific colors)
        UpdateAoEColor();
    }
    
    // Public getters for other systems
    public float GetHealRange() => currentHealRange;
    public int GetCurrentTier() => currentTier;
    public bool IsHealingActive() => isHealingActive;
    public int GetHealTargetCount() => healTargets.Count;
    public int GetBuffedVillagerCount() => buffedVillagers.Count;
    public List<VillagerHealth> GetHealTargets() => new List<VillagerHealth>(healTargets);
    
    // Check if a villager has damage buff from this mage
    public bool HasDamageBuff(VillagerCombat combat)
    {
        return buffedVillagers.ContainsKey(combat) && buffedVillagers[combat] > Time.time;
    }
    
    public float GetDamageBuffMultiplier()
    {
        return currentTier >= 2 ? (1f + tier2DamageBuffPercentage) : 1f;
    }
    
    // Public methods for AoE visual control
    public void SetAoEColor(Color tier1, Color tier2)
    {
        tier1Color = tier1;
        tier2Color = tier2;
        UpdateAoEColor();
    }
    
    public void SetAoEVisibility(bool visible)
    {
        showHealingRange = visible;
        UpdateAoEVisualSystem();
    }
    
    // Configuration methods
    public void SetHealRange(float baseRange, float rangePerTier)
    {
        baseHealRange = baseRange;
        healRangePerTier = rangePerTier;
        UpdateHealingStats();
    }
    
    public void SetHealingValues(int tier1HPS, int tier2HPS, float damageBuffPercent)
    {
        tier1HealPerSecond = tier1HPS;
        tier2HealPerSecond = tier2HPS;
        tier2DamageBuffPercentage = damageBuffPercent;
    }
    
    private void OnDrawGizmosSelected()
    {
        // Draw heal range
        Color gizmoColor = currentTier >= 2 ? tier2Color : tier1Color;
        gizmoColor.a = 1f; // Full opacity for gizmos
        Gizmos.color = gizmoColor;
        
        if (Application.isPlaying)
        {
            Gizmos.DrawWireSphere(transform.position, currentHealRange);
        }
        else
        {
            float estimatedRange = baseHealRange + (2 * healRangePerTier); // Estimate at tier 2
            Gizmos.DrawWireSphere(transform.position, estimatedRange);
        }
        
        // Draw lines to heal targets
        if (Application.isPlaying && healTargets.Count > 0)
        {
            Gizmos.color = Color.green;
            foreach (var target in healTargets)
            {
                if (target != null)
                {
                    Gizmos.DrawLine(transform.position, target.transform.position);
                }
            }
        }
        
        // Draw lines to buffed villagers (thicker/different color)
        if (Application.isPlaying && buffedVillagers.Count > 0)
        {
            Gizmos.color = Color.blue;
            foreach (var combat in buffedVillagers.Keys)
            {
                if (combat != null && buffedVillagers[combat] > Time.time)
                {
                    // Draw a thicker line by drawing multiple lines slightly offset
                    Vector3 toVillager = combat.transform.position - transform.position;
                    Vector3 perpendicular = Vector3.Cross(toVillager, Vector3.forward).normalized * 0.1f;
                    
                    Gizmos.DrawLine(transform.position + perpendicular, combat.transform.position + perpendicular);
                    Gizmos.DrawLine(transform.position - perpendicular, combat.transform.position - perpendicular);
                    Gizmos.DrawLine(transform.position, combat.transform.position);
                }
            }
        }
    }
    
    private void OnDestroy()
    {
        // Clean up all buffs
        buffedVillagers.Clear();
    }
    
    // Context menu for debugging
    [ContextMenu("Debug Healing System")]
    public void DebugHealingSystem()
    {
        Debug.Log($"Mage {gameObject.name} Healing System:");
        Debug.Log($"  Current Tier: {currentTier}");
        Debug.Log($"  Heal Range: {currentHealRange:F1}");
        Debug.Log($"  Is Active: {isHealingActive}");
        Debug.Log($"  Heal Targets: {healTargets.Count}");
        Debug.Log($"  Buffed Villagers: {buffedVillagers.Count}");
        Debug.Log($"  Combat Efficiency: {(mageCombat != null ? mageCombat.GetEfficiency().ToString("P0") : "N/A")}");
    }
    
    [ContextMenu("Force Heal Nearby")]
    public void ForceHealNearby()
    {
        if (Application.isPlaying)
        {
            var targets = FindNearbyVillagers();
            ApplyPeriodicHealing(targets);
            Debug.Log($"Force healed {targets.Count} nearby villagers");
        }
    }
}