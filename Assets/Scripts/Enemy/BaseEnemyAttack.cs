using UnityEngine;
using System.Collections.Generic;

// Abstract base class for all enemy attack behaviors
public abstract class BaseEnemyAttack : MonoBehaviour
{
    [Header("Base Attack Configuration")]
    [SerializeField] protected float attackRange = 8f;
    [SerializeField] protected float attackCooldown = 2f;
    [SerializeField] protected int damage = 1;
    [SerializeField] protected LayerMask attackableLayers;
    
    [Header("Target Detection")]
    [SerializeField] protected float targetUpdateInterval = 0.5f;
    [SerializeField] protected bool debugTargeting = false;
    
    [Header("Attack Animation")]
    [SerializeField] protected float attackAnimationTime = 0.3f;
    [SerializeField] protected bool pauseMovementDuringAttack = true;
    
    // Target categories
    protected readonly string[] attackableTags = { "Player", "Villager", "Building" };
    
    // State variables
    protected Transform currentTarget;
    protected float lastAttackTime;
    protected float lastTargetUpdate;
    protected bool isAttacking = false;
    
    // Components
    protected EnemyAI enemyAI;
    protected Animator animator;
    
    // Events
    public System.Action<Transform> OnTargetChanged;
    public System.Action<Transform> OnAttack;
    
    protected virtual void Start()
    {
        InitializeComponents();
        ValidateConfiguration();
    }
    
    protected virtual void InitializeComponents()
    {
        enemyAI = GetComponent<EnemyAI>();
        animator = GetComponent<Animator>();
        
        // Set up default attackable layers if not configured
        if (attackableLayers == 0)
        {
            attackableLayers = LayerMask.GetMask("Default");
            Debug.LogWarning($"{GetType().Name}: No attackable layers set, using Default layer");
        }
    }
    
    protected virtual void ValidateConfiguration()
    {
        // Override in derived classes to validate specific attack requirements
    }
    
    protected virtual void Update()
    {
        UpdateTargeting();
        HandleAttacking();
    }
    
    protected virtual void UpdateTargeting()
    {
        if (Time.time - lastTargetUpdate < targetUpdateInterval) return;
        
        lastTargetUpdate = Time.time;
        
        Transform newTarget = FindClosestTarget();
        
        if (newTarget != currentTarget)
        {
            currentTarget = newTarget;
            OnTargetChanged?.Invoke(currentTarget);
            
            // Update EnemyAI to move towards new target
            if (enemyAI != null && currentTarget != null)
            {
                enemyAI.SetTarget(currentTarget);
            }
        }
    }
    
    protected virtual Transform FindClosestTarget()
    {
        List<Transform> potentialTargets = new List<Transform>();
        
        // Find all potential targets
        foreach (string tag in attackableTags)
        {
            GameObject[] objects = GameObject.FindGameObjectsWithTag(tag);
            foreach (GameObject obj in objects)
            {
                // Check if object has a health component (attackable)
                if (HasHealthComponent(obj))
                {
                    potentialTargets.Add(obj.transform);
                }
            }
        }
        
        // Find closest target
        Transform closest = null;
        float closestDistance = float.MaxValue;
        
        foreach (Transform target in potentialTargets)
        {
            float distance = Vector3.Distance(transform.position, target.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = target;
            }
        }
        
        return closest;
    }
    
    protected virtual bool HasHealthComponent(GameObject obj)
    {
        // Check for different health component types
        return obj.GetComponent<Health>() != null || 
               obj.GetComponent<PlayerHealth>() != null ||
               obj.GetComponent<VillagerHealth>() != null ||
               obj.GetComponent<BuildingHealth>() != null;
    }
    
    protected virtual void HandleAttacking()
    {
        if (currentTarget == null || isAttacking) return;
        
        float distanceToTarget = Vector3.Distance(transform.position, currentTarget.position);
        
        // Check if in range and cooldown is ready
        if (distanceToTarget <= attackRange && Time.time - lastAttackTime >= attackCooldown)
        {
            StartCoroutine(PerformAttack());
        }
    }
    
    protected virtual System.Collections.IEnumerator PerformAttack()
    {
        isAttacking = true;
        lastAttackTime = Time.time;
        
        // Pause movement if configured
        if (pauseMovementDuringAttack && enemyAI != null)
        {
            enemyAI.SetChaseEnabled(false);
        }
        
        // Play attack animation if available
        if (animator != null)
        {
            animator.SetTrigger("Attack");
        }
        
        // Face target
        FaceTarget();
        
        // Wait for attack animation wind-up
        yield return new WaitForSeconds(attackAnimationTime);
        
        // Execute the specific attack (implemented by derived classes)
        if (currentTarget != null && IsTargetInRange())
        {
            ExecuteAttack();
            OnAttack?.Invoke(currentTarget);
        }
        
        // Resume movement
        if (pauseMovementDuringAttack && enemyAI != null)
        {
            enemyAI.SetChaseEnabled(true);
        }
        
        isAttacking = false;
    }
    
    protected virtual bool IsTargetInRange()
    {
        return currentTarget != null && 
               Vector3.Distance(transform.position, currentTarget.position) <= attackRange * 1.2f;
    }
    
    protected virtual void FaceTarget()
    {
        if (currentTarget == null) return;
        
        Vector3 direction = (currentTarget.position - transform.position).normalized;
        direction.z = 0f; // Keep 2D
        
        // Update sprite facing if needed
        SpriteRenderer sprite = GetComponent<SpriteRenderer>();
        if (sprite != null)
        {
            sprite.flipX = direction.x < 0;
        }
    }
    
    // Abstract method - must be implemented by derived classes
    protected abstract void ExecuteAttack();
    
    // Utility method for damaging targets
    protected virtual bool TryDamageTarget(GameObject target, int damageAmount = -1)
    {
        int actualDamage = damageAmount == -1 ? damage : damageAmount;
        
        // Check for various health component types
        Health health = target.GetComponent<Health>();
        if (health != null)
        {
            health.TakeDamage(actualDamage);
            return true;
        }
        
        EnemyHealth enemyHealth = target.GetComponent<EnemyHealth>();
        if (enemyHealth != null)
        {
            enemyHealth.TakeDamage(actualDamage);
            return true;
        }
        
        PlayerHealth playerHealth = target.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(actualDamage);
            return true;
        }
        
        VillagerHealth villagerHealth = target.GetComponent<VillagerHealth>();
        if (villagerHealth != null)
        {
            villagerHealth.TakeDamage(actualDamage);
            return true;
        }
        
        BuildingHealth buildingHealth = target.GetComponent<BuildingHealth>();
        if (buildingHealth != null)
        {
            buildingHealth.TakeDamage(actualDamage);
            return true;
        }
        
        // Check if it's a valid target even if no health component
        if (target.CompareTag("Player") || target.CompareTag("Villager") || target.CompareTag("Building"))
        {
            Debug.LogWarning($"Attack hit {target.name} but no health component found!");
            return true;
        }
        
        return false;
    }
    
    // Public methods
    public virtual Transform GetCurrentTarget() => currentTarget;
    public virtual bool IsInAttackRange() => currentTarget != null && Vector3.Distance(transform.position, currentTarget.position) <= attackRange;
    public virtual float GetAttackCooldownRemaining() => Mathf.Max(0, attackCooldown - (Time.time - lastAttackTime));
    
    public virtual void SetDamage(int newDamage)
    {
        damage = newDamage;
    }
    
    public virtual void SetAttackRange(float newRange)
    {
        attackRange = newRange;
    }
    
    public virtual void SetAttackCooldown(float newCooldown)
    {
        attackCooldown = newCooldown;
    }
    
    public virtual void ForceAttack(Transform target)
    {
        currentTarget = target;
        if (enemyAI != null)
        {
            enemyAI.SetTarget(target);
        }
    }
    
    protected virtual void OnDrawGizmosSelected()
    {
        // Draw attack range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        
        // Draw line to current target
        if (currentTarget != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, currentTarget.position);
        }
    }
}