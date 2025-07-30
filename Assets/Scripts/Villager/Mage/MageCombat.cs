using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class MageCombat : VillagerCombat
{
    [Header("Mage Specific - Non-Combat")]
    [SerializeField] private float fleeSpeed = 4f;
    [SerializeField] private float fleeRange = 10f;
    [SerializeField] private float safeDistance = 6f;
    [SerializeField] private LayerMask threatLayers = -1;
    
    [Header("Potion Throwing")]
    [SerializeField] private Transform throwPoint;
    [SerializeField] private GameObject potionPrefab;
    [SerializeField] private float throwRange = 8f;
    [SerializeField] private float throwCooldown = 3f;
    [SerializeField] private float throwAnimationDuration = 0.8f;
    [SerializeField] private AnimationCurve potionArcCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("Auto-Find Components")]
    [SerializeField] private bool autoFindThrowPoint = true;
    [SerializeField] private bool autoFindStaff = true;
    [SerializeField] private bool autoFindPotionBar = true;
    
    [Header("Potion Progress UI")]
    [SerializeField] private Slider potionBar;
    [SerializeField] private bool hidePotionBarWhenNotBrewing = true;
    
    [Header("Debug")]
    [SerializeField] private bool debugMage = false;
    [SerializeField] private bool debugThrowing = false;
    
    // Components
    private Animator mageAnimator;
    private Transform staffTransform;
    private MageHealingSystem healingSystem;
    
    // Potion throwing state
    private bool isThrowing = false;
    private float lastThrowTime = 0f;
    private Transform currentHealTarget;
    
    // Flee state
    private bool isFleeing = false;
    private Transform currentThreat;
    
    protected override void Awake()
    {
        base.Awake();
        
        // Get mage-specific components
        healingSystem = GetComponent<MageHealingSystem>();
        if (healingSystem == null)
        {
            healingSystem = gameObject.AddComponent<MageHealingSystem>();
            Debug.Log($"MageCombat: Added MageHealingSystem to {gameObject.name}");
        }
        
        // Auto-find components
        if (autoFindThrowPoint && throwPoint == null)
        {
            FindThrowPoint();
        }
        
        if (autoFindStaff && staffTransform == null)
        {
            FindStaff();
        }
        
        if (autoFindPotionBar && potionBar == null)
        {
            FindPotionBar();
        }
        
        mageAnimator = GetComponent<Animator>();
    }
    
    private void FindThrowPoint()
    {
        // Look for throw point in hierarchy
        throwPoint = transform.Find("ThrowPoint");
        if (throwPoint == null)
        {
            // Try alternative names
            foreach (Transform child in GetComponentsInChildren<Transform>())
            {
                if (child.name.ToLower().Contains("throw") || child.name.ToLower().Contains("hand"))
                {
                    throwPoint = child;
                    break;
                }
            }
        }
        
        // Create one if not found
        if (throwPoint == null)
        {
            GameObject throwObj = new GameObject("ThrowPoint");
            throwObj.transform.SetParent(transform);
            throwObj.transform.localPosition = new Vector3(0.5f, 1f, 0); // Right hand position
            throwPoint = throwObj.transform;
            
            if (debugMage)
            {
                Debug.Log($"Created ThrowPoint for {gameObject.name}");
            }
        }
    }
    
    private void FindStaff()
    {
        // Look for staff in hierarchy
        staffTransform = transform.Find("Staff");
        if (staffTransform == null)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>())
            {
                if (child.name.ToLower().Contains("staff") || child.name.ToLower().Contains("wand"))
                {
                    staffTransform = child;
                    break;
                }
            }
        }
        
        if (debugMage && staffTransform != null)
        {
            Debug.Log($"Found staff: {staffTransform.name} for {gameObject.name}");
        }
    }
    
    private void FindPotionBar()
    {
        // Look for potion bar in hierarchy
        potionBar = GetComponentInChildren<Slider>();
        
        // If multiple sliders, try to find one with "potion" in the name
        if (potionBar == null)
        {
            Slider[] sliders = GetComponentsInChildren<Slider>();
            foreach (var slider in sliders)
            {
                if (slider.name.ToLower().Contains("potion"))
                {
                    potionBar = slider;
                    break;
                }
            }
            
            // Fallback to first slider found
            if (potionBar == null && sliders.Length > 0)
            {
                potionBar = sliders[0];
            }
        }
        
        // Configure potion bar
        if (potionBar != null)
        {
            potionBar.minValue = 0f;
            potionBar.maxValue = 1f;
            potionBar.value = 0f;
            
            // Hide initially if configured
            if (hidePotionBarWhenNotBrewing)
            {
                potionBar.gameObject.SetActive(false);
            }
            
            if (debugMage)
            {
                Debug.Log($"Found potion bar: {potionBar.name} for {gameObject.name}");
            }
        }
        else if (debugMage)
        {
            Debug.LogWarning($"No PotionBar (Slider) found for {gameObject.name}");
        }
    }
    
    protected override void Start()
    {
        base.Start();
        
        // Mages don't engage in direct combat
        detectionRange = fleeRange; // Use detection range for flee range
        
        // Initialize healing system
        if (healingSystem != null)
        {
            healingSystem.Initialize(this, villager);
        }
    }
    
    protected override void Update()
    {
        // Mages don't use the base combat update
        // Instead, they focus on healing and fleeing
        
        if (!CanOperate()) return;
        
        UpdateThreatDetection();
        UpdatePotionThrowing();
        UpdateStaffRotation();
    }
    
    private bool CanOperate()
    {
        // Mages can operate when loyal or angry, but not when rebelling
        if (villager == null || !villager.IsActive()) return false;
        return villager.GetState() != VillagerState.Rebel;
    }
    
    private void UpdateThreatDetection()
    {
        // Check for nearby threats every few frames
        if (Time.frameCount % 30 != 0) return; // Update every 30 frames
        
        Transform nearestThreat = FindNearestThreat();
        
        if (nearestThreat != currentThreat)
        {
            currentThreat = nearestThreat;
            
            if (currentThreat != null)
            {
                StartFleeing();
            }
            else
            {
                StopFleeing();
            }
        }
    }
    
    private Transform FindNearestThreat()
    {
        Transform nearest = null;
        float nearestDistance = fleeRange;
        
        // Check for enemies
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (GameObject enemy in enemies)
        {
            if (enemy == gameObject) continue;
            
            float distance = Vector3.Distance(transform.position, enemy.transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = enemy.transform;
            }
        }
        
        // Also check for rebel villagers
        if (villager != null && !villager.IsRebel())
        {
            Villager[] allVillagers = FindObjectsOfType<Villager>();
            foreach (Villager v in allVillagers)
            {
                if (v == villager || !v.IsRebel()) continue;
                
                float distance = Vector3.Distance(transform.position, v.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = v.transform;
                }
            }
        }
        
        return nearest;
    }
    
    private void StartFleeing()
    {
        isFleeing = true;
        
        // Notify AI to flee
        if (villagerAI != null)
        {
            villagerAI.SetCombatTarget(currentThreat); // AI will flee from combat targets
        }
        
        if (debugMage)
        {
            Debug.Log($"Mage {gameObject.name} started fleeing from {currentThreat.name}");
        }
    }
    
    private void StopFleeing()
    {
        isFleeing = false;
        
        // Clear AI target
        if (villagerAI != null)
        {
            villagerAI.ClearCombatTarget();
        }
        
        if (debugMage)
        {
            Debug.Log($"Mage {gameObject.name} stopped fleeing");
        }
    }
    
    private void UpdatePotionThrowing()
    {
        // Don't throw potions while fleeing or if efficiency too low
        if (isFleeing || isThrowing || combatEfficiency < 0.3f) 
        {
            // Hide potion bar when not actively brewing
            UpdatePotionBarVisibility(false);
            return;
        }
        
        // Check cooldown
        float cooldownRemaining = throwCooldown - (Time.time - lastThrowTime);
        if (cooldownRemaining > 0)
        {
            // Show brewing progress during cooldown
            UpdatePotionBarProgress(1f - (cooldownRemaining / throwCooldown));
            UpdatePotionBarVisibility(true);
            return;
        }
        
        // Find villager who needs healing
        Transform healTarget = FindHealTarget();
        
        if (healTarget != null)
        {
            // Reset bar for new potion throw
            UpdatePotionBarProgress(0f);
            UpdatePotionBarVisibility(false); // Hide during throw animation
            
            currentHealTarget = healTarget;
            StartCoroutine(ThrowPotionSequence());
        }
        else
        {
            // No target, show full bar (potion ready but no target)
            UpdatePotionBarProgress(1f);
            UpdatePotionBarVisibility(true);
        }
    }
    
    private Transform FindHealTarget()
    {
        Transform bestTarget = null;
        float lowestHealthRatio = 1f; // Only heal if below 100% health
        
        // Find all villagers within throw range
        Villager[] allVillagers = FindObjectsOfType<Villager>();
        
        foreach (Villager v in allVillagers)
        {
            if (v == villager) continue; // Don't heal self
            if (v.IsRebel() && !villager.IsRebel()) continue; // Loyals don't heal rebels
            if (!v.IsRebel() && villager.IsRebel()) continue; // Rebels don't heal loyals
            
            float distance = Vector3.Distance(transform.position, v.transform.position);
            if (distance > throwRange) continue;
            
            // Check health ratio
            VillagerHealth vHealth = v.GetComponent<VillagerHealth>();
            if (vHealth != null)
            {
                float healthRatio = (float)vHealth.GetCurrentHP() / vHealth.GetMaxHP();
                
                if (healthRatio < lowestHealthRatio)
                {
                    lowestHealthRatio = healthRatio;
                    bestTarget = v.transform;
                }
            }
        }
        
        return bestTarget;
    }
    
    private IEnumerator ThrowPotionSequence()
    {
        if (debugThrowing)
        {
            Debug.Log($"Mage {gameObject.name} starting potion throw sequence to {currentHealTarget.name}");
        }
        
        isThrowing = true;
        lastThrowTime = Time.time;
        
        // Show throwing progress on potion bar
        UpdatePotionBarVisibility(true);
        
        // Trigger throw animation
        if (mageAnimator != null)
        {
            mageAnimator.SetTrigger("Throw");
        }
        
        // Update potion bar during wind-up (0 to 0.5 progress)
        float windUpTime = throwAnimationDuration * 0.3f;
        float elapsed = 0f;
        
        while (elapsed < windUpTime)
        {
            float progress = elapsed / windUpTime;
            UpdatePotionBarProgress(progress * 0.5f); // 0 to 0.5
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // Spawn and throw potion projectile at 50% progress
        if (potionPrefab != null && throwPoint != null && currentHealTarget != null)
        {
            ThrowPotion();
        }
        
        // Update potion bar during throw completion (0.5 to 1.0 progress)
        float remainingTime = throwAnimationDuration * 0.7f;
        elapsed = 0f;
        
        while (elapsed < remainingTime)
        {
            float progress = elapsed / remainingTime;
            UpdatePotionBarProgress(0.5f + (progress * 0.5f)); // 0.5 to 1.0
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // Complete the throw
        UpdatePotionBarProgress(0f); // Reset for next potion
        isThrowing = false;
        currentHealTarget = null;
        
        if (debugThrowing)
        {
            Debug.Log($"Mage {gameObject.name} completed potion throw sequence");
        }
    }
    
    private void ThrowPotion()
    {
        Vector3 startPos = throwPoint.position;
        Vector3 targetPos = currentHealTarget.position;
        
        // Create potion projectile
        GameObject potion = Instantiate(potionPrefab, startPos, Quaternion.identity);
        PotionProjectile projectile = potion.GetComponent<PotionProjectile>();
        
        if (projectile == null)
        {
            projectile = potion.AddComponent<PotionProjectile>();
        }
        
        // Calculate healing amount based on tier and efficiency
        int healAmount = CalculateHealAmount();
        
        // Launch the potion
        projectile.Launch(startPos, targetPos, healAmount, potionArcCurve);
        
        if (debugThrowing)
        {
            Debug.Log($"Mage {gameObject.name} threw potion with {healAmount} heal to {currentHealTarget.name}");
        }
    }
    
    private int CalculateHealAmount()
    {
        if (villager == null) return 0;
        
        VillagerStats stats = villager.GetStats();
        int baseHeal = 15; // Base healing amount
        
        // Scale with tier
        int healAmount = baseHeal + (stats.tier * 10);
        
        // Apply efficiency (food and state penalties)
        healAmount = Mathf.RoundToInt(healAmount * combatEfficiency);
        
        return Mathf.Max(1, healAmount); // Minimum 1 heal
    }
    
    private void UpdatePotionBarProgress(float progress)
    {
        if (potionBar != null)
        {
            potionBar.value = Mathf.Clamp01(progress);
        }
    }
    
    private void UpdatePotionBarVisibility(bool visible)
    {
        if (potionBar != null && hidePotionBarWhenNotBrewing)
        {
            potionBar.gameObject.SetActive(visible);
        }
    }
    
    private void UpdateStaffRotation()
    {
        if (staffTransform == null) return;
        
        if (isFleeing && currentThreat != null)
        {
            // Point staff away from threat (defensive posture)
            Vector3 fleeDirection = (transform.position - currentThreat.position).normalized;
            float angle = Mathf.Atan2(fleeDirection.y, fleeDirection.x) * Mathf.Rad2Deg;
            staffTransform.rotation = Quaternion.Lerp(staffTransform.rotation, Quaternion.Euler(0, 0, angle), 3f * Time.deltaTime);
        }
        else if (currentHealTarget != null && isThrowing)
        {
            // Point staff toward heal target
            Vector3 targetDirection = (currentHealTarget.position - staffTransform.position).normalized;
            float angle = Mathf.Atan2(targetDirection.y, targetDirection.x) * Mathf.Rad2Deg;
            staffTransform.rotation = Quaternion.Lerp(staffTransform.rotation, Quaternion.Euler(0, 0, angle), 5f * Time.deltaTime);
        }
        else
        {
            // Return to neutral position
            staffTransform.rotation = Quaternion.Lerp(staffTransform.rotation, Quaternion.identity, 2f * Time.deltaTime);
        }
    }
    
    // Override base combat methods since mages don't fight
    protected override bool CanCombat()
    {
        return false; // Mages never engage in combat
    }
    
    protected override void UpdateTarget()
    {
        // Mages don't target enemies for combat, only for fleeing
    }
    
    protected override IEnumerator PerformAttack()
    {
        // Mages don't attack
        yield break;
    }
    
    public override void UpdateCombatStats()
    {
        // Update base stats but don't apply combat bonuses
        if (villager == null) return;
        
        VillagerStats stats = villager.GetStats();
        
        // Mages don't have damage or attack cooldown, but we track efficiency
        // Efficiency affects healing and AoE systems
        
        // Update healing system if available
        if (healingSystem != null)
        {
            healingSystem.OnMageTierChanged();
        }
    }
    
    // Public getters for debugging and UI
    public bool IsFleeing() => isFleeing;
    public bool IsThrowing() => isThrowing;
    public Transform GetCurrentThreat() => currentThreat;
    public Transform GetCurrentHealTarget() => currentHealTarget;
    public float GetThrowCooldownRemaining() => Mathf.Max(0f, throwCooldown - (Time.time - lastThrowTime));
    public float GetFleeRange() => fleeRange;
    public float GetThrowRange() => throwRange;
    public float GetPotionProgress() => potionBar != null ? potionBar.value : 0f;
    
    // Public setters for configuration
    public void SetFleeRange(float range)
    {
        fleeRange = range;
        detectionRange = range;
    }
    
    public void SetThrowRange(float range)
    {
        throwRange = range;
    }
    
    public void SetThrowCooldown(float cooldown)
    {
        throwCooldown = cooldown;
    }
    
    public void SetPotionBar(Slider slider)
    {
        potionBar = slider;
        if (potionBar != null)
        {
            potionBar.minValue = 0f;
            potionBar.maxValue = 1f;
            potionBar.value = 0f;
        }
    }
    
    protected override void OnDrawGizmosSelected()
    {
        // Draw flee range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, fleeRange);
        
        // Draw throw range
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, throwRange);
        
        // Draw safe distance
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, safeDistance);
        
        // Draw line to current threat
        if (currentThreat != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, currentThreat.position);
        }
        
        // Draw line to heal target
        if (currentHealTarget != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, currentHealTarget.position);
        }
        
        // Draw throw point
        if (throwPoint != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(throwPoint.position, 0.2f);
        }
        
        // Draw staff position
        if (staffTransform != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(staffTransform.position, 0.1f);
            
            // Draw staff direction
            Vector3 staffDirection = staffTransform.rotation * Vector3.right;
            Gizmos.DrawRay(staffTransform.position, staffDirection * 1f);
        }
    }
    
    // Context menu for debugging
    [ContextMenu("Debug Mage Stats")]
    public void DebugMageStats()
    {
        Debug.Log($"Mage {gameObject.name} Stats:");
        Debug.Log($"  Is Fleeing: {isFleeing}");
        Debug.Log($"  Is Throwing: {isThrowing}");
        Debug.Log($"  Combat Efficiency: {combatEfficiency:P0}");
        Debug.Log($"  Current Threat: {(currentThreat != null ? currentThreat.name : "None")}");
        Debug.Log($"  Current Heal Target: {(currentHealTarget != null ? currentHealTarget.name : "None")}");
        Debug.Log($"  Throw Cooldown Remaining: {GetThrowCooldownRemaining():F1}s");
        Debug.Log($"  Potion Progress: {GetPotionProgress():P0}");
        
        if (healingSystem != null)
        {
            Debug.Log($"  Healing System Active: {healingSystem.IsHealingActive()}");
            Debug.Log($"  AoE Heal Range: {healingSystem.GetHealRange():F1}");
        }
    }
    
    [ContextMenu("Force Throw Potion")]
    public void ForceThrowPotion()
    {
        if (Application.isPlaying && !isThrowing)
        {
            Transform target = FindHealTarget();
            if (target != null)
            {
                currentHealTarget = target;
                StartCoroutine(ThrowPotionSequence());
            }
            else
            {
                Debug.LogWarning("No valid heal target found for forced potion throw");
            }
        }
    }
}