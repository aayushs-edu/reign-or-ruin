using UnityEngine;
using System.Collections.Generic;
using System.Collections;

// Moving collider attack system for Ghost Mage enemies
public class GhostMageParticleAttack : BaseEnemyAttack
{
    [Header("Particle Attack Configuration")]
    [SerializeField] private GameObject particleSystemPrefab;
    [SerializeField] private Transform particleSpawnPoint;
    [SerializeField] private float particleDuration = 2f;
    
    [Header("Animation Configuration")]
    [SerializeField] private float animationWindupDelay = 0.5f; // Delay before particles spawn after animation starts
    
    [Header("Moving Collider Configuration")]
    [SerializeField] private Vector3 colliderBaseSize = new Vector3(1f, 0.5f, 1f);
    [SerializeField] private LayerMask colliderHitLayers = -1;
    [SerializeField] private float colliderMaxDistance = 10f;
    [SerializeField] private float colliderMoveSpeed = 8f;
    [SerializeField] private float colliderGrowthRate = 2f; // Y growth multiplier over time
    
    [Header("Damage Control")]
    [SerializeField] private float hitCooldownPerTarget = 0.5f;

    [Header("Audio")]
    [SerializeField] private AudioClip particleAttackSound;
    [SerializeField] private AudioClip particleHitSound;
    
    // Damage tracking
    private Dictionary<GameObject, float> lastHitTimes = new Dictionary<GameObject, float>();
    
    // Components
    private AudioSource audioSource;
    private Animator animator;
    private GameObject currentParticleInstance;
    private GameObject currentColliderInstance;
    private ParticleSystem currentParticleSystem;
    private Vector3 attackDirection;
    private bool isParticleAttackActive = false;
    
    protected override void InitializeComponents()
    {
        base.InitializeComponents();
        
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogWarning($"GhostMageParticleAttack: No Animator found on {gameObject.name}! Attack animation will not play.");
        }
        
        // Auto-create spawn point if not assigned
        if (particleSpawnPoint == null)
        {
            GameObject spawnObj = new GameObject("ParticleSpawnPoint");
            spawnObj.transform.SetParent(transform);
            spawnObj.transform.localPosition = Vector3.zero;
            particleSpawnPoint = spawnObj.transform;
            Debug.LogWarning($"GhostMageParticleAttack: Created particleSpawnPoint for {gameObject.name}");
        }
    }
    
    protected override void ValidateConfiguration()
    {
        base.ValidateConfiguration();
        
        if (particleSystemPrefab == null)
        {
            Debug.LogError($"GhostMageParticleAttack on {gameObject.name}: No particle system prefab assigned!");
        }
    }
    
    protected override void Update()
    {
        base.Update();
        CleanupHitTimes();
    }

    private void CleanupHitTimes()
    {
        List<GameObject> toRemove = new List<GameObject>();
        
        foreach (var kvp in lastHitTimes)
        {
            if (kvp.Key == null || Time.time - kvp.Value > 10f)
            {
                toRemove.Add(kvp.Key);
            }
        }
        
        foreach (var key in toRemove)
        {
            lastHitTimes.Remove(key);
        }
    }
    
    protected override void ExecuteAttack()
    {
        StartCoroutine(PerformParticleAttack());
    }
    
    private IEnumerator PerformParticleAttack()
    {
        if (particleSystemPrefab == null || currentTarget == null) yield break;
        
        isParticleAttackActive = true;
        Vector3 spawnPosition = particleSpawnPoint != null ? particleSpawnPoint.position : transform.position;
        attackDirection = (currentTarget.position - spawnPosition).normalized;
        
        // Play attack animation first
        if (animator != null)
        {
            animator.SetTrigger("Attack");
        }
        
        if (debugTargeting)
        {
            Debug.Log($"{gameObject.name} started attack animation, particles will spawn in {animationWindupDelay}s");
        }
        
        // Wait for animation windup delay
        yield return new WaitForSeconds(animationWindupDelay);
        
        // Now spawn particle system and collider
        currentParticleInstance = Instantiate(particleSystemPrefab, spawnPosition, Quaternion.LookRotation(attackDirection));
        currentParticleSystem = currentParticleInstance.GetComponent<ParticleSystem>();
        
        if (currentParticleSystem != null)
        {
            // Disable particle collision since we're using our own collider
            var collision = currentParticleSystem.collision;
            collision.enabled = false;
            
            currentParticleSystem.Play();
        }
        
        // Create collider as child of particle system
        currentColliderInstance = new GameObject("ParticleCollider");
        currentColliderInstance.transform.SetParent(currentParticleInstance.transform);
        currentColliderInstance.transform.localPosition = Vector3.zero;
        
        // Add box collider
        BoxCollider2D boxCollider = currentColliderInstance.AddComponent<BoxCollider2D>();
        boxCollider.isTrigger = true;
        boxCollider.size = colliderBaseSize;
        
        // Add collision detection component
        ParticleColliderMover mover = currentColliderInstance.AddComponent<ParticleColliderMover>();
        mover.Initialize(this, attackDirection, colliderMoveSpeed, colliderMaxDistance, 
                        particleDuration, colliderGrowthRate, colliderBaseSize, colliderHitLayers);
        
        // Play attack sound when particles actually fire
        if (particleAttackSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(particleAttackSound);
        }
        
        if (debugTargeting)
        {
            Debug.Log($"{gameObject.name} launched particle attack at {currentTarget.name}");
        }
        
        // Keep attack active for duration
        yield return new WaitForSeconds(particleDuration);
        
        // Clean up and reset animation state
        isParticleAttackActive = false;
        
        // Reset animator to idle state
        if (animator != null)
        {
            animator.ResetTrigger("Attack"); // Clear any pending triggers
        }
        
        if (currentParticleInstance != null)
        {
            Destroy(currentParticleInstance);
        }
        
        currentParticleSystem = null;
        
        if (debugTargeting)
        {
            Debug.Log($"{gameObject.name} attack completed and animation reset");
        }
    }
    
    // Called by ParticleColliderMover when collider hits targets
    public void HandleColliderHit(GameObject target, Vector3 hitPosition)
    {
        if (!isParticleAttackActive) return;
        
        // Check if this target is valid
        if (IsValidTarget(target) && CanHitTarget(target))
        {
            // Deal damage
            if (TryDamageTarget(target))
            {
                lastHitTimes[target] = Time.time;
                
                // Play hit sound
                if (particleHitSound != null && audioSource != null)
                {
                    audioSource.PlayOneShot(particleHitSound);
                }
                
                if (debugTargeting)
                {
                    Debug.Log($"Particle collider hit {target.name} for {damage} damage at {hitPosition}");
                }
            }
        }
    }
    
    private bool IsValidTarget(GameObject target)
    {
        // Check if target has a health component and is an attackable tag
        bool hasHealth = HasHealthComponent(target);
        bool hasValidTag = false;
        
        foreach (string tag in attackableTags)
        {
            if (target.CompareTag(tag))
            {
                hasValidTag = true;
                break;
            }
        }
        
        // Check layer mask
        bool inCorrectLayer = (colliderHitLayers.value & (1 << target.layer)) != 0;
        
        return hasHealth && hasValidTag && inCorrectLayer;
    }
    
    private bool CanHitTarget(GameObject target)
    {
        if (!lastHitTimes.ContainsKey(target)) return true;
        
        return Time.time - lastHitTimes[target] >= hitCooldownPerTarget;
    }
    
    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        
        // Draw collider path when targeting
        if (currentTarget != null && Application.isPlaying)
        {
            Vector3 origin = particleSpawnPoint != null ? particleSpawnPoint.position : transform.position;
            Vector3 direction = (currentTarget.position - origin).normalized;
            
            // Draw collider movement path
            Gizmos.color = Color.magenta;
            Gizmos.DrawRay(origin, direction * colliderMaxDistance);
            
            // Draw collider size at different points
            for (int i = 0; i <= 3; i++)
            {
                float t = i / 3f;
                Vector3 position = origin + direction * (colliderMaxDistance * t);
                float yGrowth = 1f + (colliderGrowthRate - 1f) * t;
                Vector3 size = new Vector3(colliderBaseSize.x, colliderBaseSize.y * yGrowth, colliderBaseSize.z);
                
                Gizmos.color = Color.Lerp(Color.yellow, Color.red, t);
                Gizmos.DrawWireCube(position, size);
            }
        }
        
        // Draw particle system position
        if (particleSpawnPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(particleSpawnPoint.position, 0.3f);
        }
    }
}

// Component that handles the moving collider behavior
public class ParticleColliderMover : MonoBehaviour
{
    private GhostMageParticleAttack attackSystem;
    private Vector3 moveDirection;
    private float moveSpeed;
    private float maxDistance;
    private float duration;
    private float growthRate;
    private Vector3 baseSize;
    private LayerMask hitLayers;
    private BoxCollider2D boxCollider;
    
    private Vector3 startPosition;
    private float startTime;
    private HashSet<GameObject> hitTargets = new HashSet<GameObject>();
    
    public void Initialize(GhostMageParticleAttack system, Vector3 direction, float speed, float distance, 
                          float attackDuration, float yGrowthRate, Vector3 size, LayerMask layers)
    {
        attackSystem = system;
        moveDirection = direction.normalized;
        moveSpeed = speed;
        maxDistance = distance;
        duration = attackDuration;
        growthRate = yGrowthRate;
        baseSize = size;
        hitLayers = layers;
        
        startPosition = transform.position;
        startTime = Time.time;
        
        boxCollider = GetComponent<BoxCollider2D>();
        
        // Set initial size
        UpdateColliderSize(0f);
    }
    
    private void Update()
    {
        float elapsed = Time.time - startTime;
        float progress = Mathf.Clamp01(elapsed / duration);
        
        // Move collider
        float distanceTraveled = elapsed * moveSpeed;
        if (distanceTraveled <= maxDistance)
        {
            transform.position = startPosition + moveDirection * distanceTraveled;
        }
        
        // Update collider size based on progress
        UpdateColliderSize(progress);
        
        // Destroy when finished
        if (progress >= 1f || distanceTraveled >= maxDistance)
        {
            Destroy(gameObject);
        }
    }
    
    private void UpdateColliderSize(float progress)
    {
        if (boxCollider == null) return;
        
        float yMultiplier = 1f + (growthRate - 1f) * progress;
        Vector3 newSize = new Vector3(baseSize.x, baseSize.y * yMultiplier, baseSize.z);
        boxCollider.size = newSize;
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        // Check layer mask
        if ((hitLayers.value & (1 << other.gameObject.layer)) == 0) return;
        
        // Prevent multiple hits on the same target
        if (hitTargets.Contains(other.gameObject)) return;
        
        hitTargets.Add(other.gameObject);
        attackSystem?.HandleColliderHit(other.gameObject, other.transform.position);
    }
}