using UnityEngine;
using System.Collections.Generic;
using System.Collections;

// Particle cone attack system for Ghost Mage enemies
public class GhostMageParticleAttack : BaseEnemyAttack
{
    [Header("Particle Attack Configuration")]
    [SerializeField] private GameObject particleSystemPrefab;
    [SerializeField] private Transform particleSpawnPoint;
    [SerializeField] private float particleDuration = 2f;
    [SerializeField] private float coneAngle = 25f;
    [SerializeField] private float coneRange = 8f; // How far the cone extends
    
    [Header("Damage Control")]
    [SerializeField] private float hitCooldownPerTarget = 0.1f; // Prevent multiple hits too quickly

    [Header("Audio")]
    [SerializeField] private AudioClip particleAttackSound;
    [SerializeField] private AudioClip particleHitSound;
    
    // Damage tracking to prevent multiple hits per attack
    private HashSet<GameObject> hitTargetsThisAttack = new HashSet<GameObject>();
    private Dictionary<GameObject, float> lastHitTimes = new Dictionary<GameObject, float>();
    
    // Components
    private AudioSource audioSource;
    private GameObject currentParticleInstance;
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
        
        // Clean up old hit time entries
        CleanupHitTimes();
    }

    private void CleanupHitTimes()
    {
        // Remove old entries to prevent memory buildup
        List<GameObject> toRemove = new List<GameObject>();
        
        foreach (var kvp in lastHitTimes)
        {
            if (kvp.Key == null || Time.time - kvp.Value > 10f) // Clean up after 10 seconds
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
        
        // Clear hit tracking for this attack
        hitTargetsThisAttack.Clear();
        isParticleAttackActive = true;

        Vector3 spawnPosition = particleSpawnPoint != null ? particleSpawnPoint.position : transform.position;
        // Calculate and store attack direction
        attackDirection = (currentTarget.position - spawnPosition).normalized;
        
        // Instantiate the particle system prefab
        currentParticleInstance = Instantiate(particleSystemPrefab, spawnPosition, Quaternion.LookRotation(attackDirection));

        currentParticleSystem = currentParticleInstance.GetComponent<ParticleSystem>();
        
        if (currentParticleSystem == null)
        {
            Debug.LogError($"Particle system prefab {particleSystemPrefab.name} doesn't have a ParticleSystem component!");
            isParticleAttackActive = false;
            yield break;
        }
        
        // Set up collision detection for particles
        SetupParticleCollision();
        
        // Start the particle system
        currentParticleSystem.Play();
        
        // Play attack sound
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
        
        // Clean up
        isParticleAttackActive = false;
        hitTargetsThisAttack.Clear();
        
        if (currentParticleInstance != null)
        {
            Destroy(currentParticleInstance);
        }
        currentParticleSystem = null;
    }
    
    private void SetupParticleCollision()
    {
        if (currentParticleSystem == null) return;
        
        // Enable collision module
        var collision = currentParticleSystem.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision2D;
        collision.sendCollisionMessages = true;
        collision.collidesWith = attackableLayers;
        
        // Add collision callback component
        ParticleCollisionCallback collisionCallback = currentParticleInstance.GetComponent<ParticleCollisionCallback>();
        if (collisionCallback == null)
        {
            collisionCallback = currentParticleInstance.AddComponent<ParticleCollisionCallback>();
        }
        collisionCallback.Initialize(this);
    }
    
    // Called by ParticleCollisionCallback when particles hit targets
    public void HandleParticleHit(GameObject target, Vector3 hitPosition)
    {
        if (!isParticleAttackActive) return;
        
        // Check if this target is valid and hasn't been hit this attack
        if (IsValidTarget(target) && !hitTargetsThisAttack.Contains(target))
        {
            // Check hit cooldown
            if (CanHitTarget(target))
            {
                // Deal damage
                if (TryDamageTarget(target))
                {
                    hitTargetsThisAttack.Add(target);
                    lastHitTimes[target] = Time.time;
                    
                    // Play hit sound
                    if (particleHitSound != null && audioSource != null)
                    {
                        audioSource.PlayOneShot(particleHitSound);
                    }
                    
                    if (debugTargeting)
                    {
                        Debug.Log($"Particle collided with {target.name} for {damage} damage at {hitPosition}");
                    }
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
        
        return hasHealth && hasValidTag;
    }
    
    private bool CanHitTarget(GameObject target)
    {
        if (!lastHitTimes.ContainsKey(target)) return true;
        
        return Time.time - lastHitTimes[target] >= hitCooldownPerTarget;
    }

    
    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        
        // Draw particle cone area when targeting or attacking
        if (currentTarget != null && Application.isPlaying)
        {
            Vector3 origin = currentParticleInstance != null ? currentParticleInstance.transform.position : transform.position;
            Vector3 direction = isParticleAttackActive ? attackDirection : (currentTarget.position - origin).normalized;
            float halfAngle = coneAngle * 0.5f * Mathf.Deg2Rad;
            
            // Draw cone outline
            Gizmos.color = Color.magenta;
            Vector3 leftBound = new Vector3(
                direction.x * Mathf.Cos(halfAngle) - direction.y * Mathf.Sin(halfAngle),
                direction.x * Mathf.Sin(halfAngle) + direction.y * Mathf.Cos(halfAngle),
                0
            ) * coneRange;
            
            Vector3 rightBound = new Vector3(
                direction.x * Mathf.Cos(-halfAngle) - direction.y * Mathf.Sin(-halfAngle),
                direction.x * Mathf.Sin(-halfAngle) + direction.y * Mathf.Cos(-halfAngle),
                0
            ) * coneRange;
            
            Gizmos.DrawLine(origin, origin + leftBound);
            Gizmos.DrawLine(origin, origin + rightBound);
            Gizmos.DrawLine(origin + leftBound, origin + rightBound);
        }
        
        // Draw particle system position
        if (particleSpawnPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(particleSpawnPoint.position, 0.3f);
        }
    }
}

// Helper component for particle collision callbacks
public class ParticleCollisionCallback : MonoBehaviour
{
    private GhostMageParticleAttack attackSystem;
    
    public void Initialize(GhostMageParticleAttack system)
    {
        attackSystem = system;
    }
    
    void OnParticleCollision(GameObject other)
    {
        // Get collision events
        List<ParticleCollisionEvent> collisionEvents = new List<ParticleCollisionEvent>();
        int numCollisionEvents = GetComponent<ParticleSystem>().GetCollisionEvents(other, collisionEvents);
        
        // Process each collision
        for (int i = 0; i < numCollisionEvents; i++)
        {
            attackSystem?.HandleParticleHit(other, collisionEvents[i].intersection);
        }
    }
}