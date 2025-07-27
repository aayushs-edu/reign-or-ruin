using UnityEngine;
using System.Collections;

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
        }
    }
    
    protected virtual void Update()
    {
        if (!CanCombat()) return;
        
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
        
        // Loyal villagers only fight during enemy waves
        if (villager.GetState() == VillagerState.Loyal)
        {
            // Check if there are enemies present
            return GameObject.FindGameObjectsWithTag("Enemy").Length > 0;
        }
        
        return false;
    }
    
    protected virtual void UpdateTarget()
    {
        if (Time.frameCount % 30 != 0) return; // Update every 30 frames
        
        Transform nearestEnemy = FindNearestTarget();
        
        if (nearestEnemy != currentTarget)
        {
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
        GameObject[] potentialTargets = null;
        
        if (villager.GetState() == VillagerState.Rebel)
        {
            // Rebels attack player and loyal villagers
            potentialTargets = GetRebelTargets();
        }
        else
        {
            // Loyal villagers attack enemies
            potentialTargets = GameObject.FindGameObjectsWithTag("Enemy");
        }
        
        if (potentialTargets == null || potentialTargets.Length == 0) return null;
        
        Transform nearest = null;
        float nearestDistance = detectionRange;
        
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
        
        return nearest;
    }
    
    protected GameObject[] GetRebelTargets()
    {
        var targets = new System.Collections.Generic.List<GameObject>();
        
        // Add player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) targets.Add(player);
        
        // Add loyal villagers
        GameObject[] villagers = GameObject.FindGameObjectsWithTag("Villager");
        foreach (var v in villagers)
        {
            Villager vComponent = v.GetComponent<Villager>();
            if (vComponent != null && vComponent.IsLoyal())
            {
                targets.Add(v);
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
        return !isAttacking && Time.time - lastAttackTime >= currentAttackCooldown;
    }
    
    protected abstract IEnumerator PerformAttack();
    
    protected virtual void DealDamage()
    {
        if (currentTarget == null) return;
        
        // Try various health component types
        Health health = currentTarget.GetComponent<Health>();
        if (health != null)
        {
            health.TakeDamage(currentDamage);
            OnDamageDealt?.Invoke(currentDamage);
            return;
        }
        
        EnemyHealth enemyHealth = currentTarget.GetComponent<EnemyHealth>();
        if (enemyHealth != null)
        {
            enemyHealth.TakeDamage(currentDamage);
            OnDamageDealt?.Invoke(currentDamage);
            return;
        }
        
        PlayerHealth playerHealth = currentTarget.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(currentDamage);
            OnDamageDealt?.Invoke(currentDamage);
            
            // Friendly fire increases discontent
            if (villager.IsLoyal())
            {
                villager.OnHitByPlayerFriendlyFire();
            }
        }
    }
    
    public virtual void UpdateCombatStats()
    {
        if (villager == null) return;
        
        VillagerStats stats = villager.GetStats();
        
        // Update damage based on tier
        currentDamage = baseDamage + Mathf.RoundToInt(stats.tier * damagePerTier);
        
        // Update cooldown based on tier
        currentAttackCooldown = baseAttackCooldown - (stats.tier * cooldownReductionPerTier);
        currentAttackCooldown = Mathf.Max(0.5f, currentAttackCooldown); // Min 0.5s cooldown
        
        // Update range based on tier
        currentAttackRange = baseAttackRange + (stats.tier * rangeIncreasePerTier);
        
        // Apply rebel bonus
        if (villager.GetState() == VillagerState.Rebel)
        {
            currentDamage = Mathf.RoundToInt(currentDamage * 1.5f);
            currentAttackCooldown *= 0.8f;
        }
    }
    
    protected virtual void HandleRebellion(Villager v)
    {
        UpdateCombatStats();
        // Clear current target to re-evaluate
        currentTarget = null;
    }
    
    protected virtual void OnDrawGizmosSelected()
    {
        // Draw detection range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        
        // Draw attack range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, currentAttackRange > 0 ? currentAttackRange : baseAttackRange);
        
        // Draw line to target
        if (currentTarget != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, currentTarget.position);
        }
    }
    
    protected virtual void OnDestroy()
    {
        if (villager != null)
        {
            villager.OnVillagerRebel -= HandleRebellion;
        }
    }
}