using UnityEngine;
using System.Collections.Generic;
using System.Collections;

[RequireComponent(typeof(Collider2D))]
public class ShovelDamageDealer : MonoBehaviour
{
    [Header("Damage Settings")]
    [SerializeField] private int damage = 8;
    [SerializeField] private bool onlyDamageOnce = true; // Only damage each enemy once per swing
    [SerializeField] private LayerMask damageLayers = -1;
    
    [Header("Hit Effect")]
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private Vector3 hitEffectOffset = Vector3.zero;
    [SerializeField] private bool parentEffectToTarget = false;
    [SerializeField] private float effectAutoDestroyTime = 2f; // Fallback if can't get animation length
    
    [Header("Debug")]
    [SerializeField] private bool debugMode = false;
    
    // References
    private CommonerCombat combatSystem;
    private Villager villager;
    private Collider2D damageCollider;
    
    // State
    private bool canDealDamage = false;
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
            Debug.LogError($"ShovelDamageDealer: No Collider2D found on {gameObject.name}!");
        }
    }
    
    public void Initialize(CommonerCombat combat, Villager villagerComponent)
    {
        combatSystem = combat;
        villager = villagerComponent;
    }
    
    public void SetDamage(int newDamage)
    {
        damage = newDamage;
    }
    
    public void EnableDamageDealing()
    {
        canDealDamage = true;
        damagedThisSwing.Clear();
        
        if (debugMode)
        {
            Debug.Log($"ShovelDamageDealer: Damage dealing enabled for {transform.root.name}");
        }
    }

    public void DisableDamageDealing()
    {
        canDealDamage = false;

        if (debugMode)
        {
            Debug.Log($"ShovelDamageDealer: Damage dealing disabled for {transform.root.name}");
        }
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log($"ShovelDamageDealer: Triggered with {other.gameObject.name}");
        if (!canDealDamage) return;
        
        // Skip if we've already damaged this target this swing
        if (onlyDamageOnce && damagedThisSwing.Contains(other.gameObject)) return;
        
        // Check layer
        if (damageLayers != -1 && !IsInLayerMask(other.gameObject.layer, damageLayers)) return;
        
        // Determine if this is a valid target based on villager state
        if (!IsValidTarget(other.gameObject)) return;
        
        // Try to deal damage
        if (TryDealDamage(other.gameObject))
        {
            damagedThisSwing.Add(other.gameObject);
            
            // Spawn hit effect at impact point
            SpawnHitEffect(other);
            
            // Notify combat system
            if (combatSystem != null)
            {
                combatSystem.OnShovelHitEnemy(other.gameObject, damage);
            }
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
        
        // Parent to target if desired (useful for effects that should follow the target)
        if (parentEffectToTarget)
        {
            effect.transform.SetParent(hitTarget.transform);
            effect.transform.localPosition = hitEffectOffset;
        }
        
        // Get animator and calculate destroy time
        Animator effectAnimator = effect.GetComponent<Animator>();
        if (effectAnimator != null)
        {
            StartCoroutine(DestroyEffectAfterAnimation(effect, effectAnimator));
        }
        else
        {
            // No animator, use fallback destroy time
            Destroy(effect, effectAutoDestroyTime);
            
            if (debugMode)
            {
                Debug.LogWarning($"Hit effect has no Animator, using fallback destroy time of {effectAutoDestroyTime}s");
            }
        }
        
        if (debugMode)
        {
            Debug.Log($"Spawned hit effect at {spawnPosition} for hit on {hitTarget.name}");
        }
    }
    
    private IEnumerator DestroyEffectAfterAnimation(GameObject effect, Animator animator)
    {
        // Wait a frame for animator to start
        yield return null;
        
        // Get current animation info
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        float animationLength = stateInfo.length;
        
        // If we can't get animation length, try from clip
        if (animationLength <= 0)
        {
            AnimatorClipInfo[] clipInfo = animator.GetCurrentAnimatorClipInfo(0);
            if (clipInfo.Length > 0)
            {
                animationLength = clipInfo[0].clip.length;
            }
        }
        
        // Use fallback if still no length
        if (animationLength <= 0)
        {
            animationLength = effectAutoDestroyTime;
            
            if (debugMode)
            {
                Debug.LogWarning($"Could not get animation length for hit effect, using fallback: {effectAutoDestroyTime}s");
            }
        }
        
        // Wait for animation to complete
        yield return new WaitForSeconds(animationLength);
        
        // Destroy the effect
        if (effect != null)
        {
            Destroy(effect);
        }
    }
    
    private bool IsInLayerMask(int layer, LayerMask layerMask)
    {
        return (layerMask.value & (1 << layer)) != 0;
    }
    
    private bool IsValidTarget(GameObject target)
    {
        // Don't hit self
        if (target.transform.IsChildOf(transform.root)) return false;
        
        if (villager == null) return false;
        
        if (villager.GetState() == VillagerState.Rebel)
        {
            // Rebels attack player and loyal villagers
            if (target.CompareTag("Player")) return true;
            
            if (target.CompareTag("Villager"))
            {
                Villager targetVillager = target.GetComponent<Villager>();
                return targetVillager != null && targetVillager.IsLoyal();
            }
        }
        else
        {
            // Loyal villagers attack enemies
            return target.CompareTag("Enemy");
        }
        
        return false;
    }
    
    private bool TryDealDamage(GameObject target)
    {
        bool damageDealt = false;
        
        // Try generic Health component first
        Health health = target.GetComponent<Health>();
        if (health != null)
        {
            health.TakeDamage(damage);
            damageDealt = true;
        }
        
        // Try specific health components
        if (!damageDealt)
        {
            EnemyHealth enemyHealth = target.GetComponent<EnemyHealth>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(damage);
                damageDealt = true;
            }
        }
        
        if (!damageDealt)
        {
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
            }
        }
        
        if (!damageDealt)
        {
            VillagerHealth villagerHealth = target.GetComponent<VillagerHealth>();
            if (villagerHealth != null)
            {
                villagerHealth.TakeDamage(damage);
                damageDealt = true;
            }
        }
        
        if (debugMode && damageDealt)
        {
            Debug.Log($"ShovelDamageDealer: Dealt {damage} damage to {target.name}");
        }
        
        return damageDealt;
    }
    
    // Animation event support (if you want to call these from animation events)
    public void StartDamageWindow()
    {
        EnableDamageDealing();
    }
    
    public void EndDamageWindow()
    {
        DisableDamageDealing();
    }
    
    private void OnDrawGizmos()
    {
        if (!debugMode) return;
        
        // Show damage state
        if (canDealDamage)
        {
            Gizmos.color = Color.red;
        }
        else
        {
            Gizmos.color = Color.gray;
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