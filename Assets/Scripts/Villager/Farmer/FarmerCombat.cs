using UnityEngine;
using System.Collections;

public enum FarmerMode
{
    Farm,     // Default: Wander farm, flee from enemies, produce food
    Combat    // Combat mode: Fight like a commoner
}

public class FarmerCombat : VillagerCombat
{
    [Header("Farmer Specific")]
    [SerializeField] private Transform hoeTransform;
    [SerializeField] private GameObject hoeSprite;
    [SerializeField] private GameObject attackFXObject;
    [SerializeField] private float attackAnimationDuration = 0.5f;
    
    [Header("Farmer Mode")]
    [SerializeField] private FarmerMode currentMode = FarmerMode.Farm;
    [SerializeField] private bool allowModeToggle = true;
    [SerializeField] private KeyCode toggleModeKey = KeyCode.M;
    
    [Header("Hoe Positioning")]
    [SerializeField] private float idleRotation = 0f; // Rotation when idle (0 = upright)
    [SerializeField] private float combatReadyDistance = 10f; // Distance at which hoe starts tracking
    [SerializeField] private Vector3 hoeSpriteOffset; // Offset of sprite from pivot point
    
    [Header("Farm Mode Settings")]
    [SerializeField] private float farmWanderRadius = 8f;
    [SerializeField] private float farmWanderSpeed = 1.5f;
    [SerializeField] private float fleeSpeedMultiplier = 1.8f;
    [SerializeField] private float fleeDistance = 12f;
    [SerializeField] private float maxDistanceFromFarm = 1.5f; // Multiplier of farmWanderRadius for max flee distance
    [SerializeField] private LayerMask enemyDetectionLayers = -1;
    
    [Header("Food Production")]
    [SerializeField] private int baseFoodProduction = 2;
    [SerializeField] private int tier1FoodProduction = 4;
    [SerializeField] private int tier2FoodProduction = 8;
    [SerializeField] private bool debugFoodProduction = false;
    
    [Header("Combat Mode Indicators")]
    [SerializeField] private GameObject combatModeIndicator; // Visual indicator when in combat mode
    [SerializeField] private Color farmModeColor = new Color(0f, 1f, 0f, 0.8f);
    [SerializeField] private Color combatModeColor = new Color(1f, 0f, 0f, 0.8f);
    [SerializeField] private UnityEngine.Rendering.Universal.Light2D modeLight;
    
    [Header("Debug")]
    [SerializeField] private bool debugMode = false;
    
    // Cached components
    private Animator hoeAnimator;
    private HoeDamageDealer hoeDamageDealer;
    private VillagerAI farmerAI;
    private Transform farmCenter; // Where farmer should wander around
    
    // Farm mode state
    private Vector3 currentWanderTarget;
    private float lastWanderTime;
    private float wanderInterval = 3f;
    private bool isFleeing = false;
    private Transform fleeFrom;
    
    // Mode switching
    private bool canToggleMode = true;
    private float modeToggleCooldown = 1f;
    private float lastModeToggle;
    
    protected override void Awake()
    {
        base.Awake();
        
        farmerAI = GetComponent<VillagerAI>();
        
        // Auto-find hoe hierarchy if not assigned
        if (hoeTransform == null)
        {
            hoeTransform = transform.Find("Hoe");
            if (hoeTransform == null)
            {
                foreach (Transform child in GetComponentsInChildren<Transform>())
                {
                    if (child.name.ToLower().Contains("hoe") && child != transform)
                    {
                        hoeTransform = child;
                        break;
                    }
                }
            }
        }
        
        // Find hoe sprite and its components
        if (hoeTransform != null)
        {
            // Find hoe sprite
            if (hoeSprite == null)
            {
                Transform sprite = hoeTransform.Find("HoeSprite");
                if (sprite != null)
                {
                    hoeSprite = sprite.gameObject;
                }
                else
                {
                    // Try to find any child with sprite renderer
                    SpriteRenderer sr = hoeTransform.GetComponentInChildren<SpriteRenderer>();
                    if (sr != null) hoeSprite = sr.gameObject;
                }
            }
            
            // Get hoe animator
            if (hoeSprite != null)
            {
                hoeAnimator = hoeSprite.GetComponent<Animator>();
                if (hoeAnimator == null && debugMode)
                {
                    Debug.LogWarning($"FarmerCombat: No Animator found on hoe sprite for {gameObject.name}!");
                }
                
                // Set up damage dealer component on hoe
                hoeDamageDealer = hoeSprite.GetComponent<HoeDamageDealer>();
                if (hoeDamageDealer == null)
                {
                    if (debugMode) Debug.LogWarning($"FarmerCombat: No HoeDamageDealer found, adding one to {hoeSprite.name}");
                    hoeDamageDealer = hoeSprite.AddComponent<HoeDamageDealer>();
                }
                
                if (debugMode) Debug.Log($"FarmerCombat: Found/Added HoeDamageDealer on {hoeSprite.name}");
                
                // Apply sprite offset to position hoe sprite within container
                hoeSprite.transform.localPosition = hoeSpriteOffset;
            }
            
            // Find attack FX
            if (attackFXObject == null)
            {
                Transform fx = hoeTransform.Find("attackFX");
                if (fx == null)
                {
                    fx = hoeTransform.Find("AttackFX");
                }
                if (fx != null)
                {
                    attackFXObject = fx.gameObject;
                }
            }
        }
        
        if (hoeTransform == null)
        {
            Debug.LogError($"FarmerCombat: No hoe transform found on {gameObject.name}!");
        }
        
        // Set farm center to current position by default
        farmCenter = transform;
    }
    
    protected override void Start()
    {
        base.Start();
        
        // Configure damage dealer for combat mode
        if (hoeDamageDealer != null)
        {
            hoeDamageDealer.Initialize(this, villager);
            hoeDamageDealer.SetDamage(currentDamage);
            
            if (debugMode)
            {
                Debug.Log($"FarmerCombat: Initialized HoeDamageDealer for {gameObject.name} with {currentDamage} damage");
            }
        }
        else
        {
            Debug.LogError($"FarmerCombat: Could not set up HoeDamageDealer for {gameObject.name}!");
        }
        
        // Apply initial hoe container position
        if (hoeTransform != null)
        {
            hoeTransform.rotation = Quaternion.Euler(0, 0, idleRotation);
        }
        
        // Ensure sprite is at correct offset
        if (hoeSprite != null)
        {
            hoeSprite.transform.localPosition = hoeSpriteOffset;
        }
        
        // Initialize mode visual indicators
        InitializeModeIndicators();
        
        // Set initial mode
        SetFarmerMode(currentMode);
    }
    
    private void InitializeModeIndicators()
    {
        // Create mode light if not assigned
        if (modeLight == null)
        {
            GameObject lightObj = new GameObject("ModeLight");
            lightObj.transform.SetParent(transform);
            lightObj.transform.localPosition = Vector3.zero;
            
            modeLight = lightObj.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
            modeLight.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Point;
            modeLight.pointLightOuterRadius = 2f;
            modeLight.pointLightInnerRadius = 0f;
            modeLight.falloffIntensity = 1f;
            modeLight.intensity = 1.5f;
        }
        
        UpdateModeVisuals();
    }
    
    protected override void Update()
    {
        base.Update();
        
        // Handle mode toggle input
        if (allowModeToggle && Input.GetKeyDown(toggleModeKey) && CanToggleMode())
        {
            ToggleFarmerMode();
        }
        
        // Update behavior based on current mode
        if (currentMode == FarmerMode.Farm)
        {
            UpdateFarmModeBehavior();
        }
        else
        {
            UpdateCombatModeBehavior();
        }
    }
    
    private void UpdateFarmModeBehavior()
    {
        // In farm mode, farmer wanders around and flees from enemies
        
        // Check for nearby enemies to flee from
        CheckForThreats();
        
        if (isFleeing)
        {
            // Continue fleeing behavior
            UpdateFleeingBehavior();
        }
        else
        {
            // Normal farm wandering
            UpdateFarmWandering();
        }
        
        // Keep hoe in idle position
        if (!isAttacking && hoeTransform != null)
        {
            ReturnToIdleRotation();
        }
    }
    
    private void UpdateCombatModeBehavior()
    {
        // In combat mode, behave like a commoner with weapon tracking
        if (!isAttacking && hoeTransform != null)
        {
            if (currentTarget != null)
            {
                float distanceToTarget = Vector3.Distance(transform.position, currentTarget.position);
                
                // Only rotate hoe when target is within combat ready distance
                if (distanceToTarget <= combatReadyDistance)
                {
                    UpdateHoeRotation();
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
    
    private void CheckForThreats()
    {
        if (currentMode != FarmerMode.Farm) return;
        
        // Find nearest enemy within detection range
        Collider2D[] threats = Physics2D.OverlapCircleAll(transform.position, fleeDistance, enemyDetectionLayers);
        
        Transform nearestThreat = null;
        float nearestDistance = float.MaxValue;
        
        foreach (var threat in threats)
        {
            if (threat.gameObject == gameObject) continue;
            
            // Check if it's actually a threat (enemy or rebel villager)
            if (threat.CompareTag("Enemy") || (threat.GetComponent<Villager>()?.IsRebel() == true))
            {
                float distance = Vector3.Distance(transform.position, threat.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestThreat = threat.transform;
                }
            }
        }
        
        if (nearestThreat != null && !isFleeing)
        {
            // Start fleeing
            StartFleeing(nearestThreat);
        }
        else if (nearestThreat == null && isFleeing)
        {
            // Stop fleeing
            StopFleeing();
        }
        else if (isFleeing && nearestThreat != fleeFrom)
        {
            // Update flee target
            fleeFrom = nearestThreat;
        }
    }
    
    private void StartFleeing(Transform threat)
    {
        isFleeing = true;
        fleeFrom = threat;
        
        if (debugMode)
        {
            Debug.Log($"Farmer {gameObject.name} started fleeing from {threat.name}");
        }
    }
    
    private void StopFleeing()
    {
        isFleeing = false;
        fleeFrom = null;
        
        if (debugMode)
        {
            Debug.Log($"Farmer {gameObject.name} stopped fleeing");
        }
    }
    
    private void UpdateFleeingBehavior()
    {
        if (fleeFrom == null)
        {
            StopFleeing();
            return;
        }
        
        // Calculate flee target that keeps farmer near the farm
        Vector3 fleeTarget = CalculateSmartFleeTarget();
        
        // Move towards flee target
        if (farmerAI != null)
        {
            // Use AI to move to flee position
            // This would require extending VillagerAI to support flee behavior
            // For now, move directly
            transform.position = Vector3.MoveTowards(transform.position, fleeTarget, farmWanderSpeed * fleeSpeedMultiplier * Time.deltaTime);
        }
        
        if (debugMode)
        {
            Debug.DrawLine(transform.position, fleeTarget, Color.blue, 0.1f);
        }
    }
    
    private Vector3 CalculateSmartFleeTarget()
    {
        if (farmCenter == null || fleeFrom == null) 
        {
            // Fallback to simple flee direction
            Vector3 simpleFleeDirection = (transform.position - fleeFrom.position).normalized;
            return transform.position + simpleFleeDirection * fleeDistance;
        }
        
        // Get positions
        Vector3 farmPos = farmCenter.position;
        Vector3 currentPos = transform.position;
        Vector3 threatPos = fleeFrom.position;
        
        // Calculate basic flee direction (away from threat)
        Vector3 awayFromThreat = (currentPos - threatPos).normalized;
        
        // Calculate direction towards farm center
        Vector3 towardsFarm = (farmPos - currentPos).normalized;
        
        // Blend the two directions based on distance from farm
        float distanceFromFarm = Vector3.Distance(currentPos, farmPos);
        float farmInfluence = Mathf.Clamp01(distanceFromFarm / farmWanderRadius);
        
        // The further from farm, the more we prioritize getting back to it
        Vector3 blendedDirection = Vector3.Lerp(awayFromThreat, towardsFarm, farmInfluence * 0.6f).normalized;
        
        // Calculate potential flee target
        Vector3 potentialTarget = currentPos + blendedDirection * (fleeDistance * 0.8f);
        
        // Ensure the flee target is within reasonable distance of farm
        float targetDistanceFromFarm = Vector3.Distance(potentialTarget, farmPos);
        if (targetDistanceFromFarm > farmWanderRadius * maxDistanceFromFarm)
        {
            // Clamp the target to stay within expanded farm area
            Vector3 directionFromFarm = (potentialTarget - farmPos).normalized;
            potentialTarget = farmPos + directionFromFarm * (farmWanderRadius * maxDistanceFromFarm);
        }
        
        // Make sure we're still moving away from the threat
        Vector3 threatToTarget = (potentialTarget - threatPos).normalized;
        Vector3 threatToCurrent = (currentPos - threatPos).normalized;
        
        // If the target would move us closer to the threat, adjust it
        if (Vector3.Dot(threatToTarget, threatToCurrent) < 0.3f)
        {
            // Find a position that's both away from threat and near farm
            Vector3 safeDirection = Vector3.Cross(Vector3.Cross(Vector3.forward, awayFromThreat), Vector3.forward).normalized;
            
            // Try both perpendicular directions and pick the one closer to farm
            Vector3 option1 = currentPos + (awayFromThreat + safeDirection).normalized * fleeDistance * 0.7f;
            Vector3 option2 = currentPos + (awayFromThreat - safeDirection).normalized * fleeDistance * 0.7f;
            
            float dist1 = Vector3.Distance(option1, farmPos);
            float dist2 = Vector3.Distance(option2, farmPos);
            
            potentialTarget = dist1 < dist2 ? option1 : option2;
        }
        
        return potentialTarget;
    }
    
    private void UpdateFarmWandering()
    {
        // Wander around the farm center
        if (Time.time - lastWanderTime >= wanderInterval)
        {
            ChooseNewWanderTarget();
            lastWanderTime = Time.time;
        }
        
        // Move towards current wander target
        if (currentWanderTarget != Vector3.zero)
        {
            float distanceToTarget = Vector3.Distance(transform.position, currentWanderTarget);
            
            if (distanceToTarget > 0.5f)
            {
                Vector3 direction = (currentWanderTarget - transform.position).normalized;
                transform.position += direction * farmWanderSpeed * Time.deltaTime;
            }
            else
            {
                // Reached target, choose new one
                ChooseNewWanderTarget();
            }
        }
    }
    
    private void ChooseNewWanderTarget()
    {
        Vector2 randomDirection = Random.insideUnitCircle * farmWanderRadius;
        currentWanderTarget = farmCenter.position + (Vector3)randomDirection;
        
        wanderInterval = Random.Range(2f, 5f); // Randomize next wander time
        
        if (debugMode)
        {
            Debug.Log($"Farmer {gameObject.name} chose new wander target: {currentWanderTarget}");
        }
    }
    
    private void UpdateHoeRotation()
    {
        if (currentTarget == null || hoeTransform == null) return;
        
        Vector2 direction = (currentTarget.position - hoeTransform.position).normalized;
        hoeTransform.right = direction;
        
        Vector2 scale = hoeTransform.localScale;
        if (direction.x < 0)
        {
            scale.y = -1;
        }
        else
        {
            scale.y = 1;
        }
        hoeTransform.localScale = scale;
    }
    
    private void ReturnToIdleRotation()
    {
        if (hoeTransform == null) return;
        
        hoeTransform.localScale = Vector3.one;
        Quaternion targetRotation = Quaternion.Euler(0, 0, idleRotation);
        hoeTransform.rotation = Quaternion.Lerp(hoeTransform.rotation, targetRotation, 5 * Time.deltaTime);
    }
    
    protected override IEnumerator PerformAttack()
    {
        // Only attack in combat mode
        if (currentMode != FarmerMode.Combat) yield break;
        
        if (debugMode)
        {
            Debug.Log($"FarmerCombat: {gameObject.name} starting attack on {currentTarget?.name}");
        }
        
        isAttacking = true;
        lastAttackTime = Time.time;
        
        // Trigger hoe attack animation
        if (hoeAnimator != null)
        {
            hoeAnimator.SetTrigger("Attack");
        }
        else if (debugMode)
        {
            Debug.LogWarning($"FarmerCombat: No hoe animator found on {gameObject.name}!");
        }
        
        // Wait for animation to complete
        yield return new WaitForSeconds(attackAnimationDuration);
        
        isAttacking = false;
        
        if (debugMode)
        {
            Debug.Log($"FarmerCombat: {gameObject.name} finished attack");
        }
    }
    
    protected override bool CanCombat()
    {
        // Only engage in combat when in Combat mode
        if (currentMode == FarmerMode.Farm) return false;
        
        // Use base combat logic for combat mode
        return base.CanCombat();
    }
    
    public override void UpdateCombatStats()
    {
        // Call base implementation first
        base.UpdateCombatStats();
        
        // Update damage dealer with new stats
        if (hoeDamageDealer != null)
        {
            hoeDamageDealer.SetDamage(currentDamage);
            
            if (debugMode)
            {
                Debug.Log($"FarmerCombat: Updated {gameObject.name} damage to {currentDamage}");
            }
        }
        
        // Farmer-specific stat adjustments (same as commoner in combat mode)
        if (villager != null)
        {
            VillagerStats stats = villager.GetStats();
            
            // Tier 2 gets faster attacks (same as commoner)
            if (stats.tier >= 2 && currentMode == FarmerMode.Combat)
            {
                currentAttackCooldown *= 0.8f; // 20% faster attacks at tier 2
            }
        }
    }
    
    // Mode management methods
    public void SetFarmerMode(FarmerMode newMode)
    {
        FarmerMode oldMode = currentMode;
        currentMode = newMode;
        
        if (oldMode != newMode)
        {
            OnModeChanged(oldMode, newMode);
        }
    }
    
    public void ToggleFarmerMode()
    {
        if (!CanToggleMode()) return;
        
        FarmerMode newMode = currentMode == FarmerMode.Farm ? FarmerMode.Combat : FarmerMode.Farm;
        SetFarmerMode(newMode);
        
        lastModeToggle = Time.time;
    }
    
    private bool CanToggleMode()
    {
        return canToggleMode && (Time.time - lastModeToggle >= modeToggleCooldown);
    }
    
    private void OnModeChanged(FarmerMode oldMode, FarmerMode newMode)
    {
        if (debugMode)
        {
            Debug.Log($"Farmer {gameObject.name} mode changed: {oldMode} -> {newMode}");
        }
        
        // Update visual indicators
        UpdateModeVisuals();
        
        // Reset combat state when switching modes
        if (newMode == FarmerMode.Farm)
        {
            // Clear combat target when switching to farm mode
            currentTarget = null;
            if (farmerAI != null)
            {
                farmerAI.ClearCombatTarget();
            }
        }
        else if (newMode == FarmerMode.Combat)
        {
            // Stop fleeing when switching to combat mode
            StopFleeing();
        }
        
        // Update combat stats for new mode
        UpdateCombatStats();
    }
    
    private void UpdateModeVisuals()
    {
        if (modeLight != null)
        {
            switch (currentMode)
            {
                case FarmerMode.Farm:
                    modeLight.color = farmModeColor;
                    modeLight.intensity = 1f;
                    break;
                case FarmerMode.Combat:
                    modeLight.color = combatModeColor;
                    modeLight.intensity = 1.5f;
                    break;
            }
        }
        
        if (combatModeIndicator != null)
        {
            combatModeIndicator.SetActive(currentMode == FarmerMode.Combat);
        }
    }
    
    // Food production methods
    public int CalculateFoodProduction()
    {
        if (villager == null) return baseFoodProduction;
        
        VillagerStats stats = villager.GetStats();
        
        switch (stats.tier)
        {
            case 0:
                return baseFoodProduction;
            case 1:
                return tier1FoodProduction;
            case 2:
                return tier2FoodProduction;
            default:
                return baseFoodProduction;
        }
    }
    
    // Public getters and setters
    public FarmerMode GetCurrentMode() => currentMode;
    public bool IsInFarmMode() => currentMode == FarmerMode.Farm;
    public bool IsInCombatMode() => currentMode == FarmerMode.Combat;
    public bool IsFleeing() => isFleeing;
    public Transform GetFleeTarget() => fleeFrom;
    public float GetFarmWanderRadius() => farmWanderRadius;
    public Vector3 GetCurrentWanderTarget() => currentWanderTarget;
    
    public void SetAllowModeToggle(bool allow)
    {
        allowModeToggle = allow;
    }
    
    public void SetFarmCenter(Transform center)
    {
        farmCenter = center;
    }
    
    public void SetFoodProduction(int baseFood, int tier1Food, int tier2Food)
    {
        baseFoodProduction = baseFood;
        tier1FoodProduction = tier1Food;
        tier2FoodProduction = tier2Food;
    }
    
    public void SetFarmWanderRadius(float radius)
    {
        farmWanderRadius = radius;
    }
    
    public void SetFleeDistance(float distance)
    {
        fleeDistance = distance;
    }
    
    public void SetMaxDistanceFromFarm(float multiplier)
    {
        maxDistanceFromFarm = multiplier;
    }
    
    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        
        // Draw hoe pivot point
        if (hoeTransform != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(hoeTransform.position, 0.1f);
            
            // Draw line showing hoe direction
            Vector3 hoeDirection = hoeTransform.rotation * Vector3.right;
            Gizmos.DrawRay(hoeTransform.position, hoeDirection * 1f);
            
            // Draw hoe sprite position
            if (Application.isPlaying && hoeSprite != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(hoeSprite.transform.position, 0.05f);
                Gizmos.DrawLine(hoeTransform.position, hoeSprite.transform.position);
            }
        }
        
        // Draw combat ready distance (only in combat mode)
        if (currentMode == FarmerMode.Combat)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, combatReadyDistance);
        }
        
        // Draw farm wander area (only in farm mode)
        if (currentMode == FarmerMode.Farm && farmCenter != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(farmCenter.position, farmWanderRadius);
            
            // Draw current wander target
            if (Application.isPlaying && currentWanderTarget != Vector3.zero)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(currentWanderTarget, 0.3f);
                Gizmos.DrawLine(transform.position, currentWanderTarget);
            }
        }
        
        // Draw flee distance and direction
        if (Application.isPlaying && currentMode == FarmerMode.Farm)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, fleeDistance);
            
            if (isFleeing && fleeFrom != null)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(transform.position, fleeFrom.position);
                
                // Draw smart flee target
                Vector3 smartFleeTarget = CalculateSmartFleeTarget();
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(smartFleeTarget, 0.5f);
                Gizmos.DrawLine(transform.position, smartFleeTarget);
                
                // Draw farm influence area
                if (farmCenter != null)
                {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawWireSphere(farmCenter.position, farmWanderRadius * maxDistanceFromFarm);
                }
            }
        }
    }
    
    // Context menu for debugging
    [ContextMenu("Toggle Farmer Mode")]
    public void DebugToggleMode()
    {
        if (Application.isPlaying)
        {
            ToggleFarmerMode();
        }
    }
    
    [ContextMenu("Debug Farmer Stats")]
    public void DebugFarmerStats()
    {
        Debug.Log($"Farmer {gameObject.name} Stats:");
        Debug.Log($"  Mode: {currentMode}");
        Debug.Log($"  Food Production: {CalculateFoodProduction()}");
        Debug.Log($"  Is Fleeing: {isFleeing}");
        Debug.Log($"  Combat Damage: {currentDamage}");
        Debug.Log($"  Combat Cooldown: {currentAttackCooldown:F2}s");
        Debug.Log($"  Combat Range: {currentAttackRange:F2}");
    }
}