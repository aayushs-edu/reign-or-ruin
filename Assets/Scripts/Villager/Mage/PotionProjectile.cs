using UnityEngine;
using System.Collections;

public class PotionProjectile : MonoBehaviour
{
    [Header("Projectile Settings")]
    [SerializeField] private float flightDuration = 1.5f;
    [SerializeField] private float arcHeight = 3f;
    [SerializeField] private AnimationCurve flightCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private bool rotateInFlight = true;
    [SerializeField] private float rotationSpeed = 360f; // Degrees per second
    
    [Header("Impact Settings")]
    [SerializeField] private GameObject impactEffectPrefab;
    [SerializeField] private LayerMask impactLayers = -1;
    [SerializeField] private float impactRadius = 1f; // Radius to heal nearby villagers
    [SerializeField] private bool destroyOnImpact = true;
    [SerializeField] private float destroyDelay = 0.1f; // Small delay to allow particle effects
    
    [Header("Visual Settings")]
    [SerializeField] private SpriteRenderer potionSprite;
    [SerializeField] private ParticleSystem trailEffect;
    [SerializeField] private Color healingColor = Color.green;
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
    private int healAmount;
    private bool isFlying = false;
    private float flightStartTime;
    private AnimationCurve customFlightCurve;
    
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
        
        // Set healing color
        if (potionSprite != null)
        {
            potionSprite.color = healingColor;
        }
        
        // Configure trail effect
        if (trailEffect != null)
        {
            var main = trailEffect.main;
            main.startColor = healingColor;
        }
        
        // Configure audio source
        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f; // 3D sound
        }
    }
    
    public void Launch(Vector3 start, Vector3 target, int healing, AnimationCurve curve = null)
    {
        startPosition = start;
        targetPosition = target;
        healAmount = healing;
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
            Debug.Log($"Potion launched from {start} to {target} with {healing} healing");
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
                // Reached target
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
        
        // Fallback - should have reached target by now
        if (isFlying)
        {
            OnReachTarget();
        }
    }
    
    private void UpdatePosition(float progress)
    {
        // Use custom curve if provided, otherwise use default
        float curveValue = customFlightCurve.Evaluate(progress);
        
        // Linear interpolation between start and target
        Vector3 linearPosition = Vector3.Lerp(startPosition, targetPosition, progress);
        
        // Add arc height using sine curve
        float heightOffset = Mathf.Sin(progress * Mathf.PI) * arcHeight;
        
        // Apply curve to the height for more interesting trajectories
        heightOffset *= curveValue;
        
        // Set final position
        transform.position = linearPosition + Vector3.up * heightOffset;
    }
    
    private void OnReachTarget()
    {
        if (!isFlying) return; // Prevent double-execution
        
        isFlying = false;
        
        // Stop trail effect
        if (trailEffect != null)
        {
            trailEffect.Stop();
        }
        
        // Apply healing effect
        ApplyHealingEffect();
        
        // Spawn impact effect
        SpawnImpactEffect();
        
        // Play impact sound
        PlaySound(impactSound);
        
        if (debugProjectile)
        {
            Debug.Log($"Potion reached target at {transform.position}, applying {healAmount} healing");
        }
        
        // Destroy projectile
        if (destroyOnImpact)
        {
            Destroy(gameObject, destroyDelay);
        }
    }
    
    private void ApplyHealingEffect()
    {
        // Find all villagers within impact radius
        Collider2D[] nearbyColliders = Physics2D.OverlapCircleAll(transform.position, impactRadius, impactLayers);
        
        int villagersHealed = 0;
        
        foreach (var collider in nearbyColliders)
        {
            // Look for villager health component
            VillagerHealth villagerHealth = collider.GetComponent<VillagerHealth>();
            if (villagerHealth != null)
            {
                // Apply healing
                villagerHealth.Heal(healAmount);
                villagersHealed++;
                
                if (debugProjectile)
                {
                    Debug.Log($"Potion healed {collider.name} for {healAmount} HP");
                }
            }
            else
            {
                // Also check for player health (in case mage heals player)
                PlayerHealth playerHealth = collider.GetComponent<PlayerHealth>();
                if (playerHealth != null)
                {
                    playerHealth.Heal(healAmount);
                    villagersHealed++;
                    
                    if (debugProjectile)
                    {
                        Debug.Log($"Potion healed player for {healAmount} HP");
                    }
                }
            }
        }
        
        if (debugProjectile)
        {
            Debug.Log($"Potion healing effect applied to {villagersHealed} targets");
        }
    }
    
    private void SpawnImpactEffect()
    {
        if (impactEffectPrefab != null)
        {
            GameObject effect = Instantiate(impactEffectPrefab, transform.position, Quaternion.identity);
            
            // Auto-destroy the effect after a reasonable time
            Destroy(effect, 3f);
            
            if (debugProjectile)
            {
                Debug.Log($"Spawned impact effect at {transform.position}");
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
        // Trigger early impact if hitting something solid (ground, walls, etc.)
        if (!isFlying) return;
        
        // Check if we hit something that should stop the projectile
        if (ShouldTriggerEarlyImpact(other))
        {
            // Adjust target position to current position for immediate impact
            targetPosition = transform.position;
            OnReachTarget();
        }
    }
    
    private bool ShouldTriggerEarlyImpact(Collider2D other)
    {
        // Don't trigger on villagers (potion passes through them)
        if (other.GetComponent<Villager>() != null) return false;
        if (other.CompareTag("Player")) return false;
        
        // Check layer mask
        return IsInLayerMask(other.gameObject.layer, impactLayers);
    }
    
    private bool IsInLayerMask(int layer, LayerMask layerMask)
    {
        return (layerMask.value & (1 << layer)) != 0;
    }
    
    // Public methods for configuration
    public void SetFlightDuration(float duration)
    {
        flightDuration = duration;
    }
    
    public void SetArcHeight(float height)
    {
        arcHeight = height;
    }
    
    public void SetHealingColor(Color color)
    {
        healingColor = color;
        
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
    
    // Public getters
    public bool IsFlying() => isFlying;
    public float GetFlightProgress()
    {
        if (!isFlying) return 1f;
        return Mathf.Clamp01((Time.time - flightStartTime) / flightDuration);
    }
    public Vector3 GetTargetPosition() => targetPosition;
    public int GetHealAmount() => healAmount;
    
    private void OnDrawGizmos()
    {
        if (!debugProjectile) return;
        
        // Draw impact radius
        Gizmos.color = healingColor;
        Gizmos.DrawWireSphere(transform.position, impactRadius);
        
        // Draw flight path when flying
        if (isFlying && Application.isPlaying)
        {
            Gizmos.color = Color.green;
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
    }
    
    // Context menu for testing
    [ContextMenu("Test Launch")]
    public void TestLaunch()
    {
        if (Application.isPlaying)
        {
            Vector3 testTarget = transform.position + Vector3.right * 5f;
            Launch(transform.position, testTarget, 10);
        }
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