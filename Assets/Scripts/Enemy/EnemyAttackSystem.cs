using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class EnemyAttackSystem : MonoBehaviour
{
    [Header("Attack Configuration")]
    [SerializeField] private GameObject fireballPrefab;
    [SerializeField] private Transform fireballSpawnPoint;
    [SerializeField] private float attackRange = 8f;
    [SerializeField] private float attackCooldown = 2f;
    [SerializeField] private float fireballSpeed = 10f;
    [SerializeField] private int damage = 1;
    
    [Header("Target Detection")]
    [SerializeField] private LayerMask attackableLayers;
    [SerializeField] private float targetUpdateInterval = 0.5f;
    [SerializeField] private bool debugTargeting = false;
    
    [Header("Attack Animation")]
    [SerializeField] private float attackAnimationTime = 0.3f;
    [SerializeField] private bool pauseMovementDuringAttack = true;
    
    // Target categories
    private readonly string[] attackableTags = { "Player", "Villager", "Building" };
    
    // State variables
    private Transform currentTarget;
    private float lastAttackTime;
    private float lastTargetUpdate;
    private bool isAttacking = false;
    
    // Components
    private EnemyAI enemyAI;
    private Animator animator;
    
    // Events
    public System.Action<Transform> OnTargetChanged;
    public System.Action<Transform> OnAttack;
    
    private void Start()
    {
        InitializeComponents();
        ValidateConfiguration();
    }
    
    private void InitializeComponents()
    {
        enemyAI = GetComponent<EnemyAI>();
        animator = GetComponent<Animator>();
        
        // Auto-create spawn point if not assigned
        if (fireballSpawnPoint == null)
        {
            GameObject spawnObj = new GameObject("FireballSpawnPoint");
            spawnObj.transform.SetParent(transform);
            spawnObj.transform.localPosition = new Vector3(0.5f, 0.5f, 0f);
            fireballSpawnPoint = spawnObj.transform;
            Debug.LogWarning($"EnemyAttackSystem: Created fireballSpawnPoint for {gameObject.name}");
        }
        
        // Set up default attackable layers if not configured
        if (attackableLayers == 0)
        {
            attackableLayers = LayerMask.GetMask("Default"); // Adjust based on your layer setup
            Debug.LogWarning("EnemyAttackSystem: No attackable layers set, using Default layer");
        }
    }
    
    private void ValidateConfiguration()
    {
        if (fireballPrefab == null)
        {
            Debug.LogError($"EnemyAttackSystem on {gameObject.name}: No fireball prefab assigned!");
        }
    }
    
    private void Update()
    {
        UpdateTargeting();
        HandleAttacking();
    }
    
    private void UpdateTargeting()
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
    
    private Transform FindClosestTarget()
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
    
    private bool HasHealthComponent(GameObject obj)
    {
        // Check for different health component types
        return obj.GetComponent<Health>() != null || 
               obj.GetComponent<PlayerHealth>() != null ||
               obj.GetComponent<VillagerHealth>() != null ||
               obj.GetComponent<BuildingHealth>() != null;
    }
    
    private void HandleAttacking()
    {
        if (currentTarget == null || isAttacking) return;
        
        float distanceToTarget = Vector3.Distance(transform.position, currentTarget.position);
        
        // Check if in range and cooldown is ready
        if (distanceToTarget <= attackRange && Time.time - lastAttackTime >= attackCooldown)
        {
            StartCoroutine(PerformAttack());
        }
    }
    
    private System.Collections.IEnumerator PerformAttack()
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
        
        // Spawn fireball if target still exists and in range
        if (currentTarget != null && Vector3.Distance(transform.position, currentTarget.position) <= attackRange * 1.2f)
        {
            SpawnFireball();
            OnAttack?.Invoke(currentTarget);
        }
        
        // Resume movement
        if (pauseMovementDuringAttack && enemyAI != null)
        {
            enemyAI.SetChaseEnabled(true);
        }
        
        isAttacking = false;
    }
    
    private void FaceTarget()
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
    
    private void SpawnFireball()
    {
        if (fireballPrefab == null || fireballSpawnPoint == null) return;
        
        GameObject fireball = Instantiate(fireballPrefab, fireballSpawnPoint.position, Quaternion.identity);
        
        // Set up fireball component
        Fireball fireballScript = fireball.GetComponent<Fireball>();
        if (fireballScript == null)
        {
            fireballScript = fireball.AddComponent<Fireball>();
        }
        
        // Calculate direction to target
        Vector3 direction = (currentTarget.position - fireballSpawnPoint.position).normalized;
        
        // Initialize fireball
        fireballScript.Initialize(direction, fireballSpeed, damage, gameObject);
        
        // Set fireball rotation to face direction
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        fireball.transform.rotation = Quaternion.Euler(0, 0, angle);
        
        if (debugTargeting)
        {
            Debug.Log($"{gameObject.name} fired at {currentTarget.name}");
        }
    }
    
    // Public methods
    public Transform GetCurrentTarget() => currentTarget;
    public bool IsInAttackRange() => currentTarget != null && Vector3.Distance(transform.position, currentTarget.position) <= attackRange;
    public float GetAttackCooldownRemaining() => Mathf.Max(0, attackCooldown - (Time.time - lastAttackTime));
    
    public void SetDamage(int newDamage)
    {
        damage = newDamage;
    }
    
    public void ForceAttack(Transform target)
    {
        currentTarget = target;
        if (enemyAI != null)
        {
            enemyAI.SetTarget(target);
        }
    }
    
    private void OnDrawGizmosSelected()
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
        
        // Draw spawn point
        if (fireballSpawnPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(fireballSpawnPoint.position, 0.2f);
        }
    }
}