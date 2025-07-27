using UnityEngine;
using UnityEngine.AI;

public class VillagerAI : MonoBehaviour
{
    [Header("AI Settings")]
    [SerializeField] private float normalMoveSpeed = 2f;
    [SerializeField] private float combatMoveSpeed = 3f;
    [SerializeField] private float rebelMoveSpeed = 3.5f;
    [SerializeField] private float attackRange = 5f;
    [SerializeField] private float fleeRange = 8f;
    
    [Header("Behavior")]
    [SerializeField] private AIState currentState = AIState.Idle;
    [SerializeField] private Transform homePosition;
    [SerializeField] private float idleWanderRadius = 3f;
    
    public enum AIState
    {
        Idle,
        Working,
        Fleeing,
        Fighting,
        Rebel,
        MovingToTarget
    }
    
    // Components
    private NavMeshAgent agent;
    private Transform combatTarget;
    private Transform fleeTarget;
    private bool isRebel = false;
    private Villager villager;
    private VillagerCombat combatComponent;
    
    // Timers
    private float stateChangeTimer;
    private float nextStateChange;
    
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
            default:
                targetSpeed = normalMoveSpeed;
                break;
        }
        
        agent.speed = targetSpeed;
    }
    
    private void UpdateNormalBehavior()
    {
        // Priority 1: Combat target
        if (combatTarget != null)
        {
            currentState = AIState.MovingToTarget;
            MoveToTarget(combatTarget);
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
            currentState = isRebel ? AIState.Rebel : AIState.Idle;
        }
    }
    
    public void ClearCombatTarget()
    {
        combatTarget = null;
        currentState = isRebel ? AIState.Rebel : AIState.Idle;
    }
    
    public void SetRebel(bool rebel)
    {
        isRebel = rebel;
        
        if (agent != null)
        {
            agent.speed = isRebel ? rebelMoveSpeed : normalMoveSpeed;
        }
        
        currentState = isRebel ? AIState.Rebel : AIState.Idle;
    }
    
    public bool IsRebel() => isRebel;
    public AIState GetCurrentState() => currentState;
    public bool IsInCombat() => currentState == AIState.Fighting || currentState == AIState.MovingToTarget;
    public float GetDistanceToTarget() => combatTarget != null ? Vector3.Distance(transform.position, combatTarget.position) : float.MaxValue;
    
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
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, fleeRange);
        
        // Draw path
        if (agent != null && agent.hasPath)
        {
            Gizmos.color = currentState == AIState.Fleeing ? Color.yellow : Color.green;
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
    }
}