using UnityEngine;
using UnityEngine.AI;

public class EnemyAI : MonoBehaviour
{
    [Header("AI Settings")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float stoppingDistance = 1.5f;
    [SerializeField] private float pathUpdateRate = 0.2f;
    
    [Header("Behavior")]
    [SerializeField] private bool shouldChasePlayer = true;
    [SerializeField] private float detectionRadius = 10f;
    [SerializeField] private LayerMask playerLayer = 1;
    
    [Header("Visual")]
    [SerializeField] private bool flipSpriteBasedOnDirection = true;
    
    // Components
    private NavMeshAgent agent;
    private SpriteRenderer spriteRenderer;
    private Transform target;
    
    // Internal variables
    private bool facingRight = true;
    private float lastPathUpdate;
    private Vector3 lastTargetPosition;
    
    private void Start()
    {
        SetupComponents();
        ConfigureNavMeshAgent();
        
        // Ensure we're on NavMesh after spawning
        StartCoroutine(EnsureOnNavMesh());
    }
    
    private System.Collections.IEnumerator EnsureOnNavMesh()
    {
        yield return new WaitForEndOfFrame(); // Wait for NavMesh to initialize
        
        if (agent != null && !agent.isOnNavMesh)
        {
            // Try to place agent on nearest NavMesh position
            UnityEngine.AI.NavMeshHit hit;
            if (UnityEngine.AI.NavMesh.SamplePosition(transform.position, out hit, 5f, UnityEngine.AI.NavMesh.AllAreas))
            {
                transform.position = hit.position;
                Debug.Log($"Moved {gameObject.name} to valid NavMesh position: {hit.position}");
            }
            else
            {
                Debug.LogError($"{gameObject.name} could not be placed on NavMesh! Check your NavMesh setup.");
                // Don't destroy, just disable AI
                shouldChasePlayer = false;
            }
        }
    }
    
    private void SetupComponents()
    {
        // Get NavMeshAgent component
        agent = GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            Debug.LogError($"EnemyAI: No NavMeshAgent found on {gameObject.name}! Please add a NavMeshAgent component.");
        }
        
        // Get SpriteRenderer for flipping
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            Debug.LogWarning($"EnemyAI: No SpriteRenderer found on {gameObject.name}! Sprite flipping will not work.");
        }
    }
    
    private void ConfigureNavMeshAgent()
    {
        if (agent == null) return;
        
        // Configure agent for 2D top-down movement
        agent.updateRotation = false;  // Don't rotate the GameObject
        agent.updateUpAxis = false;    // Don't use Y-axis for up direction (2D game)
        
        // Set movement parameters
        agent.speed = moveSpeed;
        agent.stoppingDistance = stoppingDistance;
        
        // CRITICAL: Lock Z position for 2D
        agent.baseOffset = 0f;
        
        // Ensure agent stays on Z = 0 plane
        Vector3 pos = transform.position;
        pos.z = 0f;
        transform.position = pos;
    }

    private void LateUpdate()
    {
        // Force Z position to stay at 0 for 2D
        if (transform.position.z != 0f)
        {
            Vector3 pos = transform.position;
            pos.z = 0f;
            transform.position = pos;
        }
        
        if (transform.rotation.eulerAngles.x != 0f)
        {
            // Reset rotation to avoid 3D rotation issues
            transform.rotation = Quaternion.Euler(0f, 0f, 0f);
        }
    }
    
    private void Update()
    {
        if (!shouldChasePlayer || target == null || agent == null) return;
        
        // Check if target is within detection radius
        float distanceToTarget = Vector3.Distance(transform.position, target.position);
        
        if (distanceToTarget <= detectionRadius)
        {
            UpdatePathfinding();
            HandleSpriteFlipping();
        }
        else
        {
            // Stop moving if target is too far
            agent.ResetPath();
        }
    }
    
    private void UpdatePathfinding()
    {
        // Only update path periodically for performance
        if (Time.time - lastPathUpdate < pathUpdateRate) return;
        
        // Check if target has moved significantly
        float targetMoveDelta = Vector3.Distance(target.position, lastTargetPosition);
        if (targetMoveDelta < 0.5f && agent.hasPath) return; // Target hasn't moved much
        
        // Ensure we're still on NavMesh
        if (!agent.isOnNavMesh)
        {
            Debug.LogWarning($"Enemy {gameObject.name} fell off NavMesh! Attempting to recover...");
            
            // Try to get back on NavMesh
            UnityEngine.AI.NavMeshHit hit;
            if (UnityEngine.AI.NavMesh.SamplePosition(transform.position, out hit, 2f, UnityEngine.AI.NavMesh.AllAreas))
            {
                transform.position = hit.position;
            }
            else
            {
                Debug.LogError($"Could not recover {gameObject.name} to NavMesh!");
                return;
            }
        }
        
        // Update path to target
        if (agent.isOnNavMesh)
        {
            agent.SetDestination(target.position);
            lastTargetPosition = target.position;
            lastPathUpdate = Time.time;
        }
    }
    
    private void HandleSpriteFlipping()
    {
        if (!flipSpriteBasedOnDirection || spriteRenderer == null || agent == null) return;
        
        // Check movement direction
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
    
    // Public methods
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        if (target != null && agent != null && agent.isOnNavMesh)
        {
            agent.SetDestination(target.position);
        }
    }
    
    public void SetMoveSpeed(float newSpeed)
    {
        moveSpeed = newSpeed;
        if (agent != null)
        {
            agent.speed = moveSpeed;
        }
    }
    
    public void SetChaseEnabled(bool enabled)
    {
        shouldChasePlayer = enabled;
        if (!enabled && agent != null)
        {
            agent.ResetPath();
        }
    }
    
    public bool IsMoving()
    {
        return agent != null && agent.velocity.magnitude > 0.1f;
    }
    
    public float GetDistanceToTarget()
    {
        return target != null ? Vector3.Distance(transform.position, target.position) : float.MaxValue;
    }
    
    // For debugging
    private void OnDrawGizmosSelected()
    {
        // Draw detection radius
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        
        // Draw path
        if (agent != null && agent.hasPath)
        {
            Gizmos.color = Color.red;
            Vector3[] path = agent.path.corners;
            for (int i = 0; i < path.Length - 1; i++)
            {
                Gizmos.DrawLine(path[i], path[i + 1]);
            }
        }
        
        // Draw line to target
        if (target != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, target.position);
        }
    }
}