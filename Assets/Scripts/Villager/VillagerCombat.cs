using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public abstract class VillagerCombat : MonoBehaviour
{
    [Header("Combat Configuration")]
    [SerializeField] protected float baseAttackRange = 1.5f;
    [SerializeField] protected float baseAttackCooldown = 2f;
    [SerializeField] protected int baseDamage = 5;
    [SerializeField] protected float detectionRange = 8f;
    [SerializeField] protected LayerMask enemyLayers;

    [Header("Power Scaling")]
    [SerializeField] protected float damagePerTier = 5f;
    [SerializeField] protected float cooldownReductionPerTier = 0.3f;
    [SerializeField] protected float rangeIncreasePerTier = 0.2f;

    [Header("Combat Efficiency")]
    [SerializeField] protected float combatEfficiency = 1f; // 1 = full efficiency, 0.5 = half

    [Header("Rebel Combat")]
    [SerializeField] protected float rebelDamageMultiplier = 1.5f;
    [SerializeField] protected float rebelCooldownMultiplier = 0.8f; // Rebels

    [Header("Debug")]
    [SerializeField] protected bool debugTargeting = false;

    // Components
    protected Villager villager;
    protected VillagerAI villagerAI;
    protected Transform currentTarget;
    protected Animator animator;

    // Combat state
    protected bool isAttacking = false;
    protected float lastAttackTime;
    protected float currentAttackCooldown;
    protected int currentDamage;
    protected float currentAttackRange;

    // Mage buff system
    protected Dictionary<MageHealingSystem, float> mageDamageBuffs = new Dictionary<MageHealingSystem, float>();
    protected float totalMageDamageMultiplier = 1f;

    // Events
    public System.Action<Transform> OnTargetAcquired;
    public System.Action<int> OnDamageDealt;

    protected virtual void Awake()
    {
        villager = GetComponent<Villager>();
        villagerAI = GetComponent<VillagerAI>();
        animator = GetComponent<Animator>();

        if (villager == null)
        {
            Debug.LogError($"VillagerCombat: No Villager component found on {gameObject.name}!");
        }
    }

    protected virtual void Start()
    {
        UpdateCombatStats();
        SetupEventListeners();
    }

    protected virtual void SetupEventListeners()
    {
        if (villager != null)
        {
            // Listen for power changes to update combat stats
            villager.OnVillagerRebel += HandleRebellion;
            villager.OnStateChanged += HandleStateChanged;
        }
    }

    protected virtual void Update()
    {
        if (!CanCombat()) return;

        UpdateMageBuffs();
        UpdateTarget();

        if (currentTarget != null)
        {
            HandleCombat();
        }
        else if (villagerAI != null)
        {
            // Clear target if no enemies found
            villagerAI.ClearCombatTarget();
        }
    }

    protected virtual bool CanCombat()
    {
        // Can't combat if not active or if building is destroyed
        if (villager == null || !villager.IsActive()) return false;

        // Only rebels and loyal villagers during waves can combat
        if (villager.GetState() == VillagerState.Rebel) return true;

        // Loyal and angry villagers fight during enemy waves AND against rebels
        if (villager.GetState() == VillagerState.Loyal || villager.GetState() == VillagerState.Angry)
        {
            // Check if there are enemies present OR rebels present
            bool hasEnemies = GameObject.FindGameObjectsWithTag("Enemy").Length > 0;
            bool hasRebels = HasNearbyRebels();

            return hasEnemies || hasRebels;
        }

        return false;
    }

    private bool HasNearbyRebels()
    {
        // Check for rebel villagers in detection range
        Villager[] allVillagers = FindObjectsOfType<Villager>();
        foreach (var v in allVillagers)
        {
            if (v != villager && v.IsRebel())
            {
                float distance = Vector3.Distance(transform.position, v.transform.position);
                if (distance <= detectionRange)
                {
                    return true;
                }
            }
        }
        return false;
    }

    protected virtual void UpdateTarget()
    {
        // Angry villagers update targets less frequently (reduced efficiency)
        int frameInterval = villager.IsAngry() ? 60 : 30; // Update every 60 frames if angry, 30 if not

        if (Time.frameCount % frameInterval != 0) return;

        Transform nearestEnemy = FindNearestTarget();

        if (nearestEnemy != currentTarget)
        {
            if (debugTargeting)
            {
                Debug.Log($"{villager.name} ({villager.GetState()}) target changed from {(currentTarget?.name ?? "none")} to {(nearestEnemy?.name ?? "none")}");
            }

            currentTarget = nearestEnemy;
            OnTargetAcquired?.Invoke(currentTarget);

            // Update AI to move toward target
            if (villagerAI != null && currentTarget != null)
            {
                villagerAI.SetCombatTarget(currentTarget);
            }
        }
    }

    protected virtual Transform FindNearestTarget()
    {
        Transform nearest = null;
        float nearestDistance = detectionRange * combatEfficiency; // Reduced detection range when angry

        if (villager.GetState() == VillagerState.Rebel)
        {
            // Rebels attack player and loyal villagers
            var potentialTargets = GetRebelTargets();

            foreach (GameObject target in potentialTargets)
            {
                if (target == gameObject) continue;

                float distance = Vector3.Distance(transform.position, target.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = target.transform;
                }
            }
        }
        else
        {
            // Loyal and angry villagers attack enemies AND rebels

            // First check for traditional enemies
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

            // IMPORTANT: Also check for rebel villagers
            Villager[] allVillagers = FindObjectsOfType<Villager>();
            foreach (Villager targetVillager in allVillagers)
            {
                if (targetVillager == villager) continue; // Skip self

                // Attack rebel villagers
                if (targetVillager.IsRebel())
                {
                    float distance = Vector3.Distance(transform.position, targetVillager.transform.position);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearest = targetVillager.transform;

                        if (debugTargeting)
                        {
                            Debug.Log($"Loyal {villager.name} targeting rebel {targetVillager.name} at distance {distance}");
                        }
                    }
                }
            }
        }

        return nearest;
    }

    protected GameObject[] GetRebelTargets()
    {
        var targets = new System.Collections.Generic.List<GameObject>();

        // Add player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) targets.Add(player);

        // Add loyal villagers
        Villager[] allVillagers = FindObjectsOfType<Villager>();
        foreach (Villager v in allVillagers)
        {
            if (v != villager && v.IsLoyal())
            {
                targets.Add(v.gameObject);
            }
        }

        return targets.ToArray();
    }

    protected virtual void HandleCombat()
    {
        float distanceToTarget = Vector3.Distance(transform.position, currentTarget.position);

        if (distanceToTarget <= currentAttackRange && CanAttack())
        {
            StartCoroutine(PerformAttack());
        }
    }

    protected virtual bool CanAttack()
    {
        // Apply efficiency to attack cooldown (angry villagers attack slower)
        float effectiveCooldown = currentAttackCooldown / combatEfficiency;
        return !isAttacking && Time.time - lastAttackTime >= effectiveCooldown;
    }

    protected abstract System.Collections.IEnumerator PerformAttack();

    protected virtual void DealDamage()
    {
        if (currentTarget == null) return;

        // Apply efficiency to damage
        int effectiveDamage = Mathf.RoundToInt(currentDamage * combatEfficiency);

        // Try various health component types
        Health health = currentTarget.GetComponent<Health>();
        if (health != null)
        {
            health.TakeDamage(effectiveDamage);
            OnDamageDealt?.Invoke(effectiveDamage);
            return;
        }

        EnemyHealth enemyHealth = currentTarget.GetComponent<EnemyHealth>();
        if (enemyHealth != null)
        {
            enemyHealth.TakeDamage(effectiveDamage);
            OnDamageDealt?.Invoke(effectiveDamage);
            return;
        }

        PlayerHealth playerHealth = currentTarget.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(effectiveDamage);
            OnDamageDealt?.Invoke(effectiveDamage);

            // Friendly fire increases discontent
            if (villager.IsLoyal() || villager.IsAngry())
            {
                villager.OnHitByPlayerFriendlyFire();
            }
        }

        VillagerHealth villagerHealth = currentTarget.GetComponent<VillagerHealth>();
        if (villagerHealth != null)
        {
            villagerHealth.TakeDamage(effectiveDamage, gameObject);
            OnDamageDealt?.Invoke(effectiveDamage);
        }
    }

    // Mage buff system methods
    protected virtual void UpdateMageBuffs()
    {
        // Clean up expired buffs and calculate total multiplier
        var expiredBuffs = new List<MageHealingSystem>();
        totalMageDamageMultiplier = 1f;

        foreach (var kvp in mageDamageBuffs)
        {
            if (kvp.Key == null || kvp.Value <= Time.time)
            {
                expiredBuffs.Add(kvp.Key);
            }
            else
            {
                // Add buff multiplier
                totalMageDamageMultiplier *= kvp.Key.GetDamageBuffMultiplier();
            }
        }

        // Remove expired buffs
        foreach (var expired in expiredBuffs)
        {
            mageDamageBuffs.Remove(expired);
        }
    }

    public virtual void ApplyMageDamageBuff(MageHealingSystem mageSystem, float duration)
    {
        if (mageSystem == null) return;

        float expiryTime = Time.time + duration;
        mageDamageBuffs[mageSystem] = expiryTime;

        if (debugTargeting)
        {
            Debug.Log($"{gameObject.name} received mage damage buff from {mageSystem.name} for {duration}s");
        }
    }

    public virtual void RemoveMageDamageBuff(MageHealingSystem mageSystem)
    {
        if (mageDamageBuffs.ContainsKey(mageSystem))
        {
            mageDamageBuffs.Remove(mageSystem);

            if (debugTargeting)
            {
                Debug.Log($"{gameObject.name} lost mage damage buff from {mageSystem.name}");
            }
        }
    }

    public virtual bool HasMageDamageBuff(MageHealingSystem mageSystem)
    {
        return mageDamageBuffs.ContainsKey(mageSystem) && mageDamageBuffs[mageSystem] > Time.time;
    }

    public virtual float GetTotalMageDamageMultiplier() => totalMageDamageMultiplier;

    public virtual void UpdateCombatStats()
    {
        if (villager == null) return;

        VillagerStats stats = villager.GetStats();

        // Update damage based on tier
        currentDamage = baseDamage + Mathf.RoundToInt(stats.tier * damagePerTier);

        // Apply mage damage buffs
        currentDamage = Mathf.RoundToInt(currentDamage * totalMageDamageMultiplier);

        // Update cooldown based on tier
        currentAttackCooldown = baseAttackCooldown - (stats.tier * cooldownReductionPerTier);
        currentAttackCooldown = Mathf.Max(0.5f, currentAttackCooldown); // Min 0.5s cooldown

        // Update range based on tier
        currentAttackRange = baseAttackRange + (stats.tier * rangeIncreasePerTier);

        // Apply rebel bonus
        if (villager.GetState() == VillagerState.Rebel)
        {
            currentDamage = Mathf.RoundToInt(currentDamage * rebelDamageMultiplier);
            currentAttackCooldown *= rebelCooldownMultiplier;
        }
    }

    public void SetCombatEfficiency(float efficiency)
    {
        combatEfficiency = Mathf.Clamp01(efficiency);
        // Debug.Log($"{gameObject.name} combat efficiency set to {combatEfficiency:P0}");
    }

    protected virtual void HandleRebellion(Villager v)
    {
        combatEfficiency = 1f; // Rebels fight at full efficiency
        UpdateCombatStats();
        // Clear current target to re-evaluate
        currentTarget = null;

        // Clear all mage buffs when becoming rebel (mages won't buff rebels)
        mageDamageBuffs.Clear();
        totalMageDamageMultiplier = 1f;
    }

    protected virtual void HandleStateChanged(Villager v, VillagerState newState)
    {
        // Clear target when state changes to force re-evaluation
        currentTarget = null;

        if (debugTargeting)
        {
            Debug.Log($"{v.name} state changed to {newState}, clearing combat target");
        }
    }

    protected virtual void OnDrawGizmosSelected()
    {
        // Draw detection range (affected by efficiency)
        Gizmos.color = Color.yellow;
        float effectiveDetectionRange = detectionRange * combatEfficiency;
        Gizmos.DrawWireSphere(transform.position, effectiveDetectionRange);

        // Draw attack range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, currentAttackRange > 0 ? currentAttackRange : baseAttackRange);

        // Draw line to target
        if (currentTarget != null)
        {
            Gizmos.color = villager != null && villager.IsRebel() ? Color.red : Color.magenta;
            Gizmos.DrawLine(transform.position, currentTarget.position);
        }

        // Draw mage buff indicator
        if (Application.isPlaying && mageDamageBuffs.Count > 0)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 1.5f, 0.3f);
        }
    }

    protected virtual void OnDestroy()
    {
        if (villager != null)
        {
            villager.OnVillagerRebel -= HandleRebellion;
            villager.OnStateChanged -= HandleStateChanged;
        }

        // Clear mage buffs
        mageDamageBuffs.Clear();
    }

    public bool IsAttacking() => isAttacking;
    public float GetEfficiency() => combatEfficiency;

    // Public getters for mage buff system
    public int GetMageBuffCount() => mageDamageBuffs.Count;
    public Dictionary<MageHealingSystem, float> GetMageBuffs() => new Dictionary<MageHealingSystem, float>(mageDamageBuffs);
}