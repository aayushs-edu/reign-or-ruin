using UnityEngine;

// TEMPORARY TEST SCRIPT - Replace ShovelDamageDealer with this to debug
[RequireComponent(typeof(Collider2D))]
public class ShovelDamageTest : MonoBehaviour
{
    [Header("Test Settings")]
    public int damage = 10;
    public bool alwaysDealDamage = true;
    public bool logEverything = true;
    
    private void Start()
    {
        // Ensure collider is trigger
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = true;
            Debug.Log($"[SHOVEL TEST] Collider found and set to trigger. Type: {col.GetType().Name}");
            Debug.Log($"[SHOVEL TEST] Collider enabled: {col.enabled}");
            Debug.Log($"[SHOVEL TEST] GameObject active: {gameObject.activeInHierarchy}");
        }
        else
        {
            Debug.LogError("[SHOVEL TEST] NO COLLIDER FOUND!");
        }
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log($"[SHOVEL HIT] Triggered by: {other.name} (Tag: {other.tag}, Layer: {LayerMask.LayerToName(other.gameObject.layer)})");
        
        // Try to damage anything with health
        bool damaged = TryDamageAnything(other.gameObject);
        
        if (damaged)
        {
            Debug.Log($"[SHOVEL HIT] ✓ Successfully damaged {other.name} for {damage} damage!");
        }
        else
        {
            Debug.LogWarning($"[SHOVEL HIT] ✗ Could not damage {other.name} - no health component found");
            
            // List all components on the target
            if (logEverything)
            {
                Component[] components = other.GetComponents<Component>();
                Debug.Log($"[SHOVEL HIT] Components on {other.name}:");
                foreach (var comp in components)
                {
                    Debug.Log($"  - {comp.GetType().Name}");
                }
            }
        }
    }
    
    private void OnTriggerStay2D(Collider2D other)
    {
        if (logEverything)
        {
            Debug.Log($"[SHOVEL STAY] Still overlapping: {other.name}");
        }
    }
    
    private void OnCollisionEnter2D(Collision2D collision)
    {
        Debug.LogError($"[SHOVEL COLLISION] This shouldn't happen! Collided with {collision.gameObject.name}. Check if collider is set to trigger!");
    }
    
    private bool TryDamageAnything(GameObject target)
    {
        // Try every possible health component type
        
        // Generic Health
        Health health = target.GetComponent<Health>();
        if (health != null)
        {
            health.TakeDamage(damage);
            return true;
        }
        
        // EnemyHealth
        EnemyHealth enemyHealth = target.GetComponent<EnemyHealth>();
        if (enemyHealth != null)
        {
            enemyHealth.TakeDamage(damage);
            return true;
        }
        
        // PlayerHealth
        PlayerHealth playerHealth = target.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(damage);
            return true;
        }
        
        // VillagerHealth
        VillagerHealth villagerHealth = target.GetComponent<VillagerHealth>();
        if (villagerHealth != null)
        {
            villagerHealth.TakeDamage(damage);
            return true;
        }
        
        // BuildingHealth
        BuildingHealth buildingHealth = target.GetComponent<BuildingHealth>();
        if (buildingHealth != null)
        {
            buildingHealth.TakeDamage(damage);
            return true;
        }
        
        return false;
    }
    
    private void OnDrawGizmos()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col == null) return;
        
        // Draw collider bounds in world space
        Gizmos.color = Color.red;
        
        if (col is BoxCollider2D box)
        {
            Vector3 center = transform.TransformPoint(box.offset);
            Vector3 size = box.size;
            size.x *= transform.lossyScale.x;
            size.y *= transform.lossyScale.y;
            
            Gizmos.matrix = Matrix4x4.TRS(center, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, size);
            Gizmos.matrix = Matrix4x4.identity;
        }
        else if (col is CircleCollider2D circle)
        {
            Vector3 center = transform.TransformPoint(circle.offset);
            float radius = circle.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);
            Gizmos.DrawWireSphere(center, radius);
        }
    }
}