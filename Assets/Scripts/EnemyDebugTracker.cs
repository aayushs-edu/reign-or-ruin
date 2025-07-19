using UnityEngine;
using UnityEngine.AI;

public class EnemyDebugTracker : MonoBehaviour
{
    [Header("Debug Settings")]
    [SerializeField] private bool logPositionChanges = true;
    [SerializeField] private bool logComponentStates = true;
    [SerializeField] private float logInterval = 1f;
    
    private Vector3 lastPosition;
    private float lastLogTime;
    private SpriteRenderer spriteRenderer;
    private NavMeshAgent agent;
    private bool wasVisible = true;
    
    private void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        agent = GetComponent<NavMeshAgent>();
        lastPosition = transform.position;
        
        Debug.Log($"[ENEMY DEBUG] {gameObject.name} spawned at {transform.position}");
        LogComponentStates();
    }
    
    private void Update()
    {
        CheckVisibility();
        CheckPosition();
        
        if (Time.time - lastLogTime > logInterval)
        {
            if (logComponentStates)
            {
                LogComponentStates();
            }
            lastLogTime = Time.time;
        }
    }
    
    private void CheckVisibility()
    {
        bool isVisible = gameObject.activeInHierarchy && 
                        spriteRenderer != null && 
                        spriteRenderer.enabled && 
                        spriteRenderer.color.a > 0f;
        
        if (wasVisible && !isVisible)
        {
            Debug.LogError($"[ENEMY DEBUG] {gameObject.name} became INVISIBLE! " +
                          $"Active: {gameObject.activeInHierarchy}, " +
                          $"SpriteEnabled: {(spriteRenderer != null ? spriteRenderer.enabled.ToString() : "NULL")}, " +
                          $"Alpha: {(spriteRenderer != null ? spriteRenderer.color.a.ToString() : "NULL")}");
        }
        
        wasVisible = isVisible;
    }
    
    private void CheckPosition()
    {
        if (logPositionChanges && Vector3.Distance(transform.position, lastPosition) > 0.1f)
        {
            Debug.Log($"[ENEMY DEBUG] {gameObject.name} moved from {lastPosition} to {transform.position}");
            lastPosition = transform.position;
            
            // Check if Z position changed (bad for 2D)
            if (Mathf.Abs(transform.position.z) > 0.1f)
            {
                Debug.LogWarning($"[ENEMY DEBUG] {gameObject.name} Z position is {transform.position.z} - should be 0 for 2D!");
            }
        }
    }
    
    private void LogComponentStates()
    {
        string log = $"[ENEMY DEBUG] {gameObject.name} state:\n";
        log += $"- Position: {transform.position}\n";
        log += $"- Active: {gameObject.activeInHierarchy}\n";
        
        if (spriteRenderer != null)
        {
            log += $"- Sprite: {(spriteRenderer.sprite != null ? spriteRenderer.sprite.name : "NULL")}\n";
            log += $"- SpriteRenderer Enabled: {spriteRenderer.enabled}\n";
            log += $"- Color: {spriteRenderer.color}\n";
            log += $"- Sorting Layer: {spriteRenderer.sortingLayerName}\n";
            log += $"- Sorting Order: {spriteRenderer.sortingOrder}\n";
        }
        else
        {
            log += "- SpriteRenderer: NULL\n";
        }
        
        if (agent != null)
        {
            log += $"- NavMeshAgent Enabled: {agent.enabled}\n";
            log += $"- On NavMesh: {agent.isOnNavMesh}\n";
            log += $"- Has Path: {agent.hasPath}\n";
            log += $"- Velocity: {agent.velocity}\n";
        }
        else
        {
            log += "- NavMeshAgent: NULL\n";
        }
        
        Debug.Log(log);
    }
    
    private void OnDestroy()
    {
        Debug.Log($"[ENEMY DEBUG] {gameObject.name} destroyed at position {transform.position}");
    }
}