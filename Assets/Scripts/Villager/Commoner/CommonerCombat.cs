using UnityEngine;
using System.Collections;

public class CommonerCombat : VillagerCombat
{
    [Header("Commoner Specific")]
    [SerializeField] private Transform shovelTransform;
    [SerializeField] private GameObject shovelSprite;
    [SerializeField] private GameObject attackFXObject;
    [SerializeField] private float attackAnimationDuration = 0.5f;
    
    [Header("Shovel Positioning")]
    [SerializeField] private float idleRotation = 0f; // Rotation when idle (0 = upright)
    [SerializeField] private float combatReadyDistance = 10f; // Distance at which shovel starts tracking
    [SerializeField] private Vector3 shovelSpriteOffset; // Offset of sprite from pivot point
    
    [Header("Captain Influence")]
    [SerializeField] private bool showInfluenceIndicator = true;
    [SerializeField] private Color influenceIndicatorColor = new Color(0f, 1f, 0f, 0.8f);
    [SerializeField] private GameObject influenceVisualEffect;
    [SerializeField] private float influenceLightIntensity = 2f;
    [SerializeField] private float influenceLightRadius = 1.5f;

    [Header("Debug")]
    [SerializeField] private bool debugCombat = false;
    
    // Cached components
    private Animator shovelAnimator;
    private Animator attackFXAnimator;
    private ShovelDamageDealer shovelDamageDealer;
    private SpriteRenderer villagerSprite;
    private bool isFacingRight = true;
    
    // Captain influence tracking (single captain only)
    private CaptainCombat influencingCaptain;
    private float damageBoost = 0f;
    private float speedBoost = 0f;
    private float damageReduction = 0f;
    private float cooldownReduction = 0f;
    private bool hasInfluence = false;

    // Visual indicator for being influenced
    private GameObject influenceIndicator;
    private UnityEngine.Rendering.Universal.Light2D influenceLight;
    
    protected override void Awake()
    {
        base.Awake();

        // Auto-find shovel hierarchy if not assigned
        if (shovelTransform == null)
        {
            shovelTransform = transform.Find("Shovel");
            if (shovelTransform == null)
            {
                foreach (Transform child in GetComponentsInChildren<Transform>())
                {
                    if (child.name.ToLower().Contains("shovel") && child != transform)
                    {
                        shovelTransform = child;
                        break;
                    }
                }
            }
        }

        // Find shovel sprite and its components
        if (shovelTransform != null)
        {
            // Find shovel sprite
            if (shovelSprite == null)
            {
                Transform sprite = shovelTransform.Find("ShovelSprite");
                if (sprite != null)
                {
                    shovelSprite = sprite.gameObject;
                }
                else
                {
                    // Try to find any child with sprite renderer
                    SpriteRenderer sr = shovelTransform.GetComponentInChildren<SpriteRenderer>();
                    if (sr != null) shovelSprite = sr.gameObject;
                }
            }

            // Get shovel animator
            if (shovelSprite != null)
            {
                shovelAnimator = shovelSprite.GetComponent<Animator>();
                if (shovelAnimator == null && debugCombat)
                {
                    Debug.LogWarning($"CommonerCombat: No Animator found on shovel sprite for {gameObject.name}!");
                }

                // Set up damage dealer component on shovel
                shovelDamageDealer = shovelSprite.GetComponent<ShovelDamageDealer>();
                if (shovelDamageDealer == null)
                {
                    if (debugCombat) Debug.LogWarning($"CommonerCombat: No ShovelDamageDealer found, adding one to {shovelSprite.name}");
                    shovelDamageDealer = shovelSprite.AddComponent<ShovelDamageDealer>();
                }

                if (debugCombat) Debug.Log($"CommonerCombat: Found/Added ShovelDamageDealer on {shovelSprite.name}");

                // Apply sprite offset to position shovel sprite within container
                shovelSprite.transform.localPosition = shovelSpriteOffset;
            }

            // Find attack FX
            if (attackFXObject == null)
            {
                Transform fx = shovelTransform.Find("attackFX");
                if (fx == null)
                {
                    fx = shovelTransform.Find("AttackFX");
                }
                if (fx != null)
                {
                    attackFXObject = fx.gameObject;
                }
            }
        }

        // Get FX animator if exists
        if (attackFXObject != null)
        {
            attackFXAnimator = attackFXObject.GetComponent<Animator>();
        }
    }
    
    protected override void Start()
    {
        base.Start();
        
        CreateInfluenceIndicator();
        // Configure damage dealer
        if (shovelDamageDealer != null)
        {
            shovelDamageDealer.Initialize(this, villager);

            // IMPORTANT: Set initial damage value
            shovelDamageDealer.SetDamage(currentDamage);

            if (debugCombat)
            {
                Debug.Log($"CommonerCombat: Initialized ShovelDamageDealer for {gameObject.name} with {currentDamage} damage");
            }
        }
        else
        {
            Debug.LogError($"CommonerCombat: Could not set up ShovelDamageDealer for {gameObject.name}!");
        }
        
        // Apply initial shovel container position
        if (shovelTransform != null)
        {
            shovelTransform.rotation = Quaternion.Euler(0, 0, idleRotation);
        }
        
        // Ensure sprite is at correct offset
        if (shovelSprite != null)
        {
            shovelSprite.transform.localPosition = shovelSpriteOffset;
        }
    }
    
    protected override void Update()
    {
        base.Update();
        
        // Update shovel rotation based on state
        if (!isAttacking && shovelTransform != null)
        {
            if (currentTarget != null)
            {
                float distanceToTarget = Vector3.Distance(transform.position, currentTarget.position);
                
                // Only rotate shovel when target is within combat ready distance
                if (distanceToTarget <= combatReadyDistance)
                {
                    UpdateShovelRotation();
                }
                else
                {
                    // Target too far, return to idle
                    ReturnToIdleRotation();
                }
            }
            else
            {
                // No target, return to idle rotation
                ReturnToIdleRotation();
            }
        }
    }
    
    private void CreateInfluenceIndicator()
    {
        if (!showInfluenceIndicator) return;

        // Create a child object for the influence light
        GameObject lightObj = new GameObject("InfluenceLight");
        lightObj.transform.SetParent(transform);
        lightObj.transform.localPosition = Vector3.zero;

        // Add Light2D component
        influenceLight = lightObj.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
        
        // Configure the light
        influenceLight.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Point;
        influenceLight.color = influenceIndicatorColor;
        influenceLight.intensity = influenceLightIntensity;
        influenceLight.pointLightOuterRadius = influenceLightRadius;
        influenceLight.pointLightInnerRadius = 0f; // Start from center
        influenceLight.falloffIntensity = 1f; // Smooth falloff
        
        // Start disabled
        influenceLight.enabled = false;

        influenceIndicator = lightObj;

        if (debugCombat)
        {
            Debug.Log($"Created influence light indicator for {gameObject.name}");
        }
    }

    public void ApplyCaptainInfluence(CaptainCombat captain, float damageBoostValue, float speedBoostValue, float damageReductionValue, float cooldownReductionValue)
    {
        if (captain == null) return;

        // Store the influence from the captain
        influencingCaptain = captain;
        damageBoost = damageBoostValue;
        speedBoost = speedBoostValue;
        damageReduction = damageReductionValue;
        cooldownReduction = cooldownReductionValue;
        hasInfluence = true;

        if (debugCombat)
        {
            Debug.Log($"CommonerCombat: {gameObject.name} received captain influence from {captain.name}. " +
                     $"Bonuses: Damage +{damageBoost:P0}, Speed +{speedBoost:P0}, " +
                     $"Damage Reduction {damageReduction:P0}, Cooldown -{cooldownReduction:P0}");
        }

        // Update visuals
        UpdateInfluenceVisuals();

        // Update combat stats with new bonuses
        UpdateCombatStats();

        // Update AI speed if available
        VillagerAI ai = GetComponent<VillagerAI>();
        if (ai != null)
        {
            // Apply speed boost to AI (you may need to add this method to VillagerAI)
            // ai.ApplySpeedMultiplier(1f + speedBoost);
        }
    }

    public void RemoveCaptainInfluence(CaptainCombat captain)
    {
        if (captain == null || influencingCaptain != captain) return;

        // Remove influence
        influencingCaptain = null;
        damageBoost = 0f;
        speedBoost = 0f;
        damageReduction = 0f;
        cooldownReduction = 0f;
        hasInfluence = false;

        if (debugCombat)
        {
            Debug.Log($"CommonerCombat: {gameObject.name} lost captain influence from {captain.name}");
        }

        // Update visuals
        UpdateInfluenceVisuals();

        // Update combat stats without bonuses
        UpdateCombatStats();

        // Update AI speed
        VillagerAI ai = GetComponent<VillagerAI>();
        if (ai != null)
        {
            // Reset speed multiplier
            // ai.ApplySpeedMultiplier(1f);
        }
    }

    private void UpdateInfluenceVisuals()
    {
        if (influenceLight != null)
        {
            influenceLight.enabled = hasInfluence;

            if (hasInfluence)
            {
                // Pulse the light intensity based on influence strength
                float pulseSpeed = 2f + damageBoost * 3f;
                float basePulse = Mathf.Sin(Time.time * pulseSpeed) * 0.3f + 0.7f; // 0.4 to 1.0 range
                float finalIntensity = influenceLightIntensity * basePulse * (1f + damageBoost);
                
                influenceLight.intensity = finalIntensity;

                // Adjust color intensity based on influence strength
                Color lightColor = influenceIndicatorColor;
                lightColor.a = Mathf.Clamp01(0.5f + damageBoost * 0.5f); // More influence = more opaque
                influenceLight.color = lightColor;

                // Slightly vary the radius based on bonuses
                float radiusMultiplier = 1f + (damageBoost + speedBoost) * 0.2f;
                influenceLight.pointLightOuterRadius = influenceLightRadius * radiusMultiplier;
            }
        }

        // Spawn visual effect if configured
        if (influenceVisualEffect != null && hasInfluence)
        {
            // You can spawn particle effects or other visuals here
        }
    }
    
    private void UpdateShovelRotation()
    {
        if (currentTarget == null || shovelTransform == null) return;

        Vector2 direction = (currentTarget.position - shovelTransform.position).normalized;
        shovelTransform.right = direction;

        Vector2 scale = shovelTransform.localScale;
        if (direction.x < 0) 
        {
            scale.y = -1;
        }
        else
        {
            scale.y = 1;
        }
        shovelTransform.localScale = scale;
    }
    
    private void ReturnToIdleRotation()
    {
        shovelTransform.localScale = Vector3.one;
        Quaternion targetRotation = Quaternion.Euler(0, 0, idleRotation);
        shovelTransform.rotation = Quaternion.Lerp(shovelTransform.rotation, targetRotation, 5 * Time.deltaTime);
    }
    
    protected override IEnumerator PerformAttack()
    {
        if (debugCombat)
        {
            Debug.Log($"CommonerCombat: {gameObject.name} starting attack on {currentTarget?.name}");
        }
        
        isAttacking = true;
        lastAttackTime = Time.time;
        
        // Trigger shovel attack animation FIRST
        if (shovelAnimator != null)
        {
            shovelAnimator.SetTrigger("Attack");
            
            // Get actual animation length if possible
            AnimatorClipInfo[] clipInfo = shovelAnimator.GetCurrentAnimatorClipInfo(0);
            if (clipInfo.Length > 0)
            {
                attackAnimationDuration = clipInfo[0].clip.length;
            }
        }
        else if (debugCombat)
        {
            Debug.LogWarning($"CommonerCombat: No shovel animator found on {gameObject.name}!");
        }
        
        // Trigger attack FX animation
        if (attackFXAnimator != null)
        {
            yield return new WaitForSeconds(0.1f); // Small delay for FX
            attackFXAnimator.SetTrigger("Play");
        }
        
        // Wait for animation to complete
        yield return new WaitForSeconds(attackAnimationDuration);
        
        isAttacking = false;
        
        if (debugCombat)
        {
            Debug.Log($"CommonerCombat: {gameObject.name} finished attack");
        }
    }
    
    public override void UpdateCombatStats()
    {
        // Call base implementation first
        base.UpdateCombatStats();
        
        if (hasInfluence)
        {
            // Apply damage boost
            currentDamage = Mathf.RoundToInt(currentDamage * (1f + damageBoost));

            // Apply cooldown reduction
            currentAttackCooldown *= (1f - cooldownReduction);
            currentAttackCooldown = Mathf.Max(0.2f, currentAttackCooldown); // Minimum cooldown

            // Range doesn't get boosted directly, but could be added if desired
            // currentAttackRange *= (1f + someRangeBoost);
        }
        
        // Update damage dealer with new stats
        if (shovelDamageDealer != null)
        {
            shovelDamageDealer.SetDamage(currentDamage);

            if (debugCombat)
            {
                Debug.Log($"CommonerCombat: Updated {gameObject.name} damage to {currentDamage}");
            }
        }
        
        // Commoner-specific stat adjustments
        if (villager != null)
        {
            VillagerStats stats = villager.GetStats();
            
            // Tier 2 gets faster attacks
            if (stats.tier >= 2)
            {
                currentAttackCooldown *= 0.8f; // 20% faster attacks at tier 2
            }
        }
    }

    // IMPORTANT: Ensure proper cleanup
    protected override void OnDestroy()
    {
        base.OnDestroy();
        
        // Clean up influence indicator
        if (influenceIndicator != null)
        {
            Destroy(influenceIndicator);
        }
    }
    
    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        
        // Draw shovel pivot point
        if (shovelTransform != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(shovelTransform.position, 0.1f);
            
            // Draw line showing shovel direction
            Vector3 shovelDirection = shovelTransform.rotation * Vector3.right;
            Gizmos.DrawRay(shovelTransform.position, shovelDirection * 1f);
            
            // Draw shovel sprite position
            if (Application.isPlaying && shovelSprite != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(shovelSprite.transform.position, 0.05f);
                Gizmos.DrawLine(shovelTransform.position, shovelSprite.transform.position);
            }
        }
        
        // Draw combat ready distance
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, combatReadyDistance);
    }

    // Public getters for debugging and UI
    public bool HasCaptainInfluence() => hasInfluence;
    public float GetDamageBoost() => damageBoost;
    public float GetSpeedBoost() => speedBoost;
    public float GetDamageReduction() => damageReduction;
    public float GetCooldownReduction() => cooldownReduction;
    public CaptainCombat GetInfluencingCaptain() => influencingCaptain;
    
    // Public methods for influence light control
    public void SetInfluenceLightColor(Color newColor)
    {
        influenceIndicatorColor = newColor;
        if (influenceLight != null)
        {
            influenceLight.color = newColor;
        }
    }
    
    public void SetInfluenceLightIntensity(float intensity)
    {
        influenceLightIntensity = intensity;
        UpdateInfluenceVisuals(); // Refresh visuals with new intensity
    }
    
    public void SetInfluenceLightRadius(float radius)
    {
        influenceLightRadius = radius;
        if (influenceLight != null)
        {
            influenceLight.pointLightOuterRadius = radius;
        }
    }

    // Context menu methods for testing
    [ContextMenu("Debug Captain Influence")]
    public void DebugCaptainInfluence()
    {
        Debug.Log($"CommonerCombat {gameObject.name} Captain Influence:");
        Debug.Log($"  Has Influence: {hasInfluence}");
        Debug.Log($"  Damage Boost: +{damageBoost:P0}");
        Debug.Log($"  Speed Boost: +{speedBoost:P0}");
        Debug.Log($"  Damage Reduction: {damageReduction:P0}");
        Debug.Log($"  Cooldown Reduction: -{cooldownReduction:P0}");
        Debug.Log($"  Influencing Captain: {(influencingCaptain != null ? influencingCaptain.name : "None")}");
        Debug.Log($"  Light Enabled: {(influenceLight != null ? influenceLight.enabled.ToString() : "No Light")}");
    }
    
    [ContextMenu("Test Influence Light")]
    public void TestInfluenceLight()
    {
        if (influenceLight != null)
        {
            // Toggle the light for testing
            influenceLight.enabled = !influenceLight.enabled;
            Debug.Log($"Influence light toggled to: {influenceLight.enabled}");
        }
        else
        {
            Debug.LogWarning("No influence light found!");
        }
    }
    
    // DEBUGGING METHODS
    [ContextMenu("Test Attack Animation")]
    public void TestAttackAnimation()
    {
        if (Application.isPlaying)
        {
            StartCoroutine(PerformAttack());
        }
    }
    
    [ContextMenu("Force Update Combat Stats")]
    public void ForceUpdateCombatStats()
    {
        UpdateCombatStats();
        Debug.Log($"Combat stats updated for {gameObject.name}: Damage={currentDamage}, Cooldown={currentAttackCooldown}, Range={currentAttackRange}");
    }
}