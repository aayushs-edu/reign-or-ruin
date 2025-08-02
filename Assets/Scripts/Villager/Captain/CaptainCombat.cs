using UnityEngine;
using System.Collections;

public class CaptainCombat : VillagerCombat
{
    [Header("Captain Specific")]
    [SerializeField] private Transform swordTransform;
    [SerializeField] private GameObject swordSprite;
    [SerializeField] private GameObject attackFXObject;
    [SerializeField] private float attackAnimationDuration = 0.4f;
    
    [Header("Attack Timing - CRITICAL FOR DAMAGE")]
    [SerializeField] private float damageWindowStart = 0.1f; // When damage window opens (seconds into attack)
    [SerializeField] private float damageWindowEnd = 0.3f;   // When damage window closes
    [SerializeField] private bool debugAttackTiming = true;

    [Header("Sword Positioning")]
    [SerializeField] private float idleRotation = 0f;
    [SerializeField] private float combatReadyDistance = 20f;
    [SerializeField] private Vector3 swordSpriteOffset;
    
    [Header("Debug")]
    [SerializeField] private bool debugCombat = false;
    
    // Cached components
    private Animator swordAnimator;
    private SwordDamageDealer swordDamageDealer;
    private CaptainInfluenceSystem influenceSystem;
    
    // Attack state tracking
    private bool isDamageWindowActive = false;
    private Coroutine currentAttackCoroutine = null;
    
    protected override void Awake()
    {
        base.Awake();
        
        // Get the influence system component
        influenceSystem = GetComponent<CaptainInfluenceSystem>();
        if (influenceSystem == null)
        {
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

        // Update sword rotation based on state
        if (!isAttacking && swordTransform != null)
        {
            if (currentTarget != null)
            {
                float distanceToTarget = Vector3.Distance(transform.position, currentTarget.position);

                if (distanceToTarget <= combatReadyDistance)
                {
                    UpdateSwordRotation();
                }
                else
                {
                    ReturnToIdleRotation();
                }
            }
            else
            {
                ReturnToIdleRotation();
            }
        }
    }

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
    
    // FIXED: Proper attack timing with damage window control
    protected override IEnumerator PerformAttack()
    {
        if (debugAttackTiming)
        {
            Debug.Log($"CaptainCombat: {gameObject.name} starting attack on {currentTarget?.name} at time {Time.time:F2}");
        }

        isAttacking = true;
        isDamageWindowActive = false; // Start with damage window closed
        lastAttackTime = Time.time;
        currentAttackCoroutine = StartCoroutine(AttackCoroutine());

        // Wait for the attack coroutine to complete
        yield return currentAttackCoroutine;
        
        currentAttackCoroutine = null;
    }
    
    private IEnumerator AttackCoroutine()
    {
        // Trigger sword attack animation
        if (swordAnimator != null)
        {
            swordAnimator.SetTrigger("Attack");
            
            if (debugAttackTiming)
            {
                Debug.Log($"CaptainCombat: {gameObject.name} triggered sword animation at {Time.time:F2}");
            }
        }

        // Wait for damage window to start
        yield return new WaitForSeconds(damageWindowStart);
        
        // Open damage window
        isDamageWindowActive = true;
        
        if (debugAttackTiming)
        {
            Debug.Log($"CaptainCombat: {gameObject.name} DAMAGE WINDOW OPENED at {Time.time:F2}");
        }

        // Keep damage window open for specified duration
        float damageWindowDuration = damageWindowEnd - damageWindowStart;
        yield return new WaitForSeconds(damageWindowDuration);
        
        
        
        if (debugAttackTiming)
        {
            Debug.Log($"CaptainCombat: {gameObject.name} DAMAGE WINDOW CLOSED at {Time.time:F2}");
        }

        // Wait for remaining animation time
        float remainingTime = attackAnimationDuration - damageWindowEnd;
        if (remainingTime > 0)
        {
            yield return new WaitForSeconds(remainingTime);
        }

        // Close damage window
        isDamageWindowActive = false;

        // Attack complete
        isAttacking = false;
        
        if (debugAttackTiming)
        {
            Debug.Log($"CaptainCombat: {gameObject.name} attack completed at {Time.time:F2}");
        }
    }
    
    // CRITICAL: New method that SwordDamageDealer should use instead of IsAttacking()
    public bool IsDamageWindowActive()
    {
        return isAttacking && isDamageWindowActive;
    }
    
    
    // Stop attack if needed (for interruptions)
    public void StopAttack()
    {
        if (currentAttackCoroutine != null)
        {
            StopCoroutine(currentAttackCoroutine);
            currentAttackCoroutine = null;
        }
        
        isAttacking = false;
        isDamageWindowActive = false;
        
        if (debugAttackTiming)
        {
            Debug.Log($"CaptainCombat: {gameObject.name} attack interrupted at {Time.time:F2}");
        }
    }
    
    public override void UpdateCombatStats()
    {
        base.UpdateCombatStats();
        
        if (swordDamageDealer != null)
        {
            swordDamageDealer.SetDamage(currentDamage);
            
            if (debugCombat)
            {
                Debug.Log($"CaptainCombat: Updated {gameObject.name} damage to {currentDamage}");
            }
        }
        
        if (influenceSystem != null)
        {
            influenceSystem.OnCaptainTierChanged();
        }
    }
    
    protected override void HandleRebellion(Villager v)
    {
        base.HandleRebellion(v);
        
        if (influenceSystem != null)
        {
            influenceSystem.OnCaptainBecameRebel();
        }
    }
    
    protected override void OnDestroy()
    {
        // Stop any ongoing attack
        if (currentAttackCoroutine != null)
        {
            StopCoroutine(currentAttackCoroutine);
        }
        
        base.OnDestroy();
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

            Vector3 swordDirection = swordTransform.rotation * Vector3.right;
            Gizmos.DrawRay(swordTransform.position, swordDirection * 1f);

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

        // Draw damage window visualization
        if (Application.isPlaying)
        {
            if (isDamageWindowActive)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(transform.position, 0.3f);
            }
            else if (isAttacking)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(transform.position, 0.2f);
            }
        }
    }
    
    [ContextMenu("Debug Captain Stats")]
    public void DebugCaptainStats()
    {
        Debug.Log($"Captain {gameObject.name} Combat Stats:");
        Debug.Log($"  Damage: {currentDamage}");
        Debug.Log($"  Attack Cooldown: {currentAttackCooldown:F2}s");
        Debug.Log($"  Attack Range: {currentAttackRange:F2}");
        Debug.Log($"  Combat Efficiency: {combatEfficiency:P0}");
        Debug.Log($"  Is Attacking: {isAttacking}");
        Debug.Log($"  Damage Window Active: {isDamageWindowActive}");
        
        if (influenceSystem != null)
        {
            Debug.Log($"Captain {gameObject.name} Influence Stats:");
            Debug.Log($"  Influence Radius: {influenceSystem.GetCurrentInfluenceRadius():F2}");
            Debug.Log($"  Influenced Commoners: {influenceSystem.GetInfluencedCommonersCount()}");
            Debug.Log($"  Followers: {influenceSystem.GetFollowerCount()}/{influenceSystem.GetMaxFollowers()}");
        }
    }
}