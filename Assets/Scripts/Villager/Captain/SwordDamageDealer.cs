using UnityEngine;
using System.Collections.Generic;
using System.Collections;

[RequireComponent(typeof(Collider2D))]
public class SwordDamageDealer : MonoBehaviour
{
    [Header("Damage Settings")]
    [SerializeField] private int damage = 12;
    [SerializeField] private bool onlyDamageOnce = true;
    [SerializeField] private LayerMask damageLayers = -1;
    
    [Header("Hit Effect")]
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private Vector3 hitEffectOffset = Vector3.zero;
    [SerializeField] private float effectAutoDestroyTime = 2f;
    
    [Header("Debug")]
    [SerializeField] private bool debugMode = false;
    [SerializeField] private bool debugDamageWindow = true; // Debug specifically for damage timing issues
    
    // References
    private CaptainCombat combatSystem;
    private Villager villager;
    private Collider2D damageCollider;
    
    // State
    private HashSet<GameObject> damagedThisSwing = new HashSet<GameObject>();
    
    private void Awake()
    {
        damageCollider = GetComponent<Collider2D>();
        if (damageCollider != null)
        {
            damageCollider.isTrigger = true;
        }
        else
        {
            Debug.LogError($"SwordDamageDealer: No Collider2D found on {gameObject.name}!");
        }
    }
    
    public void Initialize(CaptainCombat combat, Villager villagerComponent)
    {
        combatSystem = combat;
        villager = villagerComponent;
        
        if (debugMode)
        {
            Debug.Log($"SwordDamageDealer: Initialized for {villagerComponent.name} with combat system {combat.name}");
        }
    }
    
    public void SetDamage(int newDamage)
    {
        damage = newDamage;
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (debugMode)
        {
            Debug.Log($"SwordDamageDealer: {transform.root.name} triggered with {other.gameObject.name} (Tag: {other.tag})");
        }
        
        // CRITICAL FIX: Use the new damage window check instead of just IsAttacking()
        bool canDealDamage = false;
        
        if (combatSystem != null)
        {
            // Try the new damage window method first (for fixed CaptainCombat)
            if (combatSystem is CaptainCombat captainCombat)
            {
                canDealDamage = captainCombat.IsDamageWindowActive();
                
                if (debugDamageWindow)
                {
                    Debug.Log($"SwordDamageDealer: Captain {captainCombat.name} - IsAttacking: {captainCombat.IsAttacking()}, " +
                             $"IsDamageWindowActive: {canDealDamage}");
                }
            }
            else
            {
                // Fallback for other combat systems
                canDealDamage = combatSystem.IsAttacking();
                
                if (debugDamageWindow)
                {
                    Debug.Log($"SwordDamageDealer: Non-Captain combat system - IsAttacking: {canDealDamage}");
                }
            }
        }
        
        if (!canDealDamage)
        {
            if (debugDamageWindow)
            {
                Debug.Log($"SwordDamageDealer: Cannot deal damage - damage window not active for {transform.root.name}");
            }
            return;
        }
        
        // Reset damaged list at start of new attack window
        if (onlyDamageOnce)
        {
            // Check if this is a new attack by seeing if the list is empty
            // (it gets cleared when damage window opens)
            if (damagedThisSwing.Count == 0)
            {
                if (debugDamageWindow)
                {
                    Debug.Log($"SwordDamageDealer: New attack started for {transform.root.name} - cleared damage list");
                }
            }
            
            // Skip if we've already damaged this target this swing
            if (damagedThisSwing.Contains(other.gameObject))
            {
                if (debugDamageWindow)
                {
                    Debug.Log($"SwordDamageDealer: Already damaged {other.name} this swing - skipping");
                }
                return;
            }
        }
        
        // Check layer
        if (damageLayers != -1 && !IsInLayerMask(other.gameObject.layer, damageLayers))
        {
            if (debugMode)
            {
                Debug.Log($"SwordDamageDealer: {other.name} not in damage layers - skipping");
            }
            return;
        }
        
        // Determine if this is a valid target based on villager state
        if (!IsValidTarget(other.gameObject))
        {
            if (debugMode)
            {
                Debug.Log($"SwordDamageDealer: {other.name} is not a valid target for {villager.name} ({villager.GetState()}) - skipping");
            }
            return;
        }

        // Try to deal damage
        if (TryDealDamage(other.gameObject))
        {
            if (onlyDamageOnce)
            {
                damagedThisSwing.Add(other.gameObject);
            }

            // Spawn hit effect at impact point
            SpawnHitEffect(other);
            
            if (debugDamageWindow)
            {
                Debug.Log($"SwordDamageDealer: Successfully damaged {other.name} for {damage} damage!");
            }
        }
    }
    
    // Public method to clear the damage list (called by CaptainCombat at start of damage window)
    public void ResetDamageList()
    {
        damagedThisSwing.Clear();
        
        if (debugDamageWindow)
        {
            Debug.Log($"SwordDamageDealer: Damage list reset for {transform.root.name}");
        }
    }
    
    private void SpawnHitEffect(Collider2D hitTarget)
    {
        if (hitEffectPrefab == null) return;
        
        // Calculate spawn position
        Vector3 spawnPosition = hitTarget.transform.position + hitEffectOffset;
        
        // Use contact point if we can get it
        ContactPoint2D[] contacts = new ContactPoint2D[1];
        if (damageCollider.GetContacts(contacts) > 0)
        {
            spawnPosition = contacts[0].point + (Vector2)hitEffectOffset;
        }
        
        // Instantiate effect
        GameObject effect = Instantiate(hitEffectPrefab, spawnPosition, Quaternion.identity);
        Destroy(effect, effectAutoDestroyTime);
        
        if (debugMode)
        {
            Debug.Log($"Spawned hit effect at {spawnPosition} for hit on {hitTarget.name}");
        }
    }
    
    private bool IsInLayerMask(int layer, LayerMask layerMask)
    {
        return (layerMask.value & (1 << layer)) != 0;
    }
    
    private bool IsValidTarget(GameObject target)
    {
        // Don't hit self or same villager
        if (target.transform.IsChildOf(transform.root)) 
        {
            if (debugMode)
            {
                Debug.Log($"SwordDamageDealer: Skipping self-hit for {target.name}");
            }
            return false;
        }
        
        if (villager == null) 
        {
            if (debugMode)
            {
                Debug.LogError($"SwordDamageDealer: No villager reference set!");
            }
            return false;
        }
        
        // Get target villager component (if it exists)
        Villager targetVillager = target.GetComponent<Villager>();
        
        if (villager.IsRebel())
        {
            // Rebels attack: Player OR Loyal villagers
            if (target.CompareTag("Player")) 
            {
                if (debugMode) Debug.Log($"SwordDamageDealer: Rebel {villager.name} can attack player {target.name}");
                return true;
            }
            if (targetVillager != null && !targetVillager.IsRebel()) 
            {
                if (debugMode) Debug.Log($"SwordDamageDealer: Rebel {villager.name} can attack loyal villager {target.name}");
                return true;
            }
        }
        else
        {
            // Loyals attack: Enemies OR Rebel villagers  
            if (target.CompareTag("Enemy")) 
            {
                if (debugMode) Debug.Log($"SwordDamageDealer: Loyal {villager.name} can attack enemy {target.name}");
                return true;
            }
            if (targetVillager != null && targetVillager.IsRebel()) 
            {
                if (debugMode) Debug.Log($"SwordDamageDealer: Loyal {villager.name} can attack rebel villager {target.name}");
                return true;
            }
        }
        
        // if (debugMode)
        // {
        //     Debug.Log($"SwordDamageDealer: {villager.name} ({villager.GetState()}) cannot attack {target.name} " +
        //              $"(Villager: {targetVillager?.GetState() ?? "None"})");
        // }
        
        return false;
    }
    
    private bool TryDealDamage(GameObject target)
    {
        bool damageDealt = false;
        
        if (debugMode)
        {
            Debug.Log($"TryDealDamage: {villager.name} attempting to damage {target.name}");
            
            // Log all health components on target
            Health[] healthComponents = target.GetComponents<Health>();
            Debug.Log($"Target {target.name} has {healthComponents.Length} Health components:");
            foreach (var h in healthComponents)
            {
                Debug.Log($"  - {h.GetType().Name}");
            }
        }
        
        // IMPORTANT: Try VillagerHealth FIRST since it inherits from Health
        VillagerHealth villagerHealth = target.GetComponent<VillagerHealth>();
        if (villagerHealth != null)
        {
            villagerHealth.TakeDamage(damage, villager.gameObject);
            damageDealt = true;
            
            if (debugMode)
            {
                Debug.Log($"SUCCESS: {villager.name} ({villager.GetState()}) dealt {damage} damage to villager {target.name} via VillagerHealth");
            }
            return damageDealt;
        }
        
        // Try PlayerHealth next
        PlayerHealth playerHealth = target.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(damage);
            damageDealt = true;
            
            // Friendly fire check
            if (villager != null && villager.IsLoyal())
            {
                villager.OnHitByPlayerFriendlyFire();
            }
            
            if (debugMode)
            {
                Debug.Log($"SUCCESS: {villager.name} dealt {damage} damage to player {target.name} via PlayerHealth");
            }
            return damageDealt;
        }
        
        // Try EnemyHealth
        EnemyHealth enemyHealth = target.GetComponent<EnemyHealth>();
        if (enemyHealth != null)
        {
            enemyHealth.TakeDamage(damage);
            damageDealt = true;
            
            if (debugMode)
            {
                Debug.Log($"SUCCESS: {villager.name} dealt {damage} damage to enemy {target.name} via EnemyHealth");
            }
            return damageDealt;
        }
        
        // Try generic Health component last (fallback)
        Health health = target.GetComponent<Health>();
        if (health != null)
        {
            health.TakeDamage(damage);
            damageDealt = true;
            
            if (debugMode)
            {
                Debug.Log($"SUCCESS: {villager.name} dealt {damage} damage to {target.name} via generic Health");
            }
            return damageDealt;
        }
        
        // No valid health component found
        if (debugMode)
        {
            Debug.LogError($"FAILED: {villager.name} could not damage {target.name} - no valid health component found!");
        }
        
        return false;
    }
    
    private void OnDrawGizmos()
    {
        if (!debugMode && !debugDamageWindow) return;
        
        // Show damage state
        bool canDealDamage = false;
        if (Application.isPlaying && combatSystem != null)
        {
            if (combatSystem is CaptainCombat captainCombat)
            {
                canDealDamage = captainCombat.IsDamageWindowActive();
            }
            else
            {
                canDealDamage = combatSystem.IsAttacking();
            }
        }
        
        if (canDealDamage)
        {
            Gizmos.color = Color.red; // Can deal damage
        }
        else if (Application.isPlaying && combatSystem != null && combatSystem.IsAttacking())
        {
            Gizmos.color = Color.yellow; // Attacking but damage window closed
        }
        else
        {
            Gizmos.color = Color.gray; // Not attacking
        }
        
        // Draw collider bounds
        if (damageCollider != null)
        {
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
            
            if (damageCollider is BoxCollider2D box)
            {
                Gizmos.DrawWireCube(box.offset, box.size);
            }
            else if (damageCollider is CircleCollider2D circle)
            {
                // Draw circle approximation
                int segments = 16;
                Vector3 prevPoint = new Vector3(circle.radius, 0, 0) + (Vector3)circle.offset;
                for (int i = 1; i <= segments; i++)
                {
                    float angle = i * Mathf.PI * 2f / segments;
                    Vector3 point = new Vector3(Mathf.Cos(angle) * circle.radius, Mathf.Sin(angle) * circle.radius, 0) + (Vector3)circle.offset;
                    Gizmos.DrawLine(prevPoint, point);
                    prevPoint = point;
                }
            }
            
            Gizmos.matrix = oldMatrix;
        }
        
        // Draw hit effect spawn position preview
        if (hitEffectPrefab != null && Application.isPlaying && canDealDamage)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position + hitEffectOffset, 0.2f);
        }
    }
}