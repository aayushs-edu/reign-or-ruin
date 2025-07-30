using UnityEngine;
using System.Collections;

public class CaptainCombat : VillagerCombat
{
    [Header("Captain Specific")]
    [SerializeField] private Transform swordTransform;
    [SerializeField] private GameObject swordSprite;
    [SerializeField] private GameObject attackFXObject;
    [SerializeField] private float attackAnimationDuration = 0.4f;

    [Header("Sword Positioning")]
    [SerializeField] private float idleRotation = 0f; // Rotation when idle (0 = upright)
    [SerializeField] private float combatReadyDistance = 20f; // Distance at which sword starts tracking
    [SerializeField] private Vector3 swordSpriteOffset; // Offset of sprite from pivot point
    
    [Header("Debug")]
    [SerializeField] private bool debugCombat = false;
    
    // Cached components
    private Animator swordAnimator;
    private SwordDamageDealer swordDamageDealer;
    private CaptainInfluenceSystem influenceSystem;
    
    protected override void Awake()
    {
        base.Awake();
        
        // Get the influence system component
        influenceSystem = GetComponent<CaptainInfluenceSystem>();
        if (influenceSystem == null)
        {
            // Add it if it doesn't exist
            influenceSystem = gameObject.AddComponent<CaptainInfluenceSystem>();
            Debug.Log($"CaptainCombat: Added CaptainInfluenceSystem to {gameObject.name}");
        }
        
        // Auto-find sword hierarchy if not assigned
        if (swordTransform == null)
        {
            swordTransform = transform.Find("Sword");
            if (swordTransform == null)
            {
                foreach (Transform child in GetComponentsInChildren<Transform>())
                {
                    if (child.name.ToLower().Contains("sword") && child != transform)
                    {
                        swordTransform = child;
                        break;
                    }
                }
            }
        }
        
        // Find sword sprite and its components
        if (swordTransform != null)
        {
            // Find sword sprite
            if (swordSprite == null)
            {
                Transform sprite = swordTransform.Find("SwordSprite");
                if (sprite != null)
                {
                    swordSprite = sprite.gameObject;
                }
                else
                {
                    // Try to find any child with sprite renderer
                    SpriteRenderer sr = swordTransform.GetComponentInChildren<SpriteRenderer>();
                    if (sr != null) swordSprite = sr.gameObject;
                }
            }
            
            // Get sword animator
            if (swordSprite != null)
            {
                swordAnimator = swordSprite.GetComponent<Animator>();
                if (swordAnimator == null && debugCombat)
                {
                    Debug.LogWarning($"CaptainCombat: No Animator found on sword sprite for {gameObject.name}!");
                }
                
                // Set up damage dealer component on sword
                swordDamageDealer = swordSprite.GetComponent<SwordDamageDealer>();
                if (swordDamageDealer == null)
                {
                    if (debugCombat) Debug.LogWarning($"CaptainCombat: No SwordDamageDealer found, adding one to {swordSprite.name}");
                    swordDamageDealer = swordSprite.AddComponent<SwordDamageDealer>();
                }
                
                if (debugCombat) Debug.Log($"CaptainCombat: Found/Added SwordDamageDealer on {swordSprite.name}");
                
                // Apply sprite offset to position sword sprite within container
                swordSprite.transform.localPosition = swordSpriteOffset;
            }
            
            // Find attack FX
            if (attackFXObject == null)
            {
                Transform fx = swordTransform.Find("attackFX");
                if (fx == null)
                {
                    fx = swordTransform.Find("AttackFX");
                }
                if (fx != null)
                {
                    attackFXObject = fx.gameObject;
                }
            }
        }
        
        if (swordTransform == null)
        {
            Debug.LogError($"CaptainCombat: No sword transform found on {gameObject.name}!");
        }
    }
    
    protected override void Start()
    {
        base.Start();
        
        // Configure damage dealer
        if (swordDamageDealer != null)
        {
            swordDamageDealer.Initialize(this, villager);
            
            // IMPORTANT: Set initial damage value
            swordDamageDealer.SetDamage(currentDamage);
            
            if (debugCombat)
            {
                Debug.Log($"CaptainCombat: Initialized SwordDamageDealer for {gameObject.name} with {currentDamage} damage");
            }
        }
        else
        {
            Debug.LogError($"CaptainCombat: Could not set up SwordDamageDealer for {gameObject.name}!");
        }
        
        // Apply initial sword container position
        if (swordTransform != null)
        {
            swordTransform.rotation = Quaternion.Euler(0, 0, idleRotation);
        }
        
        // Ensure sprite is at correct offset
        if (swordSprite != null)
        {
            swordSprite.transform.localPosition = swordSpriteOffset;
        }
    }
    
    protected override void Update()
    {
        base.Update();

        // Update sword rotation based on state (matching CommonerCombat pattern)
        if (!isAttacking && swordTransform != null)
        {
            if (currentTarget != null)
            {
                float distanceToTarget = Vector3.Distance(transform.position, currentTarget.position);

                // Only rotate sword when target is within combat ready distance
                if (distanceToTarget <= combatReadyDistance)
                {
                    UpdateSwordRotation();
                }
                else
                {
                    // Target too far, return to idle
                    ReturnToIdleRotation();
                }
            }
            else
            {
                // No target, return to idle rotation
                ReturnToIdleRotation();
            }
        }
    }

    // FIXED: Combined facing and weapon rotation logic with proper sprite flipping
    private void UpdateSwordRotation()
    {
        if (currentTarget == null || swordTransform == null) return;

        Vector2 direction = (currentTarget.position - swordTransform.position).normalized;
        swordTransform.right = direction;

        Vector2 scale = swordTransform.localScale;
        if (direction.x < 0) 
        {
            scale.y = -1;
        }
        else
        {
            scale.y = 1;
        }
        swordTransform.localScale = scale;
    }
    
    private void ReturnToIdleRotation()
    {
        swordTransform.localScale = Vector3.one;
        Quaternion targetRotation = Quaternion.Euler(0, 0, idleRotation);
        swordTransform.rotation = Quaternion.Lerp(swordTransform.rotation, targetRotation, 5 * Time.deltaTime);
    }
    
    // CRITICAL FIX: Proper attack timing with damage window
    protected override IEnumerator PerformAttack()
    {
        if (debugCombat)
        {
            Debug.Log($"CaptainCombat: {gameObject.name} starting attack on {currentTarget?.name}");
        }

        isAttacking = true;
        lastAttackTime = Time.time;

        // Trigger sword attack animation FIRST
        if (swordAnimator != null)
        {
            swordAnimator.SetTrigger("Attack");
        }
        else if (debugCombat)
        {
            Debug.LogWarning($"CaptainCombat: No sword animator found on {gameObject.name}!");
        }

        // Wait for animation to complete
        yield return new WaitForSeconds(attackAnimationDuration);

        isAttacking = false;

        if (debugCombat)
        {
            Debug.Log($"CaptainCombat: {gameObject.name} finished attack");
        }
    }
    
    public override void UpdateCombatStats()
    {
        // Call base implementation first
        base.UpdateCombatStats();
        
        // Update damage dealer with new stats
        if (swordDamageDealer != null)
        {
            swordDamageDealer.SetDamage(currentDamage);
            
            if (debugCombat)
            {
                Debug.Log($"CaptainCombat: Updated {gameObject.name} damage to {currentDamage}");
            }
        }
        
        // Notify influence system of tier change
        if (influenceSystem != null)
        {
            influenceSystem.OnCaptainTierChanged();
        }
    }
    
    protected override void HandleRebellion(Villager v)
    {
        base.HandleRebellion(v);
        
        // Notify influence system that captain became rebel
        if (influenceSystem != null)
        {
            influenceSystem.OnCaptainBecameRebel();
        }
    }
    
    // IMPORTANT: Ensure proper cleanup
    protected override void OnDestroy()
    {
        base.OnDestroy();
        
        // The influence system will handle its own cleanup
    }
    
    // Public getters that delegate to influence system
    public float GetCurrentInfluenceRadius() => influenceSystem != null ? influenceSystem.GetCurrentInfluenceRadius() : 0f;
    public int GetInfluencedCommonersCount() => influenceSystem != null ? influenceSystem.GetInfluencedCommonersCount() : 0;
    public int GetFollowerCount() => influenceSystem != null ? influenceSystem.GetFollowerCount() : 0;
    public int GetMaxFollowers() => influenceSystem != null ? influenceSystem.GetMaxFollowers() : 0;
    
    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        // Draw sword pivot point
        if (swordTransform != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(swordTransform.position, 0.1f);

            // Draw line showing sword direction
            Vector3 swordDirection = swordTransform.rotation * Vector3.right;
            Gizmos.DrawRay(swordTransform.position, swordDirection * 1f);

            // Draw sword sprite position
            if (Application.isPlaying && swordSprite != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(swordSprite.transform.position, 0.05f);
                Gizmos.DrawLine(swordTransform.position, swordSprite.transform.position);
            }
        }

        // Draw combat ready distance
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, combatReadyDistance);

        // ADDED: Draw damage window visualization
        if (Application.isPlaying && debugCombat && isAttacking)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 0.3f);
        }
        
        // Note: Influence radius and follower visualization is now handled by CaptainInfluenceSystem
    }
    
    // Context menu for debugging
    [ContextMenu("Debug Captain Stats")]
    public void DebugCaptainStats()
    {
        Debug.Log($"Captain {gameObject.name} Combat Stats:");
        Debug.Log($"  Damage: {currentDamage}");
        Debug.Log($"  Attack Cooldown: {currentAttackCooldown:F2}s");
        Debug.Log($"  Attack Range: {currentAttackRange:F2}");
        Debug.Log($"  Combat Efficiency: {combatEfficiency:P0}");
        
        if (influenceSystem != null)
        {
            Debug.Log($"Captain {gameObject.name} Influence Stats:");
            Debug.Log($"  Influence Radius: {influenceSystem.GetCurrentInfluenceRadius():F2}");
            Debug.Log($"  Influenced Commoners: {influenceSystem.GetInfluencedCommonersCount()}");
            Debug.Log($"  Followers: {influenceSystem.GetFollowerCount()}/{influenceSystem.GetMaxFollowers()}");
        }
    }
}