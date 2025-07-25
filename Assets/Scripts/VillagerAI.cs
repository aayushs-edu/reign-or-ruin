using UnityEngine;
using UnityEngine.AI;

public class VillagerAI : MonoBehaviour
{
    [Header("AI Settings")]
    [SerializeField] private float normalMoveSpeed = 2f;
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
        Rebel
    }
    
    // Components
    private NavMeshAgent agent;
    private Transform target;
    private bool isRebel = false;
    
    // Timers
    private float stateChangeTimer;
    private float nextStateChange;
    
    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();
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
        if (isRebel)
        {
            UpdateRebelBehavior();
        }
        else
        {
            UpdateNormalBehavior();
        }
    }
    
    private void UpdateNormalBehavior()
    {
        // Check for nearby threats
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        Transform nearestThreat = FindNearest(enemies, fleeRange);
        
        if (nearestThreat != null)
        {
            currentState = AIState.Fleeing;
            FleeFrom(nearestThreat);
        }
        else if (Time.time > nextStateChange)
        {
            // Random idle behavior
            ChooseIdleBehavior();
            nextStateChange = Time.time + Random.Range(3f, 8f);
        }
    }
    
    private void UpdateRebelBehavior()
    {
        currentState = AIState.Rebel;
        
        // Find nearest player or loyal villager to attack
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        GameObject[] villagers = GameObject.FindGameObjectsWithTag("Villager");
        
        Transform nearestTarget = player != null ? player.transform : null;
        Transform nearestVillager = FindNearest(villagers, attackRange * 2f);
        
        if (nearestVillager != null)
        {
            float distToPlayer = nearestTarget != null ? Vector3.Distance(transform.position, nearestTarget.position) : float.MaxValue;
            float distToVillager = Vector3.Distance(transform.position, nearestVillager.position);
            
            if (distToVillager < distToPlayer)
            {
                nearestTarget = nearestVillager;
            }
        }
        
        if (nearestTarget != null)
        {
            MoveToTarget(nearestTarget);
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
    
    private void FleeFrom(Transform threat)
    {
        if (agent == null || !agent.isOnNavMesh) return;
        
        Vector3 fleeDirection = (transform.position - threat.position).normalized;
        Vector3 fleeTarget = transform.position + fleeDirection * fleeRange;
        
        agent.SetDestination(fleeTarget);
    }
    
    private void MoveToTarget(Transform target)
    {
        if (agent == null || !agent.isOnNavMesh || target == null) return;
        
        agent.SetDestination(target.position);
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
}