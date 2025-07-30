using UnityEngine;
using UnityEngine.AI;

public class RangedEnemyAI : MonoBehaviour
{
    [Header("AI Settings")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float preferredDistance = 6f;
    [SerializeField] private float minimumDistance = 3f;
    
    [Header("Target Detection")]
    [SerializeField] private float detectionRadius = 12f;
    [SerializeField] private bool flipSpriteBasedOnDirection = true;
    
    // Components
    private NavMeshAgent agent;
    private SpriteRenderer spriteRenderer;
    private BaseEnemyAttack attackSystem;
    
    // State
    private Transform target;
    private bool facingRight = true;
    private bool isAttacking = false;
    
    // Target categories
    private readonly string[] targetTags = { "Player", "Villager" };
    
    private void Start()
    {
        SetupComponents();
        ConfigureAgent();
    }
    
    private void SetupComponents()
    {
        agent = GetComponent<NavMeshAgent>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        attackSystem = GetComponent<BaseEnemyAttack>();
        
        if (agent == null)
        {
            Debug.LogError($"RangedEnemyAI: No NavMeshAgent on {gameObject.name}!");
            enabled = false;
        }
    }
    
    private void ConfigureAgent()
    {
        if (agent == null) return;
        
        agent.updateRotation = false;
        agent.updateUpAxis = false;
        agent.speed = moveSpeed;
        agent.stoppingDistance = 0.1f;
        agent.baseOffset = 0f;
        
        // Ensure on NavMesh
        Vector3 pos = transform.position;
        pos.z = 0f;
        transform.position = pos;
    }
    
    private void Update()
    {
        FindTarget();
        UpdateMovement();
        UpdateAttackState();
        HandleSpriteFlipping();
        
        // Keep Z at 0
        if (transform.position.z != 0f)
        {
            Vector3 pos = transform.position;
            pos.z = 0f;
            transform.position = pos;
        }
    }
    
    private void FindTarget()
    {
        Transform closest = null;
        float closestDistance = detectionRadius;
        
        foreach (string tag in targetTags)
        {
            GameObject[] targets = GameObject.FindGameObjectsWithTag(tag);
            
            foreach (GameObject obj in targets)
            {
                float distance = Vector3.Distance(transform.position, obj.transform.position);
                if (distance < closestDistance && HasHealthComponent(obj))
                {
                    closestDistance = distance;
                    closest = obj.transform;
                }
            }
        }
        
        target = closest;
    }
    
    private bool HasHealthComponent(GameObject obj)
    {
        return obj.GetComponent<Health>() != null || 
               obj.GetComponent<PlayerHealth>() != null ||
               obj.GetComponent<VillagerHealth>() != null;
    }
    
    private void UpdateMovement()
    {
        if (target == null || agent == null || !agent.isOnNavMesh)
        {
            agent.ResetPath();
            return;
        }
        
        // Don't move while attacking
        if (isAttacking)
        {
            agent.ResetPath();
            return;
        }
        
        float distanceToTarget = Vector3.Distance(transform.position, target.position);
        Vector3 destination;
        
        if (distanceToTarget < minimumDistance)
        {
            // RETREAT: Move away from target
            Vector3 retreatDirection = (transform.position - target.position).normalized;
            destination = transform.position + retreatDirection * preferredDistance;
            
            // Increase speed when retreating
            agent.speed = moveSpeed * 1.5f;
        }
        else if (distanceToTarget > preferredDistance + 2f)
        {
            // ADVANCE: Move closer to target
            Vector3 advanceDirection = (target.position - transform.position).normalized;
            destination = target.position - advanceDirection * preferredDistance;
            
            agent.speed = moveSpeed;
        }
        else
        {
            // IN RANGE: Hold position
            agent.ResetPath();
            return;
        }
        
        // Find valid NavMesh position
        NavMeshHit hit;
        if (NavMesh.SamplePosition(destination, out hit, 5f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
        else
        {
            // Fallback: try a simpler retreat
            if (distanceToTarget < minimumDistance)
            {
                Vector3 simpleRetreat = transform.position + (transform.position - target.position).normalized * 3f;
                if (NavMesh.SamplePosition(simpleRetreat, out hit, 3f, NavMesh.AllAreas))
                {
                    agent.SetDestination(hit.position);
                }
            }
        }
    }
    
    private void UpdateAttackState()
    {
        if (attackSystem == null) return;
        
        // Simple attack state tracking
        bool wasAttacking = isAttacking;
        isAttacking = attackSystem.IsAttacking();
        
        // Resume movement after attack finishes
        if (wasAttacking && !isAttacking)
        {
            agent.speed = moveSpeed; // Reset speed
        }
    }
    
    private void HandleSpriteFlipping()
    {
        if (!flipSpriteBasedOnDirection || spriteRenderer == null || agent == null) return;
        
        if (agent.velocity.x > 0.1f && !facingRight)
        {
            FlipSprite();
        }
        else if (agent.velocity.x < -0.1f && facingRight)
        {
            FlipSprite();
        }
    }
    
    private void FlipSprite()
    {
        facingRight = !facingRight;
        spriteRenderer.flipX = !facingRight;
    }
    
    // Public interface (compatible with EnemyAI)
    public void SetTarget(Transform newTarget) => target = newTarget;
    public void SetMoveSpeed(float newSpeed) => moveSpeed = newSpeed;
    public void SetChaseEnabled(bool enabled) => enabled = enabled;
    public bool IsMoving() => agent != null && agent.velocity.magnitude > 0.1f;
    public float GetDistanceToTarget() => target != null ? Vector3.Distance(transform.position, target.position) : float.MaxValue;
    public Transform GetCurrentTarget() => target;
    
    // Debug visualization
    private void OnDrawGizmosSelected()
    {
        // Detection radius
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        
        if (target != null)
        {
            // Minimum distance (retreat threshold)
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(target.position, minimumDistance);
            
            // Preferred distance
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(target.position, preferredDistance);
            
            // Line to target
            float distance = Vector3.Distance(transform.position, target.position);
            Gizmos.color = distance < minimumDistance ? Color.red : 
                          distance > preferredDistance + 2f ? Color.yellow : Color.green;
            Gizmos.DrawLine(transform.position, target.position);
        }
        
        // Current path
        if (agent != null && agent.hasPath)
        {
            Gizmos.color = Color.white;
            Vector3[] path = agent.path.corners;
            for (int i = 0; i < path.Length - 1; i++)
            {
                Gizmos.DrawLine(path[i], path[i + 1]);
            }
        }
    }
}