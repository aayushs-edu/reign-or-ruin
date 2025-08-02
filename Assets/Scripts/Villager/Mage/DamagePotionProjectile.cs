using UnityEngine;
using System.Collections;

public class DamagePotionProjectile : MonoBehaviour
{
    [Header("Projectile Settings")]
    [SerializeField] private float flightDuration = 1.5f;
    [SerializeField] private float arcHeight = 3f;
    [SerializeField] private AnimationCurve flightCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private bool rotateInFlight = true;
    [SerializeField] private float rotationSpeed = 360f;
    
    [Header("Impact Settings")]
    [SerializeField] private GameObject impactEffectPrefab;
    [SerializeField] private LayerMask impactLayers = -1;
    [SerializeField] private float impactRadius = 1.5f; // Slightly larger than heal potions
    [SerializeField] private bool destroyOnImpact = true;
    [SerializeField] private float destroyDelay = 0.1f;
    
    [Header("Target Settings")]
    [SerializeField] private LayerMask targetLayers = -1; // Player and loyal villagers
    [SerializeField] private bool canImpactOnTargets = true;
    
    [Header("Visual Settings")]
    [SerializeField] private SpriteRenderer potionSprite;
    [SerializeField] private ParticleSystem trailEffect;
    [SerializeField] private Color damageColor = Color.red;
    [SerializeField] private bool autoFindComponents = true;
    
    [Header("Audio")]
    [SerializeField] private AudioClip launchSound;
    [SerializeField] private AudioClip impactSound;
    [SerializeField] private AudioSource audioSource;
    
    [Header("Debug")]
    [SerializeField] private bool debugProjectile = false;
    
    // Flight state
    private Vector3 startPosition;
    private Vector3 targetPosition;
    private int damageAmount;
    private bool isFlying = false;
    private float flightStartTime;
    private AnimationCurve customFlightCurve;
    private Transform throwerTransform; // Reference to the mage who threw this
    
    // Components
    private Collider2D projectileCollider;
    
    private void Awake()
    {
        if (autoFindComponents)
        {
            FindComponents();
        }
        
        SetupComponents();
    }
    
    private void FindComponents()
    {
        // Find sprite renderer
        if (potionSprite == null)
        {
            potionSprite = GetComponent<SpriteRenderer>();
            if (potionSprite == null)
            {
                potionSprite = GetComponentInChildren<SpriteRenderer>();
            }
        }
        
        // Find particle system for trail
        if (trailEffect == null)
        {
            trailEffect = GetComponentInChildren<ParticleSystem>();
        }
        
        // Find audio source
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
        
        // Find collider
        projectileCollider = GetComponent<Collider2D>();
        if (projectileCollider == null)
        {
            projectileCollider = gameObject.AddComponent<CircleCollider2D>();
        }
    }
    
    private void SetupComponents()
    {
        // Configure collider as trigger
        if (projectileCollider != null)
        {
            projectileCollider.isTrigger = true;
        }
        
        // Set damage color (red liquid)
        if (potionSprite != null)
        {
            potionSprite.color = damageColor;
        }
        
        // Configure trail effect
        if (trailEffect != null)
        {
            var main = trailEffect.main;
            main.startColor = damageColor;
        }
        
        // Configure audio source
        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f; // 3D sound
        }
    }
    
    public void Launch(Vector3 start, Vector3 target, int damage, Transform thrower = null, AnimationCurve curve = null)
    {
        startPosition = start;
        targetPosition = target;
        damageAmount = damage;
        throwerTransform = thrower;
        customFlightCurve = curve ?? flightCurve;
        
        transform.position = startPosition;
        
        isFlying = true;
        flightStartTime = Time.time;
        
        // Play launch sound
        PlaySound(launchSound);
        
        // Start trail effect
        if (trailEffect != null)
        {
            trailEffect.Play();
        }
        
        if (debugProjectile)
        {
            Debug.Log($"Damage potion launched from {start} to {target} with {damage} damage");
        }
        
        // Start flight coroutine
        StartCoroutine(FlightCoroutine());
    }
    
    private IEnumerator FlightCoroutine()
    {
        float elapsedTime = 0f;
        
        while (elapsedTime < flightDuration && isFlying)
        {
            elapsedTime = Time.time - flightStartTime;
            float progress = elapsedTime / flightDuration;
            
            if (progress >= 1f)
            {
                OnReachTarget();
                yield break;
            }
            
            // Calculate position along arc
            UpdatePosition(progress);
            
            // Rotate potion
            if (rotateInFlight)
            {
                transform.Rotate(0, 0, rotationSpeed * Time.deltaTime);
            }
            
            yield return null;
        }
        
        if (isFlying)
        {
            OnReachTarget();
        }
    }
    
    private void UpdatePosition(float progress)
    {
        float curveValue = customFlightCurve.Evaluate(progress);
        
        Vector3 linearPosition = Vector3.Lerp(startPosition, targetPosition, progress);
        float heightOffset = Mathf.Sin(progress * Mathf.PI) * arcHeight;
        heightOffset *= curveValue;
        
        transform.position = linearPosition + Vector3.up * heightOffset;
    }
    
    private void OnReachTarget()
    {
        if (!isFlying) return;
        
        isFlying = false;
        
        // Stop trail effect
        if (trailEffect != null)
        {
            trailEffect.Stop();
        }
        
        // Apply damage effect
        ApplyDamageEffect();
        
        // Spawn impact effect
        SpawnImpactEffect();
        
        // Play impact sound
        PlaySound(impactSound);
        
        if (debugProjectile)
        {
            Debug.Log($"Damage potion reached target at {transform.position}, applying {damageAmount} damage");
        }
        
        // Destroy projectile
        if (destroyOnImpact)
        {
            Destroy(gameObject, destroyDelay);
        }
    }
    
    private void ApplyDamageEffect()
    {
        // Find all targets within impact radius
        Collider2D[] nearbyColliders = Physics2D.OverlapCircleAll(transform.position, impactRadius, targetLayers);
        
        int targetsDamaged = 0;
        
        foreach (var collider in nearbyColliders)
        {
            // Skip the thrower
            if (throwerTransform != null && collider.transform == throwerTransform)
            {
                continue;
            }
            
            // Check for loyal villagers
            Villager villager = collider.GetComponent<Villager>();
            if (villager != null && !villager.IsRebel())
            {
                VillagerHealth villagerHealth = collider.GetComponent<VillagerHealth>();
                if (villagerHealth != null)
                {
                    villagerHealth.TakeDamage(damageAmount);
                    targetsDamaged++;
                    
                    if (debugProjectile)
                    {
                        Debug.Log($"Damage potion hit loyal villager {collider.name} for {damageAmount} damage");
                    }
                }
            }
            else
            {
                // Check for player
                PlayerHealth playerHealth = collider.GetComponent<PlayerHealth>();
                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(damageAmount);
                    targetsDamaged++;
                    
                    if (debugProjectile)
                    {
                        Debug.Log($"Damage potion hit player for {damageAmount} damage");
                    }
                }
            }
        }
        
        if (debugProjectile)
        {
            Debug.Log($"Damage potion effect applied to {targetsDamaged} targets");
        }
    }
    
    private void SpawnImpactEffect()
    {
        if (impactEffectPrefab != null)
        {
            GameObject effect = Instantiate(impactEffectPrefab, transform.position, Quaternion.identity);
            
            // Tint the effect red for damage
            ParticleSystem ps = effect.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                var main = ps.main;
                main.startColor = damageColor;
            }
            
            Destroy(effect, 3f);
            
            if (debugProjectile)
            {
                Debug.Log($"Spawned damage impact effect at {transform.position}");
            }
        }
    }
    
    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isFlying) return;
        
        // Check if we hit a valid target for early impact
        if (canImpactOnTargets && ShouldImpactOnTarget(other))
        {
            if (debugProjectile)
            {
                Debug.Log($"Damage potion hit target {other.name} - triggering early impact");
            }
            
            OnReachTarget();
            return;
        }
        
        // Check if we hit an obstacle
        if (ShouldTriggerEarlyImpact(other))
        {
            if (debugProjectile)
            {
                Debug.Log($"Damage potion hit obstacle {other.name} - triggering early impact");
            }
            
            targetPosition = transform.position;
            OnReachTarget();
        }
    }
    
    private bool ShouldImpactOnTarget(Collider2D other)
    {
        // Skip the thrower
        if (throwerTransform != null && other.transform == throwerTransform)
        {
            return false;
        }
        
        // Check if it's a loyal villager
        Villager villager = other.GetComponent<Villager>();
        if (villager != null)
        {
            bool isValidTarget = !villager.IsRebel() && IsInLayerMask(other.gameObject.layer, targetLayers);
            return isValidTarget;
        }
        
        // Check if it's the player
        if (other.CompareTag("Player"))
        {
            return IsInLayerMask(other.gameObject.layer, targetLayers);
        }
        
        return false;
    }
    
    private bool ShouldTriggerEarlyImpact(Collider2D other)
    {
        // Don't trigger on targets - they're handled by ShouldImpactOnTarget
        if (other.GetComponent<Villager>() != null) return false;
        if (other.CompareTag("Player")) return false;
        
        // Check if it's an obstacle
        return IsInLayerMask(other.gameObject.layer, impactLayers);
    }
    
    private bool IsInLayerMask(int layer, LayerMask layerMask)
    {
        return (layerMask.value & (1 << layer)) != 0;
    }
    
    // Public configuration methods
    public void SetCanImpactOnTargets(bool canImpact)
    {
        canImpactOnTargets = canImpact;
    }
    
    public void SetFlightDuration(float duration)
    {
        flightDuration = duration;
    }
    
    public void SetArcHeight(float height)
    {
        arcHeight = height;
    }
    
    public void SetDamageColor(Color color)
    {
        damageColor = color;
        
        if (potionSprite != null)
        {
            potionSprite.color = color;
        }
        
        if (trailEffect != null)
        {
            var main = trailEffect.main;
            main.startColor = color;
        }
    }
    
    public void SetImpactRadius(float radius)
    {
        impactRadius = radius;
    }
    
    public void SetTargetLayers(LayerMask layers)
    {
        targetLayers = layers;
    }
    
    // Public getters
    public bool IsFlying() => isFlying;
    public bool CanImpactOnTargets() => canImpactOnTargets;
    public float GetFlightProgress()
    {
        if (!isFlying) return 1f;
        return Mathf.Clamp01((Time.time - flightStartTime) / flightDuration);
    }
    public Vector3 GetTargetPosition() => targetPosition;
    public int GetDamageAmount() => damageAmount;
    
    private void OnDrawGizmos()
    {
        if (!debugProjectile) return;
        
        // Draw impact radius
        Gizmos.color = damageColor;
        Gizmos.DrawWireSphere(transform.position, impactRadius);
        
        // Draw flight path when flying
        if (isFlying && Application.isPlaying)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, targetPosition);
            
            // Draw target position
            Gizmos.DrawWireSphere(targetPosition, 0.3f);
        }
        
        // Draw trajectory preview in editor
        if (!Application.isPlaying && startPosition != Vector3.zero && targetPosition != Vector3.zero)
        {
            Gizmos.color = Color.yellow;
            
            // Draw trajectory arc
            Vector3 prevPos = startPosition;
            int steps = 20;
            
            for (int i = 1; i <= steps; i++)
            {
                float progress = (float)i / steps;
                float curveValue = flightCurve.Evaluate(progress);
                
                Vector3 linearPos = Vector3.Lerp(startPosition, targetPosition, progress);
                float heightOffset = Mathf.Sin(progress * Mathf.PI) * arcHeight * curveValue;
                Vector3 currentPos = linearPos + Vector3.up * heightOffset;
                
                Gizmos.DrawLine(prevPos, currentPos);
                prevPos = currentPos;
            }
        }
        
        // Draw target detection range when flying
        if (isFlying && canImpactOnTargets)
        {
            Gizmos.color = Color.red;
            float colliderRadius = projectileCollider != null ? 
                (projectileCollider is CircleCollider2D circle ? circle.radius : 0.5f) : 0.5f;
            Gizmos.DrawWireSphere(transform.position, colliderRadius);
        }
    }
    
    // Context menu for testing
    [ContextMenu("Test Launch")]
    public void TestLaunch()
    {
        if (Application.isPlaying)
        {
            Vector3 testTarget = transform.position + Vector3.right * 5f;
            Launch(transform.position, testTarget, 15);
        }
    }
    
    [ContextMenu("Toggle Target Impact")]
    public void ToggleTargetImpact()
    {
        SetCanImpactOnTargets(!canImpactOnTargets);
    }
    
    private void OnDestroy()
    {
        // Stop any ongoing effects
        if (trailEffect != null && trailEffect.isPlaying)
        {
            trailEffect.Stop();
        }
    }
}