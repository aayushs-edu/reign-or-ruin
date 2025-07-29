using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CaptainCombat : VillagerCombat
{
    [Header("Captain Specific")]
    [SerializeField] private Transform swordTransform;
    [SerializeField] private GameObject swordSprite;
    [SerializeField] private GameObject attackFXObject;
    [SerializeField] private float attackAnimationDuration = 0.4f;

    [Header("Sword Positioning")]
    [SerializeField] private float idleRotation = 0f; // Rotation when idle (0 = upright)
    [SerializeField] private float combatReadyDistance = 20f; // Distance at which sword starts tracking
    [SerializeField] private Vector3 swordSpriteOffset; // Offset of sprite from pivot point
    
    [Header("Influence System")]
    [SerializeField] private float baseInfluenceRadius = 8f;
    [SerializeField] private float influenceRadiusPerTier = 3f;
    [SerializeField] private LayerMask commonerLayer = -1;
    
    [Header("Commoner Stat Boosts")]
    [SerializeField] private float baseDamageBoost = 0.25f; // 25% damage boost at tier 0
    [SerializeField] private float damageBoostPerTier = 0.25f; // Additional 25% per tier
    [SerializeField] private float baseSpeedBoost = 0.15f; // 15% speed boost at tier 0
    [SerializeField] private float speedBoostPerTier = 0.15f; // Additional 15% per tier
    [SerializeField] private float baseDamageReduction = 0.1f; // 10% damage reduction at tier 0
    [SerializeField] private float damageReductionPerTier = 0.1f; // Additional 10% per tier
    [SerializeField] private float baseCooldownReduction = 0.1f; // 10% cooldown reduction at tier 0
    
    [Header("Visual Effects")]
    [SerializeField] private GameObject influenceIndicatorPrefab;
    [SerializeField] private Color influenceRangeColor = new Color(0f, 1f, 0f, 0.2f);
    [SerializeField] private bool showInfluenceRange = true;
    
    [Header("AoE Visual System")]
    [SerializeField] private Transform aoeContainer; // Parent object containing AoE Circle and AoE Particles
    [SerializeField] private Transform aoeCircle; // The visual circle
    [SerializeField] private ParticleSystem aoeParticles; // Particles emitting from boundary
    [SerializeField] private float circleScaleOffset = 0.5f; // Circle is 0.5 units larger than particle radius
    [SerializeField] private Color aoeColor = new Color(0f, 1f, 0f, 0.3f); // Color for entire AoE system
    [SerializeField] private bool autoFindAoEComponents = true;
    
    [Header("Debug")]
    [SerializeField] private bool debugCombat = false;
    [SerializeField] private bool debugInfluence = false;
    
    // Cached components
    private Animator swordAnimator;
    private SwordDamageDealer swordDamageDealer;
    
    // Influence system
    private List<CommonerCombat> influencedCommoners = new List<CommonerCombat>();
    private float currentInfluenceRadius;
    private GameObject influenceIndicator;
    private float lastInfluenceUpdate = 0f;
    private const float influenceUpdateRate = 0.2f; // Update 5 times per second
    
    // AoE Visual Components
    private SpriteRenderer aoeCircleRenderer;
    
    protected override void Awake()
    {
        base.Awake();
        
        // Auto-find sword hierarchy if not assigned
        if (swordTransform == null)
        {
            swordTransform = transform.Find("Sword");
            if (swordTransform == null)
            {
                foreach (Transform child in GetComponentsInChildren<Transform>())
                {
                    if (child.name.ToLower().Contains("sword") && child != transform)
                    {
                        swordTransform = child;
                        break;
                    }
                }
            }
        }
        
        // Find sword sprite and its components
        if (swordTransform != null)
        {
            // Find sword sprite
            if (swordSprite == null)
            {
                Transform sprite = swordTransform.Find("SwordSprite");
                if (sprite != null)
                {
                    swordSprite = sprite.gameObject;
                }
                else
                {
                    // Try to find any child with sprite renderer
                    SpriteRenderer sr = swordTransform.GetComponentInChildren<SpriteRenderer>();
                    if (sr != null) swordSprite = sr.gameObject;
                }
            }
            
            // Get sword animator
            if (swordSprite != null)
            {
                swordAnimator = swordSprite.GetComponent<Animator>();
                if (swordAnimator == null && debugCombat)
                {
                    Debug.LogWarning($"CaptainCombat: No Animator found on sword sprite for {gameObject.name}!");
                }
                
                // Set up damage dealer component on sword
                swordDamageDealer = swordSprite.GetComponent<SwordDamageDealer>();
                if (swordDamageDealer == null)
                {
                    if (debugCombat) Debug.LogWarning($"CaptainCombat: No SwordDamageDealer found, adding one to {swordSprite.name}");
                    swordDamageDealer = swordSprite.AddComponent<SwordDamageDealer>();
                }
                
                if (debugCombat) Debug.Log($"CaptainCombat: Found/Added SwordDamageDealer on {swordSprite.name}");
                
                // Apply sprite offset to position sword sprite within container
                swordSprite.transform.localPosition = swordSpriteOffset;
            }
            
            // Find attack FX
            if (attackFXObject == null)
            {
                Transform fx = swordTransform.Find("attackFX");
                if (fx == null)
                {
                    fx = swordTransform.Find("AttackFX");
                }
                if (fx != null)
                {
                    attackFXObject = fx.gameObject;
                }
            }
        }
        
        if (swordTransform == null)
        {
            Debug.LogError($"CaptainCombat: No sword transform found on {gameObject.name}!");
        }
    }
    
    protected override void Start()
    {
        base.Start();
        
        // Configure damage dealer
        if (swordDamageDealer != null)
        {
            swordDamageDealer.Initialize(this, villager);
            
            // IMPORTANT: Set initial damage value
            swordDamageDealer.SetDamage(currentDamage);
            
            if (debugCombat)
            {
                Debug.Log($"CaptainCombat: Initialized SwordDamageDealer for {gameObject.name} with {currentDamage} damage");
            }
        }
        else
        {
            Debug.LogError($"CaptainCombat: Could not set up SwordDamageDealer for {gameObject.name}!");
        }
        
        // Apply initial sword container position
        if (swordTransform != null)
        {
            swordTransform.rotation = Quaternion.Euler(0, 0, idleRotation);
        }
        
        // Ensure sprite is at correct offset
        if (swordSprite != null)
        {
            swordSprite.transform.localPosition = swordSpriteOffset;
        }
        
        // Create influence indicator
        SetupAoEVisualSystem();
        
        // Initial influence update
        UpdateInfluenceSystem();
    }
    
    protected override void Update()
    {
        base.Update();

        // Update sword rotation based on state (matching CommonerCombat pattern)
        if (!isAttacking && swordTransform != null)
        {
            if (currentTarget != null)
            {
                float distanceToTarget = Vector3.Distance(transform.position, currentTarget.position);

                // Only rotate sword when target is within combat ready distance
                if (distanceToTarget <= combatReadyDistance)
                {
                    UpdateSwordRotation();
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

        // Update influence system periodically
        if (Time.time - lastInfluenceUpdate >= influenceUpdateRate)
        {
            UpdateInfluenceSystem();
            lastInfluenceUpdate = Time.time;
        }
    }

    // FIXED: Combined facing and weapon rotation logic with proper sprite flipping
    private void UpdateSwordRotation()
    {
        if (currentTarget == null || swordTransform == null) return;

        Vector2 direction = (currentTarget.position - swordTransform.position).normalized;
        swordTransform.right = direction;

        Vector2 scale = swordTransform.localScale;
        if (direction.x < 0) 
        {
            scale.y = -1;
        }
        else
        {
            scale.y = 1;
        }
        swordTransform.localScale = scale;
    }
    
    private void ReturnToIdleRotation()
    {
        swordTransform.localScale = Vector3.one;
        Quaternion targetRotation = Quaternion.Euler(0, 0, idleRotation);
        swordTransform.rotation = Quaternion.Lerp(swordTransform.rotation, targetRotation, 5 * Time.deltaTime);
    }
    

    
    // CRITICAL FIX: Proper attack timing with damage window
    protected override IEnumerator PerformAttack()
    {
        if (debugCombat)
        {
            Debug.Log($"CaptainCombat: {gameObject.name} starting attack on {currentTarget?.name}");
        }

        isAttacking = true;
        lastAttackTime = Time.time;

        // Trigger sword attack animation FIRST
        if (swordAnimator != null)
        {
            swordAnimator.SetTrigger("Attack");
        }
        else if (debugCombat)
        {
            Debug.LogWarning($"CaptainCombat: No sword animator found on {gameObject.name}!");
        }

        // Wait for animation to complete
        yield return new WaitForSeconds(attackAnimationDuration);

        isAttacking = false;

        if (debugCombat)
        {
            Debug.Log($"CaptainCombat: {gameObject.name} finished attack");
        }
    }
    
    // Rest of the influence system methods remain the same...
    private void SetupAoEVisualSystem()
    {
        // Auto-find AoE components if enabled
        if (autoFindAoEComponents)
        {
            FindAoEComponents();
        }
        
        // Initialize AoE visual components
        InitializeAoEComponents();
        
        // Set initial color
        SetAoEColor(aoeColor);
        
        // Create fallback influence indicator if no AoE system found
        if (aoeContainer == null && showInfluenceRange)
        {
            CreateInfluenceIndicator();
        }
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
                    if (child.name.ToLower().Contains("aoe") || child.name.ToLower().Contains("influence"))
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
        
        if (debugInfluence)
        {
            Debug.Log($"CaptainCombat: AoE components found - Container: {aoeContainer != null}, " +
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
                Debug.LogWarning($"CaptainCombat: AoE Circle {aoeCircle.name} has no SpriteRenderer!");
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
    
    private void UpdateInfluenceIndicator()
    {
        // Update AoE visual system
        UpdateAoEVisualSystem();
        
        // Update fallback influence indicator if it exists
        if (influenceIndicator == null) return;
        
        // Update the circle size
        LineRenderer lr = influenceIndicator.GetComponent<LineRenderer>();
        if (lr != null)
        {
            int segments = lr.positionCount - 1;
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector3 pos = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * currentInfluenceRadius;
                lr.SetPosition(i, pos);
            }
        }
    }
    
    private void UpdateAoEVisualSystem()
    {
        if (aoeContainer == null) return;
        
        // Show/hide based on settings
        aoeContainer.gameObject.SetActive(showInfluenceRange);
        
        if (!showInfluenceRange) return;
        
        // Update AoE Circle scale
        if (aoeCircle != null)
        {
            float circleScale = currentInfluenceRadius + circleScaleOffset; // Diameter
            aoeCircle.localScale = Vector3.one * circleScale;
            
            if (debugInfluence)
            {
                Debug.Log($"Updated AoE Circle scale to {circleScale} (radius: {currentInfluenceRadius + circleScaleOffset})");
            }
        }
        
        // Update particle system radius
        if (aoeParticles != null)
        {
            var shape = aoeParticles.shape; // Get a fresh ShapeModule
            shape.radius = currentInfluenceRadius;
            
            if (debugInfluence)
            {
                Debug.Log($"Updated AoE Particles radius to {currentInfluenceRadius}");
            }
        }
    }
    
    private void UpdateInfluenceSystem()
    {
        if (villager == null) return;
        
        // Calculate current influence radius based on tier
        VillagerStats stats = villager.GetStats();
        currentInfluenceRadius = baseInfluenceRadius + (stats.tier * influenceRadiusPerTier);
        
        // Update visual indicator
        UpdateInfluenceIndicator();
        
        // Find all commoners in range
        List<CommonerCombat> nearbyCommoners = FindNearbyCommoners();
        
        // Remove influence from commoners no longer in range
        foreach (var commoner in influencedCommoners.ToArray())
        {
            if (!nearbyCommoners.Contains(commoner) || commoner == null)
            {
                RemoveInfluenceFromCommoner(commoner);
            }
        }
        
        // Add influence to new commoners in range
        foreach (var commoner in nearbyCommoners)
        {
            if (!influencedCommoners.Contains(commoner))
            {
                ApplyInfluenceToCommoner(commoner);
            }
        }
        
        if (debugInfluence && influencedCommoners.Count > 0)
        {
            Debug.Log($"Captain {gameObject.name} influencing {influencedCommoners.Count} commoners within {currentInfluenceRadius:F1} radius");
        }
    }
    
    private List<CommonerCombat> FindNearbyCommoners()
    {
        List<CommonerCombat> nearbyCommoners = new List<CommonerCombat>();
        
        // Find all colliders within influence radius
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, currentInfluenceRadius, commonerLayer);
        
        foreach (var collider in colliders)
        {
            // Skip self
            if (collider.gameObject == gameObject) continue;
            
            // Check if it's a commoner
            Villager villagerComponent = collider.GetComponent<Villager>();
            if (villagerComponent != null && villagerComponent.GetRole() == VillagerRole.Commoner)
            {
                // Only influence loyal commoners (not rebels)
                if (villagerComponent.IsLoyal() || villagerComponent.IsAngry())
                {
                    CommonerCombat commonerCombat = collider.GetComponent<CommonerCombat>();
                    if (commonerCombat != null)
                    {
                        nearbyCommoners.Add(commonerCombat);
                    }
                }
            }
        }
        
        return nearbyCommoners;
    }
    
    private void ApplyInfluenceToCommoner(CommonerCombat commoner)
    {
        if (commoner == null || villager == null) return;
        
        VillagerStats stats = villager.GetStats();
        
        // Calculate boost values based on captain's tier
        float damageBoost = baseDamageBoost + (stats.tier * damageBoostPerTier);
        float speedBoost = baseSpeedBoost + (stats.tier * speedBoostPerTier);
        float damageReduction = baseDamageReduction + (stats.tier * damageReductionPerTier);
        float cooldownReduction = baseCooldownReduction + (stats.tier * cooldownReductionPerTier);
        
        // Apply influence
        commoner.ApplyCaptainInfluence(this, damageBoost, speedBoost, damageReduction, cooldownReduction);
        
        // Add to influenced list
        influencedCommoners.Add(commoner);
        
        if (debugInfluence)
        {
            Debug.Log($"Captain {gameObject.name} (Tier {stats.tier}) applied influence to {commoner.name}: " +
                     $"Damage +{damageBoost:P0}, Speed +{speedBoost:P0}, Damage Reduction {damageReduction:P0}, Cooldown -{cooldownReduction:P0}");
        }
    }
    
    private void RemoveInfluenceFromCommoner(CommonerCombat commoner)
    {
        if (commoner == null) return;
        
        // Remove influence
        commoner.RemoveCaptainInfluence(this);
        
        // Remove from influenced list
        influencedCommoners.Remove(commoner);
        
        if (debugInfluence)
        {
            Debug.Log($"Captain {gameObject.name} removed influence from {commoner.name}");
        }
    }
    
    // IMPORTANT: Ensure proper cleanup
    protected override void OnDestroy()
    {
        base.OnDestroy();
        
        // Remove influence from all commoners
        foreach (var commoner in influencedCommoners.ToArray())
        {
            RemoveInfluenceFromCommoner(commoner);
        }
        
        // Clean up influence indicator
        if (influenceIndicator != null)
        {
            Destroy(influenceIndicator);
        }
    }
    
    private void CreateInfluenceIndicator()
    {
        if (!showInfluenceRange) return;
        
        if (influenceIndicatorPrefab != null)
        {
            influenceIndicator = Instantiate(influenceIndicatorPrefab, transform);
            influenceIndicator.transform.localPosition = Vector3.zero;
        }
        else
        {
            // Create a simple circle indicator
            GameObject indicatorObj = new GameObject("InfluenceIndicator");
            indicatorObj.transform.SetParent(transform);
            indicatorObj.transform.localPosition = Vector3.zero;
            
            // Add a simple circle renderer
            LineRenderer lr = indicatorObj.AddComponent<LineRenderer>();
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = influenceRangeColor;
            lr.endColor = influenceRangeColor;
            lr.startWidth = 0.1f;
            lr.endWidth = 0.1f;
            lr.useWorldSpace = false;
            
            // Create circle points
            int segments = 64;
            lr.positionCount = segments + 1;
            
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector3 pos = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * currentInfluenceRadius;
                lr.SetPosition(i, pos);
            }
            
            influenceIndicator = indicatorObj;
        }
    }
    
    // Public getters for other systems
    public float GetCurrentInfluenceRadius() => currentInfluenceRadius;
    public int GetInfluencedCommonersCount() => influencedCommoners.Count;
    public List<CommonerCombat> GetInfluencedCommoners() => new List<CommonerCombat>(influencedCommoners);
    public Color GetAoEColor() => aoeColor;
    public bool IsAoEVisible() => showInfluenceRange;
    public float GetCircleScaleOffset() => circleScaleOffset;
    
    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        // Draw sword pivot point
        if (swordTransform != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(swordTransform.position, 0.1f);

            // Draw line showing sword direction
            Vector3 swordDirection = swordTransform.rotation * Vector3.right;
            Gizmos.DrawRay(swordTransform.position, swordDirection * 1f);

            // Draw sword sprite position
            if (Application.isPlaying && swordSprite != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(swordSprite.transform.position, 0.05f);
                Gizmos.DrawLine(swordTransform.position, swordSprite.transform.position);
            }
        }

        // Draw combat ready distance
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, combatReadyDistance);

        // Draw influence radius
        Gizmos.color = influenceRangeColor;
        if (Application.isPlaying)
        {
            Gizmos.DrawWireSphere(transform.position, currentInfluenceRadius);
        }
        else
        {
            float estimatedRadius = baseInfluenceRadius + (2 * influenceRadiusPerTier); // Estimate at tier 2
            Gizmos.DrawWireSphere(transform.position, estimatedRadius);
        }

        // Draw lines to influenced commoners
        if (Application.isPlaying && influencedCommoners.Count > 0)
        {
            Gizmos.color = Color.blue;
            foreach (var commoner in influencedCommoners)
            {
                if (commoner != null)
                {
                    Gizmos.DrawLine(transform.position, commoner.transform.position);
                }
            }
        }

        // ADDED: Draw damage window visualization
        if (Application.isPlaying && debugCombat && isAttacking)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 0.3f);
        }
    }
    
    // Public methods for AoE visual control
    public void SetAoEColor(Color newColor)
    {
        aoeColor = newColor;
        
        // Update circle color
        if (aoeCircleRenderer != null)
        {
            aoeCircleRenderer.color = newColor;
        }
        
        // Update particle color
        if (aoeParticles != null)
        {
            var main = aoeParticles.main;
            main.startColor = new ParticleSystem.MinMaxGradient(newColor);
        }
        
        if (debugInfluence)
        {
            Debug.Log($"Captain {gameObject.name} AoE color set to {newColor}");
        }
    }
    
    public void SetAoEVisibility(bool visible)
    {
        showInfluenceRange = visible;
        
        if (aoeContainer != null)
        {
            aoeContainer.gameObject.SetActive(visible);
        }
        
        if (influenceIndicator != null)
        {
            influenceIndicator.SetActive(visible);
        }
    }
    
    public void SetCircleScaleOffset(float offset)
    {
        circleScaleOffset = offset;
        UpdateAoEVisualSystem(); // Immediately update visuals
    }
    
}