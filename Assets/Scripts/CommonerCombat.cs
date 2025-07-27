using UnityEngine;
using System.Collections;

public class CommonerCombat : VillagerCombat
{
    [Header("Commoner Specific")]
    [SerializeField] private Transform shovelTransform;
    [SerializeField] private GameObject shovelSprite;
    [SerializeField] private GameObject attackFXObject;
    [SerializeField] private float attackAnimationDuration = 0.5f;
    
    [Header("Shovel Positioning")]
    [SerializeField] private Vector3 shovelOffset = new Vector3(0.5f, 0, 0); // Offset from center when facing right
    [SerializeField] private float rotationSpeed = 5f;
    [SerializeField] private bool smoothRotation = true;
    [SerializeField] private float idleRotation = 0f; // Rotation when idle (0 = upright)
    [SerializeField] private float combatReadyDistance = 10f; // Distance at which shovel starts tracking
    [SerializeField] private Vector3 shovelSpriteOffset = new Vector3(0, 0.5f, 0); // Offset of sprite from pivot point
    
    // Cached components
    private Animator shovelAnimator;
    private Animator attackFXAnimator;
    private ShovelDamageDealer shovelDamageDealer;
    private SpriteRenderer villagerSprite;
    private bool isFacingRight = true;
    
    protected override void Awake()
    {
        base.Awake();
        
        // Auto-find shovel hierarchy if not assigned
        if (shovelTransform == null)
        {
            shovelTransform = transform.Find("Shovel");
            if (shovelTransform == null)
            {
                foreach (Transform child in GetComponentsInChildren<Transform>())
                {
                    if (child.name.ToLower().Contains("shovel") && child != transform)
                    {
                        shovelTransform = child;
                        break;
                    }
                }
            }
        }
        
        // Find shovel sprite and its components
        if (shovelTransform != null)
        {
            // Apply initial offset
            shovelTransform.localPosition = shovelOffset;
            
            // Find shovel sprite
            if (shovelSprite == null)
            {
                Transform sprite = shovelTransform.Find("ShovelSprite");
                if (sprite != null)
                {
                    shovelSprite = sprite.gameObject;
                }
                else
                {
                    // Try to find any child with sprite renderer
                    SpriteRenderer sr = shovelTransform.GetComponentInChildren<SpriteRenderer>();
                    if (sr != null) shovelSprite = sr.gameObject;
                }
            }
            
            // Get shovel animator
            if (shovelSprite != null)
            {
                shovelAnimator = shovelSprite.GetComponent<Animator>();
                if (shovelAnimator == null)
                {
                    Debug.LogWarning($"CommonerCombat: No Animator found on shovel sprite for {gameObject.name}!");
                }
                
                // Set up damage dealer component on shovel
                shovelDamageDealer = shovelSprite.GetComponent<ShovelDamageDealer>();
                if (shovelDamageDealer == null)
                {
                    Debug.LogWarning($"CommonerCombat: No ShovelDamageDealer found, adding one to {shovelSprite.name}");
                    shovelDamageDealer = shovelSprite.AddComponent<ShovelDamageDealer>();
                }
                Debug.Log($"CommonerCombat: Found/Added ShovelDamageDealer on {shovelSprite.name}");
                
                // Apply sprite offset to position shovel sprite within container
                shovelSprite.transform.localPosition = shovelSpriteOffset;
            }
            
            // Find attack FX
            if (attackFXObject == null)
            {
                Transform fx = shovelTransform.Find("attackFX");
                if (fx == null)
                {
                    fx = shovelTransform.Find("AttackFX");
                }
                if (fx != null)
                {
                    attackFXObject = fx.gameObject;
                }
            }
        }
        
        // Get FX animator if exists
        if (attackFXObject != null)
        {
            attackFXAnimator = attackFXObject.GetComponent<Animator>();
        }
        
        // Get villager sprite renderer for facing direction
        villagerSprite = GetComponent<SpriteRenderer>();
        
        if (shovelTransform == null)
        {
            Debug.LogError($"CommonerCombat: No shovel transform found on {gameObject.name}!");
        }
    }
    
    protected override void Start()
    {
        base.Start();
        
        // Set commoner-specific base stats
        baseDamage = 8;
        baseAttackCooldown = 1.5f;
        baseAttackRange = 1.5f;
        
        // Configure damage dealer
        if (shovelDamageDealer != null)
        {
            shovelDamageDealer.Initialize(this, villager);
        }
        
        // Apply initial shovel container position
        if (shovelTransform != null)
        {
            shovelTransform.localPosition = shovelOffset;
            shovelTransform.rotation = Quaternion.Euler(0, 0, idleRotation);
        }
        
        // Ensure sprite is at correct offset
        if (shovelSprite != null)
        {
            shovelSprite.transform.localPosition = shovelSpriteOffset;
        }
    }
    
    protected override void Update()
    {
        base.Update();
        
        // Update shovel rotation based on state
        if (!isAttacking && shovelTransform != null)
        {
            if (currentTarget != null)
            {
                float distanceToTarget = Vector3.Distance(transform.position, currentTarget.position);
                
                // Only rotate shovel when target is within combat ready distance
                if (distanceToTarget <= combatReadyDistance)
                {
                    UpdateShovelRotation();
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
    
    private void UpdateShovelRotation()
    {
        // Calculate direction from shovel container to target
        Vector3 directionToTarget = (currentTarget.position - shovelTransform.position).normalized;
        float angle = Mathf.Atan2(directionToTarget.y, directionToTarget.x) * Mathf.Rad2Deg;
        
        // Adjust for shovel sprite offset - we want the tip to point at target
        // This compensates for the sprite being offset from the pivot
        float offsetAngle = Mathf.Atan2(shovelSpriteOffset.y, shovelSpriteOffset.x) * Mathf.Rad2Deg;
        angle -= offsetAngle;
        
        // Adjust angle based on facing direction
        if (!isFacingRight)
        {
            angle = angle - 180f;
        }
        
        Quaternion targetRotation = Quaternion.Euler(0, 0, angle);
        
        if (smoothRotation)
        {
            shovelTransform.rotation = Quaternion.Lerp(shovelTransform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
        else
        {
            shovelTransform.rotation = targetRotation;
        }
    }
    
    private void ReturnToIdleRotation()
    {
        Quaternion targetRotation = Quaternion.Euler(0, 0, idleRotation);
        
        if (smoothRotation)
        {
            shovelTransform.rotation = Quaternion.Lerp(shovelTransform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
        else
        {
            shovelTransform.rotation = targetRotation;
        }
    }
    
    protected override IEnumerator PerformAttack()
    {
        isAttacking = true;
        lastAttackTime = Time.time;
        
        // Face the target
        FaceTarget();
        
        // Final rotation update to ensure we're facing the target
        if (currentTarget != null && shovelTransform != null)
        {
            Vector3 directionToTarget = (currentTarget.position - transform.position).normalized;
            float angle = Mathf.Atan2(directionToTarget.y, directionToTarget.x) * Mathf.Rad2Deg;
            if (!isFacingRight) angle = angle - 180f;
            shovelTransform.rotation = Quaternion.Euler(0, 0, angle);
        }
        
        // Trigger shovel attack animation FIRST
        if (shovelAnimator != null)
        {
            shovelAnimator.SetTrigger("Attack");
            
            // Get actual animation length if possible
            AnimatorClipInfo[] clipInfo = shovelAnimator.GetCurrentAnimatorClipInfo(0);
            if (clipInfo.Length > 0)
            {
                attackAnimationDuration = clipInfo[0].clip.length;
            }
        }
        else
        {
            Debug.LogWarning($"CommonerCombat: No shovel animator found on {gameObject.name}!");
        }
        
        // Enable damage dealing AFTER animation starts
        if (shovelDamageDealer != null)
        {
            shovelDamageDealer.SetDamage(currentDamage);
            shovelDamageDealer.EnableDamageDealing();
            Debug.Log($"CommonerCombat: Enabled damage dealing for {gameObject.name} with {currentDamage} damage");
        }
        else
        {
            Debug.LogError($"CommonerCombat: No ShovelDamageDealer found on {gameObject.name}!");
        }
        
        // Trigger attack FX animation
        if (attackFXAnimator != null)
        {
            yield return new WaitForSeconds(0.1f); // Small delay for FX
            attackFXAnimator.SetTrigger("Play");
        }
        
        // Trigger main character attack animation if exists
        if (animator != null)
        {
            animator.SetTrigger("Attack");
        }
        
        // Wait for animation to complete
        yield return new WaitForSeconds(attackAnimationDuration);
        
        // Disable damage dealing after attack
        if (shovelDamageDealer != null)
        {
            shovelDamageDealer.DisableDamageDealing();
            Debug.Log($"CommonerCombat: Disabled damage dealing for {gameObject.name}");
        }
        
        isAttacking = false;
    }
    
    private void FaceTarget()
    {
        if (currentTarget == null) return;
        
        Vector3 direction = (currentTarget.position - transform.position).normalized;
        direction.z = 0; // Keep 2D
        
        // Update sprite facing
        bool shouldFaceRight = direction.x > 0;
        
        if (shouldFaceRight != isFacingRight)
        {
            isFacingRight = shouldFaceRight;
            
            // Flip villager sprite
            if (villagerSprite != null)
            {
                villagerSprite.flipX = !isFacingRight;
            }
            
            // Flip shovel position horizontally
            if (shovelTransform != null)
            {
                Vector3 newPosition = shovelOffset;
                if (!isFacingRight)
                {
                    newPosition.x = -Mathf.Abs(shovelOffset.x);
                }
                else
                {
                    newPosition.x = Mathf.Abs(shovelOffset.x);
                }
                shovelTransform.localPosition = newPosition;
            }
        }
    }
    
    public void OnShovelHitEnemy(GameObject enemy, int damageDealt)
    {
        // Called by ShovelDamageDealer when damage is dealt
        OnDamageDealt?.Invoke(damageDealt);
        
        // You can add hit effects, sounds, etc. here
        Debug.Log($"Commoner {gameObject.name} hit {enemy.name} for {damageDealt} damage!");
    }
    
    public override void UpdateCombatStats()
    {
        base.UpdateCombatStats();
        
        // Update damage dealer with new stats
        if (shovelDamageDealer != null)
        {
            shovelDamageDealer.SetDamage(currentDamage);
        }
        
        // Commoner-specific stat adjustments
        if (villager != null)
        {
            VillagerStats stats = villager.GetStats();
            
            // Tier 2 gets faster attacks
            if (stats.tier >= 2)
            {
                currentAttackCooldown *= 0.8f; // 20% faster attacks at tier 2
            }
        }
    }
    
    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        
        // Draw shovel pivot point
        if (shovelTransform != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(shovelTransform.position, 0.1f);
            
            // Draw line showing shovel direction
            Vector3 shovelDirection = shovelTransform.rotation * Vector3.right;
            Gizmos.DrawRay(shovelTransform.position, shovelDirection * 1f);
            
            // Draw shovel sprite position
            if (Application.isPlaying && shovelSprite != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(shovelSprite.transform.position, 0.05f);
                Gizmos.DrawLine(shovelTransform.position, shovelSprite.transform.position);
            }
        }
        
        // Draw shovel offset position
        if (Application.isPlaying)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position + shovelOffset, 0.1f);
            Gizmos.DrawWireSphere(transform.position - new Vector3(shovelOffset.x, -shovelOffset.y, shovelOffset.z), 0.1f);
        }
    }
    
    // Public method to update shovel offset at runtime
    public void SetShovelOffset(Vector3 newOffset)
    {
        shovelOffset = newOffset;
        UpdateShovelPosition();
    }
    
    // Update shovel position based on current facing direction
    private void UpdateShovelPosition()
    {
        if (shovelTransform == null) return;
        
        Vector3 newPosition = shovelOffset;
        if (!isFacingRight)
        {
            newPosition.x = -Mathf.Abs(shovelOffset.x);
        }
        else
        {
            newPosition.x = Mathf.Abs(shovelOffset.x);
        }
        shovelTransform.localPosition = newPosition;
    }
    
    // Called in Unity Editor when values change
    private void OnValidate()
    {
        if (Application.isPlaying && shovelTransform != null)
        {
            UpdateShovelPosition();
        }
    }
}