using UnityEngine;
using System.Collections;

public class MageCombat : VillagerCombat
{
    [Header("Mage Behavior")]
    [SerializeField] private float fleeRange = 8f;
    [SerializeField] private float fleeSpeed = 6f;
    [SerializeField] private float potionRange = 15f; // Increased range to reach more villagers
    [SerializeField] private float basePotionCooldown = 3f;
    [SerializeField] private float roamRadius = 20f; // How far mage will roam to find villagers
    [SerializeField] private float roamSpeed = 3f;
    
    [Header("Potion System")]
    [SerializeField] private GameObject potionPrefab;
    [SerializeField] private Transform throwPoint;
    [SerializeField] private int basePotionHeal = 15;
    
    [Header("Components")]
    [SerializeField] private bool autoFindComponents = true;
    
    [Header("Potion UI")]
    [SerializeField] private UnityEngine.UI.Slider potionSlider;
    [SerializeField] private bool hidePotionBarWhenReady = true;
    
    // State
    private Transform fleeTarget;
    private Transform healTarget; // Villager to move toward for healing
    private bool isFleeing = false;
    private bool isThrowing = false;
    private bool isRoaming = false;
    private float lastPotionTime = 0f;
    private Vector3 roamDestination;
    private float lastRoamUpdate = 0f;
    
    // Components
    private MageHealingSystem healingSystem;
    
    protected override void Awake()
    {
        base.Awake();
        
        // Get healing system
        healingSystem = GetComponent<MageHealingSystem>();
        if (healingSystem == null)
        {
            healingSystem = gameObject.AddComponent<MageHealingSystem>();
        }
    }
    
    private void UpdatePotionSlider()
    {
        if (potionSlider == null) return;
        
        // Don't show slider if efficiency too low
        if (combatEfficiency < 0.3f)
        {
            SetPotionSliderVisibility(false);
            return;
        }
        
        // Show slider
        SetPotionSliderVisibility(true);
        
        if (isThrowing)
        {
            // Keep at 0 while throwing
            SetPotionSliderValue(0f);
        }
        else
        {
            // Show cooldown progress
            float cooldown = GetPotionCooldown();
            float timeSinceThrow = Time.time - lastPotionTime;
            float progress = Mathf.Clamp01(timeSinceThrow / cooldown);
            
            SetPotionSliderValue(progress);
            
            // Hide when ready if configured
            if (hidePotionBarWhenReady && progress >= 1f)
            {
                SetPotionSliderVisibility(false);
            }
        }
    }
    
    private void SetPotionSliderValue(float value)
    {
        if (potionSlider != null)
        {
            potionSlider.value = Mathf.Clamp01(value);
        }
    }
    
    private void SetPotionSliderVisibility(bool visible)
    {
        if (potionSlider != null)
        {
            potionSlider.gameObject.SetActive(visible);
        }
        
        if (autoFindComponents)
        {
            FindComponents();
        }
    }
    
    private void FindComponents()
    {
        // Find throw point
        if (throwPoint == null)
        {
            throwPoint = transform.Find("ThrowPoint");
            if (throwPoint == null)
            {
                GameObject throwObj = new GameObject("ThrowPoint");
                throwObj.transform.SetParent(transform);
                throwObj.transform.localPosition = new Vector3(0.5f, 1f, 0);
                throwPoint = throwObj.transform;
            }
        }
        
        // Find potion slider
        if (potionSlider == null)
        {
            potionSlider = GetComponentInChildren<UnityEngine.UI.Slider>();
        }
        
        // Configure potion slider
        if (potionSlider != null)
        {
            potionSlider.minValue = 0f;
            potionSlider.maxValue = 1f;
            potionSlider.value = 1f; // Start ready
        }
    }
    
    protected override void Start()
    {
        base.Start();
        
        // Mages don't engage in combat, they flee
        detectionRange = fleeRange;
        
        // Initialize healing system
        if (healingSystem != null)
        {
            healingSystem.Initialize(this, villager);
        }
    }
    
    protected override void Update()
    {
        if (!CanOperate()) return;
        
        // 1. Check for threats and flee (highest priority)
        CheckThreats();
        
        // 2. Update potion UI
        UpdatePotionSlider();
        
        // 3. Throw potions (even while fleeing)
        TryThrowPotion();
        
        // 4. Find villagers to heal (if not fleeing)
        if (!isFleeing)
        {
            UpdateHealTarget();
        }
        
        // 5. Update movement
        UpdateMovement();
    }
    
    private bool CanOperate()
    {
        return villager != null && villager.IsActive() && villager.GetState() != VillagerState.Rebel;
    }
    
    private void CheckThreats()
    {
        Transform nearestThreat = FindNearestThreat();
        
        if (nearestThreat != fleeTarget)
        {
            fleeTarget = nearestThreat;
            isFleeing = fleeTarget != null;
        }
    }
    
    private Transform FindNearestThreat()
    {
        Transform nearest = null;
        float nearestDistance = fleeRange;
        
        // Check enemies
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
        
        // Check rebel villagers if we're loyal
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
    
    private float GetPotionCooldown()
    {
        if (villager == null) return basePotionCooldown;
        
        int tier = villager.GetStats().tier;
        float reduction = tier * 0.3f; // 30% reduction per tier
        return Mathf.Max(0.5f, basePotionCooldown - reduction);
    }
    
    private Transform FindLowestHealthVillager()
    {
        Transform bestTarget = null;
        float lowestHealthRatio = 0.95f; // Only heal if below 95% health
        float bestScore = float.MaxValue; // Combine health ratio and distance for scoring
        
        // Check all villagers (expanded search - no distance limit initially)
        Villager[] allVillagers = FindObjectsOfType<Villager>();
        foreach (Villager v in allVillagers)
        {
            if (v == villager) continue;
            if (!ShouldHealVillager(v)) continue;
            
            float distance = Vector3.Distance(transform.position, v.transform.position);
            
            VillagerHealth vHealth = v.GetComponent<VillagerHealth>();
            if (vHealth != null)
            {
                float healthRatio = (float)vHealth.GetCurrentHP() / vHealth.GetMaxHP();
                if (healthRatio < lowestHealthRatio)
                {
                    // Score combines health priority and distance
                    // Lower health = lower score (higher priority)
                    // Closer distance = lower score (higher priority)
                    float healthScore = healthRatio * 2f; // Health is more important
                    float distanceScore = distance / 30f; // Normalize distance
                    float totalScore = healthScore + distanceScore;
                    
                    if (totalScore < bestScore)
                    {
                        bestScore = totalScore;
                        lowestHealthRatio = healthRatio;
                        bestTarget = v.transform;
                    }
                }
            }
        }
        
        // Also check player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            float distance = Vector3.Distance(transform.position, player.transform.position);
            
            PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                float playerHealthRatio = playerHealth.GetHealthPercentage();
                if (playerHealthRatio < lowestHealthRatio)
                {
                    float healthScore = playerHealthRatio * 2f;
                    float distanceScore = distance / 30f;
                    float totalScore = healthScore + distanceScore;
                    
                    if (totalScore < bestScore)
                    {
                        bestTarget = player.transform;
                    }
                }
            }
        }
        
        return bestTarget;
    }
    
    private bool ShouldHealVillager(Villager v)
    {
        // Only heal same faction
        if (villager.IsRebel())
        {
            return v.IsRebel();
        }
        else
        {
            return v.IsLoyal() || v.IsAngry();
        }
    }
    
    private IEnumerator ThrowPotionCoroutine(Transform target)
    {
        isThrowing = true;
        lastPotionTime = Time.time;
        
        // Update slider during throw
        SetPotionSliderValue(0f);
        SetPotionSliderVisibility(true);
        
        // Simple throw animation delay
        yield return new WaitForSeconds(0.3f);
        
        // Create and launch potion
        if (potionPrefab != null && throwPoint != null && target != null)
        {
            Vector3 startPos = throwPoint.position;
            Vector3 targetPos = target.position;
            
            GameObject potion = Instantiate(potionPrefab, startPos, Quaternion.identity);
            PotionProjectile projectile = potion.GetComponent<PotionProjectile>();
            
            if (projectile == null)
            {
                projectile = potion.AddComponent<PotionProjectile>();
            }
            
            int healAmount = CalculateHealAmount();
            
            // Configure projectile to impact on villagers
            projectile.SetCanImpactOnVillagers(true);
            projectile.Launch(startPos, targetPos, healAmount);
        }
        
        yield return new WaitForSeconds(0.2f);
        isThrowing = false;
    }
    
    private int CalculateHealAmount()
    {
        if (villager == null) return basePotionHeal;
        
        int tier = villager.GetStats().tier; // Get current power allocation
        
        // Base heal increases with tier, power level adds bonus
        int healAmount = basePotionHeal + (tier * 15);

        // Apply efficiency
        healAmount = Mathf.RoundToInt(healAmount * combatEfficiency);
        
        return Mathf.Max(1, healAmount);
    }
    
    private void UpdateMovement()
    {
        if (villagerAI == null) return;
        
        if (isFleeing && fleeTarget != null)
        {
            // Fleeing has highest priority
            villagerAI.SetCombatTarget(fleeTarget);
            isRoaming = false;
        }
        else if (healTarget != null)
        {
            // Move toward heal target
            float distance = Vector3.Distance(transform.position, healTarget.position);
            
            if (distance <= potionRange)
            {
                // In range - stop moving and clear target
                villagerAI.ClearCombatTarget();
                healTarget = null;
                isRoaming = false;
            }
            else
            {
                // Move toward heal target
                villagerAI.SetFollowPosition(healTarget.position);
                isRoaming = true;
            }
        }
        else
        {
            // No specific target - roam for injured villagers
            UpdateRoaming();
        }
    }
    
    private void UpdateHealTarget()
    {
        // Update heal target periodically
        if (Time.time - lastRoamUpdate < 2f) return; // Check every 2 seconds
        lastRoamUpdate = Time.time;
        
        Transform newHealTarget = FindLowestHealthVillager();
        
        if (newHealTarget != null)
        {
            float distance = Vector3.Distance(transform.position, newHealTarget.position);
            
            if (distance <= potionRange)
            {
                // Target is in range - no need to move, just throw potion
                healTarget = null;
                isRoaming = false;
            }
            else if (distance <= roamRadius * 2f) // Don't chase targets too far away
            {
                // Target is out of range but worth pursuing
                healTarget = newHealTarget;
            }
        }
        else
        {
            // No heal target found
            healTarget = null;
        }
    }
    
    private void UpdateRoaming()
    {
        if (isRoaming) return; // Already moving somewhere
        
        // Find a new roam destination
        Vector3 randomDirection = Random.insideUnitCircle.normalized;
        roamDestination = transform.position + (randomDirection * roamRadius);
        
        if (villagerAI != null)
        {
            villagerAI.SetFollowPosition(roamDestination);
            isRoaming = true;
        }
    }
    
    private void TryThrowPotion()
    {
        if (isThrowing) return;
        if (combatEfficiency < 0.3f) 
        {
            // Hide slider when can't operate
            SetPotionSliderVisibility(false);
            return;
        }
        
        // Check cooldown (reduced by tier)
        float cooldown = GetPotionCooldown();
        if (Time.time - lastPotionTime < cooldown) return;
        
        // Find target in range (use the in-range version)
        Transform healTargetInRange = FindHealTargetInRange();
        if (healTargetInRange == null) return;
        
        // Throw potion
        StartCoroutine(ThrowPotionCoroutine(healTargetInRange));
    }
    
    private Transform FindHealTargetInRange()
    {
        Transform bestTarget = null;
        float lowestHealthRatio = 0.95f;
        
        // Check villagers in throw range only
        Villager[] allVillagers = FindObjectsOfType<Villager>();
        foreach (Villager v in allVillagers)
        {
            if (v == villager) continue;
            if (!ShouldHealVillager(v)) continue;
            
            float distance = Vector3.Distance(transform.position, v.transform.position);
            if (distance > potionRange) continue;
            
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
        
        // Also check player in range
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            float distance = Vector3.Distance(transform.position, player.transform.position);
            if (distance <= potionRange)
            {
                PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
                if (playerHealth != null)
                {
                    float playerHealthRatio = playerHealth.GetHealthPercentage();
                    if (playerHealthRatio < lowestHealthRatio)
                    {
                        bestTarget = player.transform;
                    }
                }
            }
        }
        
        return bestTarget;
    }
    
    // Override base combat methods - mages don't fight
    protected override bool CanCombat() => false;
    protected override void UpdateTarget() { }
    protected override IEnumerator PerformAttack() { yield break; }
    
    public override void UpdateCombatStats()
    {
        // Update healing system when stats change
        if (healingSystem != null)
        {
            healingSystem.OnMageTierChanged();
        }
    }
    
    // Public getters
    public bool IsFleeing() => isFleeing;
    public bool IsThrowing() => isThrowing;
    public bool IsRoaming() => isRoaming;
    public Transform GetFleeTarget() => fleeTarget;
    public Transform GetHealTarget() => healTarget;
    public float GetPotionCooldownRemaining() => Mathf.Max(0f, GetPotionCooldown() - (Time.time - lastPotionTime));
    public float GetPotionProgress() => potionSlider != null ? potionSlider.value : 0f;
    
    protected override void OnDrawGizmosSelected()
    {
        // Draw flee range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, fleeRange);
        
        // Draw potion range
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, potionRange);
        
        // Draw roam radius
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, roamRadius);
        
        // Draw line to flee target
        if (fleeTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, fleeTarget.position);
        }
        
        // Draw line to heal target
        if (healTarget != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, healTarget.position);
        }
        
        // Draw roam destination
        if (isRoaming && roamDestination != Vector3.zero)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(roamDestination, 0.5f);
            Gizmos.DrawLine(transform.position, roamDestination);
        }
        
        // Draw throw point
        if (throwPoint != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(throwPoint.position, 0.2f);
        }
    }
}