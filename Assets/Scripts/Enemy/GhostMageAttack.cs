using UnityEngine;
using System.Collections.Generic;
using System.Collections;

// Simplified Ghost Mage attack system with moving damage collider
public class GhostMageAttack : BaseEnemyAttack
{
    [Header("Particle Attack Configuration")]
    [SerializeField] private GameObject particleSystemPrefab;
    [SerializeField] private Transform particleSpawnPoint;
    [SerializeField] private float particleDuration = 2f;
    [SerializeField] private float animationWindupDelay = 0.5f;
    
    [Header("Damage Collider Configuration")]
    [SerializeField] private Vector2 colliderBaseSize = new Vector2(1f, 0.5f);
    [SerializeField] private float colliderMoveSpeed = 8f;
    [SerializeField] private float colliderMaxDistance = 10f;
    [SerializeField] private float colliderGrowthRate = 2f; // Y growth multiplier over time
    [SerializeField] private LayerMask colliderHitLayers = -1;
    
    [Header("Audio")]
    [SerializeField] private AudioClip particleAttackSound;
    [SerializeField] private AudioClip particleHitSound;
    
    // Components
    private AudioSource audioSource;
    
    // Attack state
    private bool isParticleAttackActive = false;
    private Vector3 attackDirection;
    
    protected override void InitializeComponents()
    {
        base.InitializeComponents();
        
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // Auto-create spawn point if not assigned
        if (particleSpawnPoint == null)
        {
            GameObject spawnObj = new GameObject("ParticleSpawnPoint");
            spawnObj.transform.SetParent(transform);
            spawnObj.transform.localPosition = Vector3.zero;
            particleSpawnPoint = spawnObj.transform;
        }
    }
    
    protected override void ValidateConfiguration()
    {
        base.ValidateConfiguration();
        
        if (particleSystemPrefab == null)
        {
            Debug.LogError($"GhostMageAttack on {gameObject.name}: No particle system prefab assigned!");
        }
    }
    
    protected override void ExecuteAttack()
    {
        if (isParticleAttackActive) return; // Prevent overlapping attacks
        
        StartCoroutine(PerformParticleAttack());
    }
    
    private IEnumerator PerformParticleAttack()
    {
        if (particleSystemPrefab == null || currentTarget == null) yield break;
        
        isParticleAttackActive = true;
        Vector3 spawnPosition = particleSpawnPoint.position;
        attackDirection = (currentTarget.position - spawnPosition).normalized;
        
        // Play attack animation
        if (animator != null)
        {
            animator.SetTrigger("Attack");
        }
        
        // Wait for animation windup
        yield return new WaitForSeconds(animationWindupDelay);
        
        // Spawn particle system
        GameObject particleInstance = Instantiate(particleSystemPrefab, spawnPosition, 
            Quaternion.LookRotation(attackDirection));
        
        ParticleSystem particles = particleInstance.GetComponent<ParticleSystem>();
        if (particles != null)
        {
            // Disable built-in particle collision
            var collision = particles.collision;
            collision.enabled = false;
            particles.Play();
        }
        
        // Create and launch damage collider
        StartCoroutine(MoveDamageCollider(spawnPosition));
        
        // Play attack sound
        if (particleAttackSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(particleAttackSound);
        }
        
        // Wait for particle duration
        yield return new WaitForSeconds(particleDuration);
        
        // Clean up
        if (particleInstance != null)
        {
            Destroy(particleInstance);
        }
        
        isParticleAttackActive = false;
        
        if (debugTargeting)
        {
            Debug.Log($"{gameObject.name} particle attack completed");
        }
    }
    
    private IEnumerator MoveDamageCollider(Vector3 startPosition)
    {
        // Create temporary collider object
        GameObject colliderObj = new GameObject("DamageCollider");
        colliderObj.transform.position = startPosition;
        
        // Set up collider
        BoxCollider2D boxCollider = colliderObj.AddComponent<BoxCollider2D>();
        boxCollider.isTrigger = true;
        boxCollider.size = colliderBaseSize;
        
        // Track hit targets to prevent multiple hits
        HashSet<GameObject> hitTargets = new HashSet<GameObject>();
        
        float elapsed = 0f;
        float totalDistance = 0f;
        
        while (elapsed < particleDuration && totalDistance < colliderMaxDistance)
        {
            float deltaTime = Time.deltaTime;
            elapsed += deltaTime;
            
            // Move collider
            float moveDistance = colliderMoveSpeed * deltaTime;
            colliderObj.transform.position += attackDirection * moveDistance;
            totalDistance += moveDistance;
            
            // Update collider size (grow over time)
            float progress = elapsed / particleDuration;
            float yMultiplier = 1f + (colliderGrowthRate - 1f) * progress;
            boxCollider.size = new Vector2(colliderBaseSize.x, colliderBaseSize.y * yMultiplier);
            
            // Check for collisions manually
            Collider2D[] hits = Physics2D.OverlapBoxAll(
                colliderObj.transform.position,
                boxCollider.size,
                0f,
                colliderHitLayers
            );
            
            foreach (Collider2D hit in hits)
            {
                GameObject target = hit.gameObject;
                
                // Skip if already hit or invalid target
                if (hitTargets.Contains(target) || !IsValidTarget(target))
                    continue;
                
                // Deal damage
                if (TryDamageTarget(target))
                {
                    hitTargets.Add(target);
                    
                    // Play hit sound
                    if (particleHitSound != null && audioSource != null)
                    {
                        audioSource.PlayOneShot(particleHitSound);
                    }
                    
                    if (debugTargeting)
                    {
                        Debug.Log($"Particle collider hit {target.name} for {damage} damage");
                    }
                }
            }
            
            yield return null;
        }
        
        // Clean up collider
        Destroy(colliderObj);
    }
    
    private bool IsValidTarget(GameObject target)
    {
        // Check if target has a health component
        if (!HasHealthComponent(target)) return false;
        
        // Check if target has valid tag
        bool hasValidTag = false;
        foreach (string tag in attackableTags)
        {
            if (target.CompareTag(tag))
            {
                hasValidTag = true;
                break;
            }
        }
        
        return hasValidTag;
    }
    
    public override bool IsAttacking() => isParticleAttackActive;
    
    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        
        // Draw particle spawn point
        if (particleSpawnPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(particleSpawnPoint.position, 0.3f);
        }
        
        // Draw collider path when targeting
        if (currentTarget != null && Application.isPlaying)
        {
            Vector3 origin = particleSpawnPoint != null ? particleSpawnPoint.position : transform.position;
            Vector3 direction = (currentTarget.position - origin).normalized;
            
            // Draw movement path
            Gizmos.color = Color.magenta;
            Gizmos.DrawRay(origin, direction * colliderMaxDistance);
            
            // Draw collider size at different points along path
            for (int i = 0; i <= 3; i++)
            {
                float t = i / 3f;
                Vector3 position = origin + direction * (colliderMaxDistance * t);
                float yGrowth = 1f + (colliderGrowthRate - 1f) * t;
                Vector2 size = new Vector2(colliderBaseSize.x, colliderBaseSize.y * yGrowth);
                
                Gizmos.color = Color.Lerp(Color.yellow, Color.red, t);
                Gizmos.DrawWireCube(position, size);
            }
        }
    }
}