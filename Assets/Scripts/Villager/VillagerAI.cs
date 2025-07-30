using UnityEngine;
using UnityEngine.AI;

public class VillagerAI : MonoBehaviour
{
    [Header("AI Settings")]
    [SerializeField] private float normalMoveSpeed = 2f;
    [SerializeField] private float combatMoveSpeed = 3f;
    [SerializeField] private float rebelMoveSpeed = 3.5f;
    [SerializeField] private float followMoveSpeed = 2.5f; // Speed when following captain
    [SerializeField] private float attackRange = 5f;
    [SerializeField] private float fleeRange = 8f;
    
    [Header("Behavior")]
    [SerializeField] private AIState currentState = AIState.Idle;
    [SerializeField] private Transform homePosition;
    [SerializeField] private float idleWanderRadius = 3f;
    
    [Header("Following System")]
    [SerializeField] private float followStoppingDistance = 1f;
    [SerializeField] private float followUpdateRate = 0.2f;
    [SerializeField] private float captainLostTimeout = 3f; // Time before giving up on lost captain
    
    public enum AIState
    {
        Idle,
        Working,
        Fleeing,
        Fighting,
        Rebel,
        MovingToTarget,
        Following,
        MovingToFollowPosition
    }
    
    // Components
    private NavMeshAgent agent;
    private Transform combatTarget;
    private Transform fleeTarget;
    private bool isRebel = false;
    private Villager villager;
    private VillagerCombat combatComponent;
    
    // Following system
    private Transform followTarget; // The captain to follow
    private Vector3 followPosition; // Specific position to move to
    private float lastFollowUpdate;
    private float captainLostTime;
    private bool hasFollowTarget = false;
    private bool hasFollowPosition = false;
    
    // Timers
    private float stateChangeTimer;
    private float nextStateChange;
    
    [Header("Debug")]
    [SerializeField] private bool debugFollowing = false;
    
    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        villager = GetComponent<Villager>();
        combatComponent = GetComponent<VillagerCombat>();
        
        if (agent != null)
        {
            agent.updateRotation = false;
            agent.updateUpAxis = false;
            agent.speed = normalMoveSpeed;
        }
        
        if (homePosition == null)
        {
            homePosition = transform;
        }
        
        nextStateChange = Time.time + Random.Range(2f, 5f);
    }
    
    private void Update()
    {
        UpdateMovementSpeed();
        
        if (isRebel)
        {
            UpdateRebelBehavior();
        }
        else if (hasFollowTarget || hasFollowPosition)
        {
            UpdateFollowBehavior();
        }
        else
        {
            UpdateNormalBehavior();
        }
    }
    
    private void UpdateMovementSpeed()
    {
        if (agent == null) return;
        
        float targetSpeed = normalMoveSpeed;
        
        switch (currentState)
        {
            case AIState.Fighting:
            case AIState.MovingToTarget:
                targetSpeed = isRebel ? rebelMoveSpeed : combatMoveSpeed;
                break;
            case AIState.Fleeing:
                targetSpeed = combatMoveSpeed * 1.2f; // Flee faster
                break;
            case AIState.Rebel:
                targetSpeed = rebelMoveSpeed;
                break;
            case AIState.Following:
            case AIState.MovingToFollowPosition:
                targetSpeed = followMoveSpeed;
                break;
            default:
                targetSpeed = normalMoveSpeed;
                break;
        }
        
        agent.speed = targetSpeed;
    }
    
    private void UpdateFollowBehavior()
    {
        // Priority 1: Combat target (followers can still fight)
        if (combatTarget != null)
        {
            currentState = AIState.MovingToTarget;
            MoveToTarget(combatTarget);
            return;
        }
        
        // Priority 2: Follow captain or move to follow position
        if (hasFollowPosition)
        {
            UpdateFollowPosition();
        }
        else if (hasFollowTarget)
        {
            UpdateFollowTarget();
        }
    }
    
    private void UpdateFollowTarget()
    {
        if (followTarget == null)
        {
            // Captain is gone, stop following
            ClearFollowTarget();
            return;
        }
        
        // Check if captain is still within reasonable distance
        float distanceToCaptain = Vector3.Distance(transform.position, followTarget.position);
        
        if (distanceToCaptain > 20f) // Captain too far away
        {
            captainLostTime += Time.deltaTime;
            if (captainLostTime >= captainLostTimeout)
            {
                if (debugFollowing)
                {
                    Debug.Log($"{gameObject.name} lost captain {followTarget.name}, stopping follow");
                }
                ClearFollowTarget();
                return;
            }
        }
        else
        {
            captainLostTime = 0f; // Reset timeout
        }
        
        // Move towards captain (but not too close)
        if (distanceToCaptain > followStoppingDistance * 2f)
        {
            currentState = AIState.Following;
            if (agent != null && agent.isOnNavMesh)
            {
                agent.SetDestination(followTarget.position);
            }
        }
        else
        {
            // Close enough, just idle near captain
            currentState = AIState.Idle;
            if (agent != null)
            {
                agent.ResetPath();
            }
        }
    }
    
    private void UpdateFollowPosition()
    {
        if (Time.time - lastFollowUpdate < followUpdateRate) return;
        lastFollowUpdate = Time.time;
        
        float distanceToPosition = Vector3.Distance(transform.position, followPosition);
        
        if (distanceToPosition > followStoppingDistance)
        {
            currentState = AIState.MovingToFollowPosition;
            if (agent != null && agent.isOnNavMesh)
            {
                agent.SetDestination(followPosition);
                
                if (debugFollowing)
                {
                    Debug.Log($"{gameObject.name} moving to follow position {followPosition}, distance: {distanceToPosition:F2}");
                }
            }
        }
        else
        {
            // Reached follow position
            currentState = AIState.Following;
            if (agent != null)
            {
                agent.ResetPath();
            }
            
            if (debugFollowing)
            {
                Debug.Log($"{gameObject.name} reached follow position");
            }
        }
    }
    
    private void UpdateNormalBehavior()
    {
        // Priority 1: Combat target - but mages flee instead of approach
        if (combatTarget != null)
        {
            // Check if this is a mage - mages flee from combat targets
            Villager villagerComponent = GetComponent<Villager>();
            if (villagerComponent != null && villagerComponent.GetRole() == VillagerRole.Mage)
            {
                currentState = AIState.Fleeing;
                FleeFrom(combatTarget); // Flee instead of approach
            }
            else
            {
                currentState = AIState.MovingToTarget;
                MoveToTarget(combatTarget); // Normal combat behavior
            }
            return;
        }
        
        // Priority 2: Check for nearby threats
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        Transform nearestThreat = FindNearest(enemies, fleeRange);
        
        if (nearestThreat != null && villager != null && villager.GetRole() != VillagerRole.Captain)
        {
            // Non-captain villagers flee from enemies
            currentState = AIState.Fleeing;
            FleeFrom(nearestThreat);
        }
        else if (Time.time > nextStateChange)
        {
            // Random idle behavior
            currentState = AIState.Idle;
            ChooseIdleBehavior();
            nextStateChange = Time.time + Random.Range(3f, 8f);
        }
    }
    
    private void UpdateRebelBehavior()
    {
        currentState = AIState.Rebel;
        
        // Rebels use their combat system to find targets
        // The combat system will call SetCombatTarget
        if (combatTarget != null)
        {
            MoveToTarget(combatTarget);
        }
        else
        {
            // Wander aggressively
            if (Time.time > nextStateChange)
            {
                WanderAggressive();
                nextStateChange = Time.time + Random.Range(2f, 4f);
            }
        }
    }
    
    private void ChooseIdleBehavior()
    {
        if (Random.Range(0f, 1f) < 0.7f)
        {
            // Wander around home
            Vector3 randomDirection = Random.insideUnitCircle * idleWanderRadius;
            Vector3 targetPos = homePosition.position + randomDirection;
            
            if (agent != null && agent.isOnNavMesh)
            {
                agent.SetDestination(targetPos);
            }
        }
        else
        {
            // Stand still
            if (agent != null)
            {
                agent.ResetPath();
            }
        }
    }
    
    private void WanderAggressive()
    {
        // Rebels wander more widely
        Vector3 randomDirection = Random.insideUnitCircle * (idleWanderRadius * 2f);
        Vector3 targetPos = transform.position + randomDirection;
        
        if (agent != null && agent.isOnNavMesh)
        {
            agent.SetDestination(targetPos);
        }
    }
    
    private void FleeFrom(Transform threat)
    {
        if (agent == null || !agent.isOnNavMesh) return;
        
        fleeTarget = threat;
        Vector3 fleeDirection = (transform.position - threat.position).normalized;
        Vector3 fleeTargetPos = transform.position + fleeDirection * fleeRange;
        
        agent.SetDestination(fleeTargetPos);
    }
    
    private void MoveToTarget(Transform target)
    {
        if (agent == null || !agent.isOnNavMesh || target == null) return;
        
        agent.SetDestination(target.position);
        
        // Check if we've reached the target
        if (agent.remainingDistance <= agent.stoppingDistance)
        {
            currentState = AIState.Fighting;
        }
    }
    
    private Transform FindNearest(GameObject[] objects, float maxRange)
    {
        Transform nearest = null;
        float nearestDistance = maxRange;
        
        foreach (GameObject obj in objects)
        {
            if (obj == gameObject) continue;
            
            // Skip other rebels if we're a rebel
            if (isRebel)
            {
                VillagerHealth vh = obj.GetComponent<VillagerHealth>();
                if (vh != null && vh.IsRebel()) continue;
            }
            
            float distance = Vector3.Distance(transform.position, obj.transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = obj.transform;
            }
        }
        
        return nearest;
    }
    
    // Following system methods
    public void SetFollowTarget(Transform captain)
    {
        followTarget = captain;
        hasFollowTarget = captain != null;
        captainLostTime = 0f;
        
        if (captain != null)
        {
            currentState = AIState.Following;
            
            if (debugFollowing)
            {
                Debug.Log($"{gameObject.name} now following captain {captain.name}");
            }
        }
        else if (debugFollowing)
        {
            Debug.Log($"{gameObject.name} stopped following captain");
        }
    }
    
    public void SetFollowPosition(Vector3 position)
    {
        followPosition = position;
        hasFollowPosition = true;
        lastFollowUpdate = 0f; // Force immediate update
        
        if (debugFollowing)
        {
            Debug.Log($"{gameObject.name} assigned follow position {position}");
        }
    }
    
    public void ClearFollowTarget()
    {
        followTarget = null;
        hasFollowTarget = false;
        hasFollowPosition = false;
        captainLostTime = 0f;
        
        currentState = AIState.Idle;
        
        if (debugFollowing)
        {
            Debug.Log($"{gameObject.name} cleared follow target and position");
        }
    }
    
    // Combat system methods (existing)
    public void SetCombatTarget(Transform target)
    {
        combatTarget = target;
        
        if (target != null)
        {
            currentState = AIState.MovingToTarget;
            
            // Set appropriate stopping distance based on role
            if (agent != null && villager != null)
            {
                switch (villager.GetRole())
                {
                    case VillagerRole.Commoner:
                        agent.stoppingDistance = 1.2f; // Melee range
                        break;
                    case VillagerRole.Mage:
                        agent.stoppingDistance = 4f; // Ranged
                        break;
                    case VillagerRole.Captain:
                        agent.stoppingDistance = 1.5f; // Slightly longer melee
                        break;
                    default:
                        agent.stoppingDistance = 1.5f;
                        break;
                }
            }
        }
        else
        {
            // Return to previous state based on following status
            if (hasFollowTarget || hasFollowPosition)
            {
                currentState = AIState.Following;
            }
            else
            {
                currentState = isRebel ? AIState.Rebel : AIState.Idle;
            }
        }
    }
    
    public void ClearCombatTarget()
    {
        combatTarget = null;
        
        // Return to appropriate state
        if (hasFollowTarget || hasFollowPosition)
        {
            currentState = AIState.Following;
        }
        else
        {
            currentState = isRebel ? AIState.Rebel : AIState.Idle;
        }
    }
    
    public void SetRebel(bool rebel)
    {
        isRebel = rebel;
        
        if (rebel)
        {
            // Rebels don't follow anyone
            ClearFollowTarget();
        }
        
        if (agent != null)
        {
            agent.speed = isRebel ? rebelMoveSpeed : normalMoveSpeed;
        }
        
        currentState = isRebel ? AIState.Rebel : AIState.Idle;
    }
    
    // Public getters
    public bool IsRebel() => isRebel;
    public AIState GetCurrentState() => currentState;
    public bool IsInCombat() => currentState == AIState.Fighting || currentState == AIState.MovingToTarget;
    public bool IsFollowing() => hasFollowTarget || hasFollowPosition;
    public Transform GetFollowTarget() => followTarget;
    public Vector3 GetFollowPosition() => followPosition;
    public float GetDistanceToTarget() => combatTarget != null ? Vector3.Distance(transform.position, combatTarget.position) : float.MaxValue;
    public float GetDistanceToFollowTarget() => followTarget != null ? Vector3.Distance(transform.position, followTarget.position) : float.MaxValue;
    public float GetDistanceToFollowPosition() => hasFollowPosition ? Vector3.Distance(transform.position, followPosition) : float.MaxValue;
    
    private void OnDrawGizmosSelected()
    {
        // Draw home position
        if (homePosition != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(homePosition.position, 0.5f);
            Gizmos.DrawWireSphere(homePosition.position, idleWanderRadius);
        }
        
        // Draw flee range
        if (!isRebel && !hasFollowTarget)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, fleeRange);
        }
        
        // Draw path
        if (agent != null && agent.hasPath)
        {
            Color pathColor = Color.green;
            
            switch (currentState)
            {
                case AIState.Fleeing:
                    pathColor = Color.yellow;
                    break;
                case AIState.Following:
                case AIState.MovingToFollowPosition:
                    pathColor = Color.cyan;
                    break;
                case AIState.Rebel:
                    pathColor = Color.red;
                    break;
                case AIState.MovingToTarget:
                    pathColor = Color.black;
                    break;
            }
            
            Gizmos.color = pathColor;
            Vector3[] path = agent.path.corners;
            for (int i = 0; i < path.Length - 1; i++)
            {
                Gizmos.DrawLine(path[i], path[i + 1]);
            }
        }
        
        // Draw line to combat target
        if (combatTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, combatTarget.position);
        }
        
        // Draw line to follow target
        if (followTarget != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, followTarget.position);
            
            // Draw follow stopping distance
            Gizmos.DrawWireSphere(followTarget.position, followStoppingDistance * 2f);
        }
        
        // Draw follow position
        if (hasFollowPosition)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(followPosition, followStoppingDistance);
            Gizmos.DrawLine(transform.position, followPosition);
        }
        
        // Draw state indicator
        Vector3 statePos = transform.position + Vector3.up * 2f;
        Gizmos.color = Color.white;
        
        // This would show in scene view
        #if UNITY_EDITOR
                UnityEditor.Handles.Label(statePos, $"{currentState}\n{(IsFollowing() ? "Following" : "")}\n{(isRebel ? "Rebel" : "Loyal")}");
                #endif
    }
}