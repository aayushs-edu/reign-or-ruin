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
    
    [Header("Debug Settings")]
    [SerializeField] private bool debugMode = true; // Enable by default for thorough debugging
    [SerializeField] private bool logAllTriggers = true; // Log every trigger event
    [SerializeField] private bool logTargetValidation = true; // Log target validation steps
    [SerializeField] private bool logDamageAttempts = true; // Log damage dealing attempts
    [SerializeField] private bool logComponentsFound = true; // Log what components are found
    [SerializeField] private bool visualizeHits = true; // Draw debug rays on hits
    
    // References
    private CommonerCombat combatSystem;
    private Villager villager;
    private Collider2D damageCollider;
    
    // State tracking
    private bool canDealDamage = false;
    private HashSet<GameObject> damagedThisSwing = new HashSet<GameObject>();
    private int totalTriggersThisSwing = 0;
    private int validTargetsThisSwing = 0;
    private int damageDealtThisSwing = 0;
    
    // Debug tracking
    private float lastEnableTime = 0f;
    private string lastAttackerInfo = "";
    
    private void Awake()
    {
        damageCollider = GetComponent<Collider2D>();
        if (damageCollider != null)
        {
            damageCollider.isTrigger = true;
            LogDebug($"AWAKE: Collider found and set to trigger. Type: {damageCollider.GetType().Name}", LogLevel.System);
        }
        else
        {
            LogDebug($"AWAKE: ERROR - No Collider2D found!", LogLevel.Error);
        }
        
        LogDebug($"AWAKE: ShovelDamageDealer initialized on {gameObject.name}", LogLevel.System);
    }
    
    private void Start()
    {
        LogDebug($"START: GameObject active: {gameObject.activeInHierarchy}, Component enabled: {enabled}", LogLevel.System);
        LogDebug($"START: Damage layers mask: {damageLayers.value} ({GetLayerNames(damageLayers)})", LogLevel.System);
    }
    
    public void Initialize(CommonerCombat combat, Villager villagerComponent)
    {
        combatSystem = combat;
        villager = villagerComponent;
        
        if (villager != null)
        {
            lastAttackerInfo = $"{villager.name} ({villager.GetRole()}, {villager.GetState()})";
        }
        
        LogDebug($"INITIALIZE: Combat system set to {(combat != null ? combat.name : "NULL")}", LogLevel.System);
        LogDebug($"INITIALIZE: Villager set to {lastAttackerInfo}", LogLevel.System);
    }
    
    public void SetDamage(int newDamage)
    {
        int oldDamage = damage;
        damage = newDamage;
        LogDebug($"SET_DAMAGE: Changed from {oldDamage} to {damage}", LogLevel.Important);
    }
    
    public void EnableDamageDealing()
    {
        canDealDamage = true;
        damagedThisSwing.Clear();
        totalTriggersThisSwing = 0;
        validTargetsThisSwing = 0;
        damageDealtThisSwing = 0;
        lastEnableTime = Time.time;
        
        LogDebug($"ENABLE_DAMAGE: Damage dealing ENABLED for {lastAttackerInfo} with {damage} damage", LogLevel.Important);
        LogDebug($"ENABLE_DAMAGE: Cleared damaged list, reset counters", LogLevel.System);
    }

    public void DisableDamageDealing()
    {
        bool wasEnabled = canDealDamage;
        canDealDamage = false;
        
        if (wasEnabled)
        {
            float activeDuration = Time.time - lastEnableTime;
            LogDebug($"DISABLE_DAMAGE: Damage dealing DISABLED for {lastAttackerInfo}", LogLevel.Important);
            LogDebug($"SWING_SUMMARY: Duration: {activeDuration:F2}s, Triggers: {totalTriggersThisSwing}, Valid targets: {validTargetsThisSwing}, Damage dealt: {damageDealtThisSwing}", LogLevel.Important);
            
            if (totalTriggersThisSwing == 0)
            {
                LogDebug($"WARNING: No triggers detected during swing! Check collider setup.", LogLevel.Warning);
            }
            
            if (validTargetsThisSwing == 0 && totalTriggersThisSwing > 0)
            {
                LogDebug($"WARNING: Triggers detected but no valid targets! Check targeting logic.", LogLevel.Warning);
            }
            
            if (validTargetsThisSwing > 0 && damageDealtThisSwing == 0)
            {
                LogDebug($"ERROR: Valid targets found but no damage dealt! Check damage dealing logic.", LogLevel.Error);
            }
        }
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        totalTriggersThisSwing++;
        
        if (logAllTriggers)
        {
            LogDebug($"TRIGGER_{totalTriggersThisSwing}: Hit {other.name} (Tag: {other.tag}, Layer: {LayerMask.LayerToName(other.gameObject.layer)})", LogLevel.Trigger);
            LogDebug($"TRIGGER_{totalTriggersThisSwing}: Can deal damage: {canDealDamage}, Already damaged: {damagedThisSwing.Contains(other.gameObject)}", LogLevel.Trigger);
        }
        
        if (!canDealDamage) 
        {
            LogDebug($"TRIGGER_{totalTriggersThisSwing}: SKIPPED - Damage dealing disabled", LogLevel.Trigger);
            return;
        }
        
        // Skip if we've already damaged this target this swing
        if (onlyDamageOnce && damagedThisSwing.Contains(other.gameObject)) 
        {
            LogDebug($"TRIGGER_{totalTriggersThisSwing}: SKIPPED - Already damaged {other.name} this swing", LogLevel.Trigger);
            return;
        }
        
        // Check layer
        if (damageLayers != -1 && !IsInLayerMask(other.gameObject.layer, damageLayers)) 
        {
            LogDebug($"TRIGGER_{totalTriggersThisSwing}: SKIPPED - Layer {LayerMask.LayerToName(other.gameObject.layer)} not in damage layers ({GetLayerNames(damageLayers)})", LogLevel.Trigger);
            return;
        }
        
        // Check if valid target
        bool isValid = IsValidTarget(other.gameObject);
        if (!isValid)
        {
            LogDebug($"TRIGGER_{totalTriggersThisSwing}: SKIPPED - {other.name} is not a valid target", LogLevel.Trigger);
            return;
        }
        
        validTargetsThisSwing++;
        LogDebug($"TRIGGER_{totalTriggersThisSwing}: VALID TARGET - Attempting to damage {other.name}", LogLevel.Important);
        
        // Try to deal damage
        bool damageSuccess = TryDealDamage(other.gameObject);
        if (damageSuccess)
        {
            damageDealtThisSwing++;
            damagedThisSwing.Add(other.gameObject);
            
            // Spawn hit effect
            SpawnHitEffect(other);
            
            // Notify combat system
            if (combatSystem != null)
            {
                combatSystem.OnShovelHitEnemy(other.gameObject, damage);
                LogDebug($"TRIGGER_{totalTriggersThisSwing}: Notified combat system of hit", LogLevel.System);
            }
            
            // Visual debug
            if (visualizeHits)
            {
                Debug.DrawLine(transform.position, other.transform.position, Color.red, 2f);
            }
            
            LogDebug($"TRIGGER_{totalTriggersThisSwing}: SUCCESS - Damaged {other.name} for {damage} damage!", LogLevel.Success);
        }
        else
        {
            LogDebug($"TRIGGER_{totalTriggersThisSwing}: FAILED - Could not damage {other.name}", LogLevel.Error);
        }
    }
    
    private bool IsValidTarget(GameObject target)
    {
        if (logTargetValidation)
        {
            LogDebug($"TARGET_VALIDATION: Checking if {target.name} is valid target", LogLevel.Validation);
        }
        
        // Don't hit self
        if (target.transform.IsChildOf(transform.root)) 
        {
            if (logTargetValidation) LogDebug($"TARGET_VALIDATION: INVALID - {target.name} is child of attacker", LogLevel.Validation);
            return false;
        }
        
        if (villager == null) 
        {
            if (logTargetValidation) LogDebug($"TARGET_VALIDATION: INVALID - No villager component on attacker", LogLevel.Validation);
            return false;
        }
        
        string attackerState = villager.GetState().ToString();
        if (logTargetValidation)
        {
            LogDebug($"TARGET_VALIDATION: Attacker {villager.name} is {attackerState}", LogLevel.Validation);
        }
        
        // Enhanced target validation based on villager faction status
        if (villager.GetState() == VillagerState.Rebel)
        {
            // Rebels attack player and loyal villagers
            if (target.CompareTag("Player")) 
            {
                if (logTargetValidation) LogDebug($"TARGET_VALIDATION: VALID - Rebel can attack player {target.name}", LogLevel.Validation);
                return true;
            }
            
            // Check for loyal villagers
            if (target.CompareTag("Villager"))
            {
                Villager targetVillager = target.GetComponent<Villager>();
                if (targetVillager != null)
                {
                    bool targetIsLoyal = targetVillager.IsLoyal();
                    if (logTargetValidation) 
                    {
                        LogDebug($"TARGET_VALIDATION: Target villager {target.name} state: {targetVillager.GetState()}, IsLoyal: {targetIsLoyal}", LogLevel.Validation);
                    }
                    
                    if (targetIsLoyal)
                    {
                        if (logTargetValidation) LogDebug($"TARGET_VALIDATION: VALID - Rebel can attack loyal villager {target.name}", LogLevel.Validation);
                        return true;
                    }
                }
                else
                {
                    if (logTargetValidation) LogDebug($"TARGET_VALIDATION: Target {target.name} has Villager tag but no Villager component", LogLevel.Validation);
                }
            }
            
            // Check if target is another rebel
            Villager targetVillagerComponent = target.GetComponent<Villager>();
            if (targetVillagerComponent != null && targetVillagerComponent.IsRebel())
            {
                if (logTargetValidation) LogDebug($"TARGET_VALIDATION: INVALID - Rebel will not attack fellow rebel {target.name}", LogLevel.Validation);
                return false;
            }
        }
        else
        {
            // Loyal/Angry villagers attack enemies and rebels
            
            // Attack anything tagged as "Enemy"
            if (target.CompareTag("Enemy"))
            {
                if (logTargetValidation) LogDebug($"TARGET_VALIDATION: VALID - Loyal can attack enemy {target.name}", LogLevel.Validation);
                return true;
            }
            
            // Attack rebel villagers
            Villager targetVillager = target.GetComponent<Villager>();
            if (targetVillager != null)
            {
                bool targetIsRebel = targetVillager.IsRebel();
                if (logTargetValidation)
                {
                    LogDebug($"TARGET_VALIDATION: Target villager {target.name} state: {targetVillager.GetState()}, IsRebel: {targetIsRebel}", LogLevel.Validation);
                }
                
                if (targetIsRebel)
                {
                    if (logTargetValidation) LogDebug($"TARGET_VALIDATION: VALID - Loyal can attack rebel villager {target.name}", LogLevel.Validation);
                    return true;
                }
            }
        }
        
        if (logTargetValidation) LogDebug($"TARGET_VALIDATION: INVALID - {target.name} is not a valid target for {attackerState} {villager.name}", LogLevel.Validation);
        return false;
    }
    
    private bool TryDealDamage(GameObject target)
    {
        if (logDamageAttempts)
        {
            LogDebug($"DAMAGE_ATTEMPT: Trying to damage {target.name} with {damage} damage", LogLevel.Damage);
        }
        
        // Log all health components on target
        if (logComponentsFound)
        {
            LogComponentsOnTarget(target);
        }
        
        // IMPORTANT: Try VillagerHealth FIRST since it inherits from Health
        VillagerHealth villagerHealth = target.GetComponent<VillagerHealth>();
        if (villagerHealth != null)
        {
            if (logDamageAttempts)
            {
                LogDebug($"DAMAGE_ATTEMPT: Found VillagerHealth, calling TakeDamage({damage}, {villager.gameObject.name})", LogLevel.Damage);
            }
            
            villagerHealth.TakeDamage(damage, villager.gameObject);
            
            if (logDamageAttempts)
            {
                LogDebug($"DAMAGE_SUCCESS: Dealt {damage} damage to villager {target.name} via VillagerHealth", LogLevel.Success);
            }
            return true;
        }
        
        // Try PlayerHealth
        PlayerHealth playerHealth = target.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            if (logDamageAttempts)
            {
                LogDebug($"DAMAGE_ATTEMPT: Found PlayerHealth, calling TakeDamage({damage})", LogLevel.Damage);
            }
            
            playerHealth.TakeDamage(damage);
            
            // Friendly fire check
            if (villager != null && villager.IsLoyal())
            {
                villager.OnHitByPlayerFriendlyFire();
                LogDebug($"DAMAGE_SIDE_EFFECT: Triggered friendly fire penalty for loyal villager attacking player", LogLevel.System);
            }
            
            if (logDamageAttempts)
            {
                LogDebug($"DAMAGE_SUCCESS: Dealt {damage} damage to player {target.name} via PlayerHealth", LogLevel.Success);
            }
            return true;
        }
        
        // Try EnemyHealth
        EnemyHealth enemyHealth = target.GetComponent<EnemyHealth>();
        if (enemyHealth != null)
        {
            if (logDamageAttempts)
            {
                LogDebug($"DAMAGE_ATTEMPT: Found EnemyHealth, calling TakeDamage({damage})", LogLevel.Damage);
            }
            
            enemyHealth.TakeDamage(damage);
            
            if (logDamageAttempts)
            {
                LogDebug($"DAMAGE_SUCCESS: Dealt {damage} damage to enemy {target.name} via EnemyHealth", LogLevel.Success);
            }
            return true;
        }
        
        // Try generic Health component last
        Health health = target.GetComponent<Health>();
        if (health != null)
        {
            if (logDamageAttempts)
            {
                LogDebug($"DAMAGE_ATTEMPT: Found generic Health, calling TakeDamage({damage})", LogLevel.Damage);
            }
            
            health.TakeDamage(damage);
            
            if (logDamageAttempts)
            {
                LogDebug($"DAMAGE_SUCCESS: Dealt {damage} damage to {target.name} via generic Health", LogLevel.Success);
            }
            return true;
        }
        
        // No valid health component found
        if (logDamageAttempts)
        {
            LogDebug($"DAMAGE_FAILED: No valid health component found on {target.name}!", LogLevel.Error);
        }
        
        return false;
    }
    
    private void LogComponentsOnTarget(GameObject target)
    {
        Component[] allComponents = target.GetComponents<Component>();
        System.Text.StringBuilder componentList = new System.Text.StringBuilder();
        
        foreach (Component comp in allComponents)
        {
            if (comp != null)
            {
                componentList.Append(comp.GetType().Name).Append(", ");
            }
        }
        
        LogDebug($"COMPONENTS_ON_TARGET: {target.name} has components: {componentList.ToString().TrimEnd(',', ' ')}", LogLevel.System);
        
        // Specifically check for health components
        Health[] healthComponents = target.GetComponents<Health>();
        if (healthComponents.Length > 0)
        {
            LogDebug($"HEALTH_COMPONENTS: Found {healthComponents.Length} health components on {target.name}:", LogLevel.System);
            foreach (Health h in healthComponents)
            {
                LogDebug($"  - {h.GetType().Name} (Current HP: {h.GetCurrentHealth()}/{h.GetMaxHealth()})", LogLevel.System);
            }
        }
        else
        {
            LogDebug($"HEALTH_COMPONENTS: No health components found on {target.name}", LogLevel.Warning);
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
        
        LogDebug($"HIT_EFFECT: Spawned {hitEffectPrefab.name} at {spawnPosition} for hit on {hitTarget.name}", LogLevel.System);
        
        // Parent to target if desired
        if (parentEffectToTarget)
        {
            effect.transform.SetParent(hitTarget.transform);
            effect.transform.localPosition = hitEffectOffset;
        }
        
        // Handle cleanup
        Animator effectAnimator = effect.GetComponent<Animator>();
        if (effectAnimator != null)
        {
            StartCoroutine(DestroyEffectAfterAnimation(effect, effectAnimator));
        }
        else
        {
            Destroy(effect, effectAutoDestroyTime);
        }
    }
    
    private IEnumerator DestroyEffectAfterAnimation(GameObject effect, Animator animator)
    {
        yield return null;
        
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        float animationLength = stateInfo.length;
        
        if (animationLength <= 0)
        {
            AnimatorClipInfo[] clipInfo = animator.GetCurrentAnimatorClipInfo(0);
            if (clipInfo.Length > 0)
            {
                animationLength = clipInfo[0].clip.length;
            }
        }
        
        if (animationLength <= 0)
        {
            animationLength = effectAutoDestroyTime;
        }
        
        yield return new WaitForSeconds(animationLength);
        
        if (effect != null)
        {
            Destroy(effect);
        }
    }
    
    private bool IsInLayerMask(int layer, LayerMask layerMask)
    {
        return (layerMask.value & (1 << layer)) != 0;
    }
    
    private string GetLayerNames(LayerMask layerMask)
    {
        System.Text.StringBuilder layerNames = new System.Text.StringBuilder();
        for (int i = 0; i < 32; i++)
        {
            if ((layerMask.value & (1 << i)) != 0)
            {
                string layerName = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(layerName))
                {
                    layerNames.Append(layerName).Append(", ");
                }
            }
        }
        return layerNames.ToString().TrimEnd(',', ' ');
    }
    
    // Enhanced logging system with different log levels
    private enum LogLevel
    {
        System,     // Blue - System operations
        Important,  // Green - Important events
        Success,    // Green - Successful operations
        Warning,    // Yellow - Warnings
        Error,      // Red - Errors
        Trigger,    // Cyan - Trigger events
        Validation, // Magenta - Target validation
        Damage      // Orange - Damage attempts
    }
    
    private void LogDebug(string message, LogLevel level)
    {
        if (!debugMode) return;
        
        // Filter logs based on settings
        if (level == LogLevel.Trigger && !logAllTriggers) return;
        if (level == LogLevel.Validation && !logTargetValidation) return;
        if (level == LogLevel.Damage && !logDamageAttempts) return;
        if (level == LogLevel.System && !logComponentsFound) return;
        
        string prefix = $"[SHOVEL-{gameObject.name}]";
        string colorCode = GetColorCode(level);
        string formattedMessage = $"{colorCode}{prefix} {message}</color>";
        
        switch (level)
        {
            case LogLevel.Error:
                Debug.LogError(formattedMessage);
                break;
            case LogLevel.Warning:
                Debug.LogWarning(formattedMessage);
                break;
            default:
                Debug.Log(formattedMessage);
                break;
        }
    }
    
    private string GetColorCode(LogLevel level)
    {
        switch (level)
        {
            case LogLevel.System: return "<color=lightblue>";
            case LogLevel.Important: return "<color=lime>";
            case LogLevel.Success: return "<color=green>";
            case LogLevel.Warning: return "<color=yellow>";
            case LogLevel.Error: return "<color=red>";
            case LogLevel.Trigger: return "<color=cyan>";
            case LogLevel.Validation: return "<color=magenta>";
            case LogLevel.Damage: return "<color=orange>";
            default: return "<color=white>";
        }
    }
    
    // Animation event support
    public void StartDamageWindow()
    {
        EnableDamageDealing();
    }
    
    public void EndDamageWindow()
    {
        DisableDamageDealing();
    }
    
    // Debug context menu methods
    [ContextMenu("Log Current State")]
    public void LogCurrentState()
    {
        LogDebug($"=== CURRENT STATE ===", LogLevel.Important);
        LogDebug($"Can Deal Damage: {canDealDamage}", LogLevel.System);
        LogDebug($"Damage Value: {damage}", LogLevel.System);
        LogDebug($"Villager: {(villager != null ? $"{villager.name} ({villager.GetState()})" : "NULL")}", LogLevel.System);
        LogDebug($"Combat System: {(combatSystem != null ? combatSystem.name : "NULL")}", LogLevel.System);
        LogDebug($"Collider: {(damageCollider != null ? $"{damageCollider.GetType().Name} (Enabled: {damageCollider.enabled}, Trigger: {damageCollider.isTrigger})" : "NULL")}", LogLevel.System);
        LogDebug($"Damaged This Swing: {damagedThisSwing.Count}", LogLevel.System);
        LogDebug($"=== END STATE ===", LogLevel.Important);
    }
    
    [ContextMenu("Test Damage Value")]
    public void TestDamageValue()
    {
        SetDamage(damage + 1);
        SetDamage(damage - 1);
    }
    
    private void OnDrawGizmos()
    {
        if (!debugMode) return;
        
        // Show damage state with color
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
                if (canDealDamage) Gizmos.DrawCube(box.offset, box.size * 0.1f);
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
                if (canDealDamage) Gizmos.DrawWireSphere((Vector3)circle.offset, circle.radius * 0.1f);
            }
            
            Gizmos.matrix = oldMatrix;
        }
        
        // Draw damage value as text
        if (Application.isPlaying)
        {
            Vector3 textPos = transform.position + Vector3.up * 0.5f;
            Gizmos.color = canDealDamage ? Color.red : Color.gray;
            Gizmos.DrawWireSphere(textPos, 0.1f);
        }
        
        // Draw lines to damaged objects this swing
        Gizmos.color = Color.yellow;
        foreach (GameObject damaged in damagedThisSwing)
        {
            if (damaged != null)
            {
                Gizmos.DrawLine(transform.position, damaged.transform.position);
            }
        }
    }
}