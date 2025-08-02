using UnityEngine;
using System.Collections;

public class MageCombat : VillagerCombat
{
    [Header("Mage Behavior")]
    [SerializeField] private float fleeRange = 8f;
    [SerializeField] private float fleeSpeed = 6f;
    [SerializeField] private float potionRange = 15f;
    [SerializeField] private float basePotionCooldown = 3f;
    [SerializeField] private float roamRadius = 20f;
    [SerializeField] private float roamSpeed = 3f;
    
    [Header("Potion System")]
    [SerializeField] private GameObject healPotionPrefab; // Green healing potion
    [SerializeField] private GameObject damagePotionPrefab; // Red damage potion
    [SerializeField] private Transform throwPoint;
    [SerializeField] private int basePotionHeal = 15;
    [SerializeField] private int basePotionDamage = 20;
    
    [Header("Components")]
    [SerializeField] private bool autoFindComponents = true;
    
    [Header("Potion UI")]
    [SerializeField] private UnityEngine.UI.Slider potionSlider;
    [SerializeField] private bool hidePotionBarWhenReady = true;
    
    [Header("Debug")]
    [SerializeField] private bool debugHealing = false;
    
    // State
    private Transform fleeTarget;
    private Transform attackTarget; // For rebel mages targeting enemies
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
        
        // Show slider with appropriate color
        SetPotionSliderVisibility(true);
        UpdateSliderColor();
        
        if (isThrowing)
        {
            SetPotionSliderValue(0f);
        }
        else
        {
            float cooldown = GetPotionCooldown();
            float timeSinceThrow = Time.time - lastPotionTime;
            float progress = Mathf.Clamp01(timeSinceThrow / cooldown);
            
            SetPotionSliderValue(progress);
            
            if (hidePotionBarWhenReady && progress >= 1f)
            {
                SetPotionSliderVisibility(false);
            }
        }
    }
    
    private void UpdateSliderColor()
    {
        if (potionSlider?.fillRect?.GetComponent<UnityEngine.UI.Image>() == null) return;
        
        var fillImage = potionSlider.fillRect.GetComponent<UnityEngine.UI.Image>();
        
        // Green for loyal (healing), red for rebel (damage)
        if (villager != null && villager.IsRebel())
        {
            fillImage.color = Color.red;
        }
        else
        {
            fillImage.color = Color.green;
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
            potionSlider.value = 1f;
        }
    }
    
    protected override void Start()
    {
        base.Start();
        
        // Mages flee from threats
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
        
        // 1. Check for threats and targets based on faction
        UpdateTargetsBasedOnFaction();
        
        // 2. Update potion UI
        UpdatePotionSlider();
        
        // 3. Throw potions
        TryThrowPotion();
        
        // 4. Update movement
        UpdateMovement();
    }
    
    private bool CanOperate()
    {
        return villager != null && villager.IsActive();
    }
    
    private void UpdateTargetsBasedOnFaction()
    {
        if (villager.IsRebel())
        {
            // Rebel mage: flee from loyal villagers/player, attack enemies
            UpdateRebelTargets();
        }
        else
        {
            // Loyal mage: flee from enemies/rebels, heal allies
            UpdateLoyalTargets();
        }
    }
    
    private void UpdateRebelTargets()
    {
        // Find nearest loyal villager or player to flee from
        Transform nearestThreat = FindNearestLoyalTarget();
        
        if (nearestThreat != fleeTarget)
        {
            fleeTarget = nearestThreat;
            isFleeing = fleeTarget != null;
            
            if (debugHealing && isFleeing)
            {
                Debug.Log($"Rebel mage {gameObject.name} now fleeing from {fleeTarget.name}");
            }
        }
        
        // Find targets to attack with damage potions (loyal villagers/player)
        if (!isThrowing && Time.time - lastRoamUpdate > 2f)
        {
            attackTarget = FindDamageTarget();
            lastRoamUpdate = Time.time;
            
            if (debugHealing && attackTarget != null)
            {
                Debug.Log($"Rebel mage {gameObject.name} targeting {attackTarget.name} for damage potion");
            }
        }
    }
    
    private void UpdateLoyalTargets()
    {
        // Find nearest enemy or rebel to flee from
        Transform nearestThreat = FindNearestThreat();
        
        if (nearestThreat != fleeTarget)
        {
            fleeTarget = nearestThreat;
            isFleeing = fleeTarget != null;
            
            if (debugHealing && isFleeing)
            {
                Debug.Log($"Loyal mage {gameObject.name} now fleeing from {fleeTarget.name}");
            }
        }
        
        // Find targets to heal (handled by existing heal target system)
        if (!isFleeing && Time.time - lastRoamUpdate > 2f)
        {
            UpdateHealTarget();
            lastRoamUpdate = Time.time;
        }
    }
    
    private Transform FindNearestLoyalTarget()
    {
        Transform nearest = null;
        float nearestDistance = fleeRange;
        
        // Check loyal villagers
        Villager[] allVillagers = FindObjectsOfType<Villager>();
        foreach (Villager v in allVillagers)
        {
            if (v == villager || v.IsRebel()) continue;
            
            float distance = Vector3.Distance(transform.position, v.transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = v.transform;
            }
        }
        
        // Check player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            float distance = Vector3.Distance(transform.position, player.transform.position);
            if (distance < nearestDistance)
            {
                nearest = player.transform;
            }
        }
        
        return nearest;
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
        if (!villager.IsRebel())
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
    
    private Transform FindDamageTarget()
    {
        Transform bestTarget = null;
        float bestScore = float.MaxValue;
        
        // Check loyal villagers
        Villager[] allVillagers = FindObjectsOfType<Villager>();
        foreach (Villager v in allVillagers)
        {
            if (v == villager || v.IsRebel()) continue;
            
            float distance = Vector3.Distance(transform.position, v.transform.position);
            if (distance <= potionRange * 1.5f) // Slightly extended range for targeting
            {
                // Score based on distance (closer = higher priority)
                float score = distance;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestTarget = v.transform;
                }
            }
        }
        
        // Check player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            float distance = Vector3.Distance(transform.position, player.transform.position);
            if (distance <= potionRange * 1.5f)
            {
                if (distance < bestScore)
                {
                    bestTarget = player.transform;
                }
            }
        }
        
        return bestTarget;
    }

    private float GetPotionCooldown()
    {
        if (villager == null) return basePotionCooldown;

        int tier = villager.GetStats().tier;
        float reduction = tier * 0.3f;
        if (villager.IsRebel())
        {
            reduction *= rebelCooldownMultiplier; // Rebels have longer cooldowns
        }
        return Mathf.Max(0.5f, basePotionCooldown - reduction);
    }
    
    private Transform FindLowestHealthVillager()
    {
        // Only for loyal mages healing allies
        if (villager.IsRebel()) return null;
        
        Transform bestTarget = null;
        float lowestHealthRatio = 0.95f;
        float bestScore = float.MaxValue;
        
        // Check loyal villagers
        Villager[] allVillagers = FindObjectsOfType<Villager>();
        foreach (Villager v in allVillagers)
        {
            if (v == villager || v.IsRebel()) continue;
            
            float distance = Vector3.Distance(transform.position, v.transform.position);
            
            VillagerHealth vHealth = v.GetComponent<VillagerHealth>();
            if (vHealth != null)
            {
                float healthRatio = (float)vHealth.GetCurrentHP() / vHealth.GetMaxHP();
                if (healthRatio < lowestHealthRatio)
                {
                    float healthScore = healthRatio * 2f;
                    float distanceScore = distance / 30f;
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
        
        // Check player
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
    
    private IEnumerator ThrowPotionCoroutine(Transform target)
    {
        isThrowing = true;
        lastPotionTime = Time.time;
        
        SetPotionSliderValue(0f);
        SetPotionSliderVisibility(true);
        
        yield return new WaitForSeconds(0.3f);
        
        // Create and launch appropriate potion
        if (throwPoint != null && target != null)
        {
            Vector3 startPos = throwPoint.position;
            Vector3 targetPos = target.position;
            
            GameObject potionPrefab = villager.IsRebel() ? damagePotionPrefab : healPotionPrefab;
            
            if (potionPrefab != null)
            {
                GameObject potion = Instantiate(potionPrefab, startPos, Quaternion.identity);
                
                if (villager.IsRebel())
                {
                    // Launch damage potion
                    LaunchDamagePotion(potion, startPos, targetPos);
                }
                else
                {
                    // Launch heal potion
                    LaunchHealPotion(potion, startPos, targetPos);
                }
            }
        }
        
        yield return new WaitForSeconds(0.2f);
        isThrowing = false;
    }
    
    private void LaunchHealPotion(GameObject potion, Vector3 startPos, Vector3 targetPos)
    {
        PotionProjectile projectile = potion.GetComponent<PotionProjectile>();
        if (projectile == null)
        {
            projectile = potion.AddComponent<PotionProjectile>();
        }
        
        int healAmount = CalculateHealAmount();
        projectile.SetCanImpactOnVillagers(true);
        // Pass the mage's transform to prevent self-healing
        projectile.Launch(startPos, targetPos, healAmount, transform);
    }
    
    private void LaunchDamagePotion(GameObject potion, Vector3 startPos, Vector3 targetPos)
    {
        DamagePotionProjectile projectile = potion.GetComponent<DamagePotionProjectile>();
        if (projectile == null)
        {
            projectile = potion.AddComponent<DamagePotionProjectile>();
        }
        
        int damageAmount = CalculateDamageAmount();
        // Pass the mage's transform to prevent self-damage
        projectile.Launch(startPos, targetPos, damageAmount, transform);
    }
    
    private int CalculateHealAmount()
    {
        if (villager == null) return basePotionHeal;
        
        int tier = villager.GetStats().tier;
        int healAmount = basePotionHeal + (tier * 15);
        healAmount = Mathf.RoundToInt(healAmount * combatEfficiency);
        
        return Mathf.Max(1, healAmount);
    }
    
    private int CalculateDamageAmount()
    {
        if (villager == null) return basePotionDamage;
        
        int tier = villager.GetStats().tier;
        int damageAmount = basePotionDamage + (tier * 10);
        damageAmount = Mathf.RoundToInt(damageAmount * combatEfficiency);
        
        return Mathf.Max(1, damageAmount);
    }
    
    private void UpdateMovement()
    {
        if (villagerAI == null) return;
        
        if (isFleeing && fleeTarget != null)
        {
            // Fleeing has highest priority - move AWAY from the threat
            Vector3 fleeDirection = (transform.position - fleeTarget.position).normalized;
            Vector3 fleePosition = transform.position + fleeDirection * fleeRange;
            
            villagerAI.SetFollowPosition(fleePosition);
            villagerAI.SetMoveSpeed(fleeSpeed); // Use faster flee speed
            isRoaming = false;
            
            if (debugHealing)
            {
                Debug.Log($"Mage {gameObject.name} fleeing from {fleeTarget.name} to position {fleePosition}");
            }
        }
        else if (villager.IsRebel() && attackTarget != null)
        {
            // Rebel mage: move toward attack target
            float distance = Vector3.Distance(transform.position, attackTarget.position);
            
            if (distance <= potionRange)
            {
                villagerAI.ClearCombatTarget();
                attackTarget = null;
                isRoaming = false;
            }
            else
            {
                villagerAI.SetFollowPosition(attackTarget.position);
                villagerAI.SetMoveSpeed(roamSpeed); // Reset to normal speed
                isRoaming = true;
            }
        }
        else if (!villager.IsRebel())
        {
            // Loyal mage: existing heal target behavior
            villagerAI.SetMoveSpeed(roamSpeed); // Reset to normal speed
            UpdateLoyalMovement();
        }
        else
        {
            // Default roaming
            villagerAI.SetMoveSpeed(roamSpeed); // Reset to normal speed
            UpdateRoaming();
        }
    }
    
    private void UpdateLoyalMovement()
    {
        Transform healTarget = FindLowestHealthVillager();
        if (healTarget != null)
        {
            float distance = Vector3.Distance(transform.position, healTarget.position);
            
            if (distance <= potionRange)
            {
                villagerAI.ClearCombatTarget();
                isRoaming = false;
            }
            else if (distance <= roamRadius * 2f)
            {
                villagerAI.SetFollowPosition(healTarget.position);
                isRoaming = true;
            }
        }
        else
        {
            UpdateRoaming();
        }
    }
    
    private void UpdateHealTarget()
    {
        // Only for loyal mages
        if (villager.IsRebel()) return;
        
        Transform newHealTarget = FindLowestHealthVillager();
        // Implementation handled in UpdateLoyalMovement
    }
    
    private void UpdateRoaming()
    {
        if (isRoaming) return;
        
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
            SetPotionSliderVisibility(false);
            return;
        }
        
        float cooldown = GetPotionCooldown();
        if (Time.time - lastPotionTime < cooldown) return;
        
        Transform target = null;
        
        if (villager.IsRebel())
        {
            // Throw damage potions at loyal targets in range
            target = FindDamageTargetInRange();
        }
        else
        {
            // Throw heal potions at injured allies in range
            target = FindHealTargetInRange();
        }
        
        if (target != null)
        {
            StartCoroutine(ThrowPotionCoroutine(target));
        }
    }
    
    private Transform FindDamageTargetInRange()
    {
        Transform bestTarget = null;
        float nearestDistance = potionRange;
        
        // Check loyal villagers in range
        Villager[] allVillagers = FindObjectsOfType<Villager>();
        foreach (Villager v in allVillagers)
        {
            if (v == villager || v.IsRebel()) continue;
            
            float distance = Vector3.Distance(transform.position, v.transform.position);
            if (distance <= potionRange && distance < nearestDistance)
            {
                nearestDistance = distance;
                bestTarget = v.transform;
            }
        }
        
        // Check player in range
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            float distance = Vector3.Distance(transform.position, player.transform.position);
            if (distance <= potionRange && distance < nearestDistance)
            {
                bestTarget = player.transform;
            }
        }
        
        return bestTarget;
    }
    
    private Transform FindHealTargetInRange()
    {
        Transform bestTarget = null;
        float lowestHealthRatio = 0.95f;
        
        // Check loyal villagers in range
        Villager[] allVillagers = FindObjectsOfType<Villager>();
        foreach (Villager v in allVillagers)
        {
            if (v == villager || v.IsRebel()) continue;
            
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
        
        // Check player in range
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
    
    // Override base combat methods - mages don't melee
    protected override bool CanCombat() => false;
    protected override void UpdateTarget() { }
    protected override IEnumerator PerformAttack() { yield break; }
    
    public override void UpdateCombatStats()
    {
        if (healingSystem != null)
        {
            if (villager.IsRebel())
            {
                // Disable healing system for rebels
                healingSystem.SetAoEVisibility(false);
                healingSystem.OnMageBecameRebel();
            }
            else
            {
                // Re-enable healing system for loyal mages
                healingSystem.SetAoEVisibility(true);
                healingSystem.OnMageTierChanged();
            }
        }
    }
    
    // Public getters
    public bool IsFleeing() => isFleeing;
    public bool IsThrowing() => isThrowing;
    public bool IsRoaming() => isRoaming;
    public Transform GetFleeTarget() => fleeTarget;
    public Transform GetAttackTarget() => attackTarget;
    public float GetPotionCooldownRemaining() => Mathf.Max(0f, GetPotionCooldown() - (Time.time - lastPotionTime));
    
    protected override void OnDrawGizmosSelected()
    {
        // Draw flee range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, fleeRange);
        
        // Draw potion range
        if (villager != null && villager.IsRebel())
        {
            Gizmos.color = Color.red;
        }
        else
        {
            Gizmos.color = Color.green;
        }
        Gizmos.DrawWireSphere(transform.position, potionRange);
        
        // Draw line to flee target
        if (fleeTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, fleeTarget.position);
        }
        
        // Draw line to attack target (rebels)
        if (attackTarget != null && villager != null && villager.IsRebel())
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, attackTarget.position);
        }
        
        // Draw throw point
        if (throwPoint != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(throwPoint.position, 0.2f);
        }
    }
}