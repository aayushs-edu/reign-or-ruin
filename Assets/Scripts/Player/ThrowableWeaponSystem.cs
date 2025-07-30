using UnityEngine;
using Cinemachine;
using UnityEngine.Rendering.Universal;

public enum WeaponState
{
    Held,
    Aiming,
    Swinging,
    Thrown,
    Returning,
    Dropped
}

public class ThrowableWeaponSystem : MonoBehaviour
{
    [Header("Weapon References")]
    [SerializeField] private Transform weaponContainer;
    [SerializeField] private Transform weaponTransform;
    [SerializeField] private GameObject weaponSprite;
    [SerializeField] private Vector3 weaponSpriteOffset;
    [SerializeField] private SpriteRenderer weaponRenderer;
    [SerializeField] private Light2D weaponLight;
    [SerializeField] private Collider2D weaponCollider;
    [SerializeField] private ParticleSystem weaponParticles;
    [SerializeField] private bool autoFindParticles = true;
    
    [Header("Throwing Configuration")]
    [SerializeField] private float minThrowSpeed = 8f;
    [SerializeField] private float maxThrowSpeed = 20f;
    [SerializeField] private float returnSpeedMultiplier = 1.5f;
    [SerializeField] private float accuracyRadius = 2f;
    [SerializeField] private LayerMask collisionLayers = -1;
    
    [Header("Charge System")]
    [SerializeField] private float maxCharge = 100f;
    [SerializeField] private float baseChargeRegenRate = 2f;
    [SerializeField] private float powerChargeMultiplier = 0.1f;
    [SerializeField] private float baseFlightChargeDepletion = 10f;
    [SerializeField] private float intensityDepletionMultiplier = 2f;
    [SerializeField] private float minChargeToThrow = 10f;
    
    [Header("Intensity System")]
    [SerializeField] private float maxAimTime = 2f;
    [SerializeField] private float maxIntensityMultiplier = 3f;
    [SerializeField] private float intensityChargeMultiplier = 2f;
    [SerializeField] private int maxPenetration = 5;
    
    [Header("Aiming System")]
    [SerializeField] private float aimTime = 0.5f;
    [SerializeField] private Texture2D crosshairTexture;
    [SerializeField] private CinemachineVirtualCamera playerCamera;
    [SerializeField] private float zoomFOV = 4f;
    [SerializeField] private float normalFOV = 6f;
    [SerializeField] private float cameraTransitionSpeed = 3f;

    [SerializeField] private float chargeRotationSpeed = 90f; // Degrees per second during charge
    [SerializeField] private float maxChargeRotation = 180f;
    
    [Header("Swing Attack")]
    [SerializeField] private float swingDamage = 1f;
    [SerializeField] private float swingRange = 1.5f;
    [SerializeField] private float swingDuration = 0.3f;
    [SerializeField] private float swingAngle = 120f;
    [SerializeField] private float swingCooldown = 0.5f;
    
    [Header("Damage Configuration")]
    [SerializeField] private int baseDamage = 2;
    [SerializeField] private float damageMultiplierPerCharge = 0.02f;
    
    [Header("Visual Effects")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color chargedColor = Color.cyan;
    [SerializeField] private float minLightIntensity = 0f;
    [SerializeField] private float maxLightIntensity = 6f;

    // Add these new fields:
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private float hitEffectMinLifetime = 0f;
    [SerializeField] private float hitEffectMaxLifetime = 3f;
    [SerializeField] private Vector3 hitEffectOffset = Vector3.zero;

    [Header("Particle Effects")]
    [SerializeField] private float minParticleLifetime = 0f;
    [SerializeField] private float maxParticleLifetime = 2f;
    [SerializeField] private bool scaleParticleIntensityWithCharge = true;

    // Add these new fields:
    [SerializeField] private float minEmissionRate = 0f;
    [SerializeField] private float maxEmissionRate = 50f;
    [SerializeField] private float minStartSize = 0.1f;
    [SerializeField] private float maxStartSize = 1f;
    [SerializeField] private AnimationCurve emissionCurve = AnimationCurve.Linear(0, 0, 1, 1);
    [SerializeField] private AnimationCurve sizeCurve = AnimationCurve.Linear(0, 0, 1, 1);

    [Header("Physics Settings")]
    [SerializeField] private LayerMask groundLayers = 1; // What counts as ground
    [SerializeField] private float groundCheckDistance = 0.5f;
    [SerializeField] private float fallGravity = 1f;
    [SerializeField] private float fallDrag = 2f;
    
    [Header("Pickup Settings")]
    [SerializeField] private float pickupRange = 2f;
    [SerializeField] private float pickupChargeRestore = 20f;

    [Header("Audio")]
    [SerializeField] private AudioClip throwSound;
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private AudioClip swingSound;
    [SerializeField] private AudioClip pickupSound;
    [SerializeField] private float volume = 1f;

    // Add these new fields:
    [SerializeField] private AudioClip idleSound; // Looping crackling sound
    [SerializeField] private bool scaleVolumeWithCharge = true;
    [SerializeField] private float minVolume = 0.1f;
    [SerializeField] private AnimationCurve volumeCurve = AnimationCurve.Linear(0, 0, 1, 1);
    [SerializeField] private float idleVolumeMultiplier = 0.5f; // Idle sound is usually quieter

    [Header("Mouse Tracking")]
    [SerializeField] private bool enableMouseTracking = true;

    // State variables
    private AudioSource idleAudioSource;
    private WeaponState currentState = WeaponState.Held;
    private float currentCharge;
    private float aimStartTime;
    private Vector3 throwDirection;
    private Vector3 originalPosition;
    private bool isAiming = false;
    private float currentIntensity = 1f;
    private float targetCameraSize;
    private float thrownIntensity = 1f;
    
    // Swing attack variables
    private bool isSwinging = false;
    private float swingStartTime;
    private float lastSwingTime;
    private float swingStartAngle;
    private bool swingHasHit = false;
    
    // Movement variables
    private Vector3 velocity;
    private float currentSpeed;
    private int remainingPenetration;
    private bool isOnGround = false;
    
    // References
    private PowerSystem powerSystem;
    private PlayerMovement playerMovement;
    private Camera mainCamera;
    private AudioSource audioSource;
    private Transform originalParent;

    // Add to state variables section
    private float currentChargeRotation = 0f;
    
    // Events
    public System.Action<float> OnChargeChanged;
    public System.Action<WeaponState> OnStateChanged;
    public System.Action<int> OnDamageDealt;
    
    private void Start()
    {
        InitializeComponents();
        SetupWeapon();
        currentCharge = maxCharge;
        originalPosition = weaponTransform.localPosition;
        targetCameraSize = normalFOV;
        originalParent = transform.parent;
        
        // Start idle sound if available
        if (idleAudioSource != null && idleSound != null)
        {
            idleAudioSource.Play();
            UpdateIdleSound(); // Set initial volume
        }
    }
    
    private void UpdateIdleSound()
    {
        if (idleAudioSource == null || !scaleVolumeWithCharge) return;
        
        float chargeLevel = currentCharge / maxCharge;
        float targetVolume = 0f;
        
        // Only play idle sound when weapon is held and has some charge
        if (currentState == WeaponState.Held || currentState == WeaponState.Aiming)
        {
            float curveValue = volumeCurve.Evaluate(chargeLevel);
            targetVolume = Mathf.Lerp(minVolume, 1f, curveValue) * idleVolumeMultiplier;
            
            // Extra intensity when aiming
            if (isAiming && currentIntensity > 1f)
            {
                float intensityBoost = (currentIntensity - 1f) / (maxIntensityMultiplier - 1f);
                targetVolume *= (1f + intensityBoost * 0.3f); // 30% louder when charging
            }
        }
        
        idleAudioSource.volume = targetVolume;
        
        // Mute completely at very low charge or when not held
        if (chargeLevel < 0.05f || currentState == WeaponState.Dropped || currentState == WeaponState.Thrown)
        {
            idleAudioSource.volume = 0f;
        }
    }

    private void InitializeComponents()
    {
        powerSystem = PowerSystem.Instance;
        playerMovement = GetComponentInParent<PlayerMovement>();
        mainCamera = Camera.main;
        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // Add idle audio source
        idleAudioSource = transform.Find("IdleAudioSource")?.GetComponent<AudioSource>();
        if (idleAudioSource == null && idleSound != null)
        {
            GameObject idleAudioObj = new GameObject("IdleAudioSource");
            idleAudioObj.transform.SetParent(transform);
            idleAudioObj.transform.localPosition = Vector3.zero;
            idleAudioSource = idleAudioObj.AddComponent<AudioSource>();
            idleAudioSource.loop = true;
            idleAudioSource.playOnAwake = false;
            idleAudioSource.clip = idleSound;
        }

        if (weaponTransform == null) weaponTransform = transform;
        if (weaponRenderer == null) weaponRenderer = GetComponent<SpriteRenderer>();
        if (weaponLight == null) weaponLight = GetComponentInChildren<Light2D>();
        if (weaponCollider == null) weaponCollider = GetComponent<Collider2D>();

        if (autoFindParticles && weaponParticles == null)
        {
            weaponParticles = GetComponentInChildren<ParticleSystem>();
        }

        if (weaponCollider != null)
        {
            weaponCollider.isTrigger = true;
            weaponCollider.enabled = false; // Only enable when thrown or dropped
        }
        
        // Apply sprite offset to position weapon sprite within container
        if (weaponSprite != null)
        {
            weaponSprite.transform.localPosition = weaponSpriteOffset;
        }
    }
    
    private void SetupWeapon()
    {
        UpdateWeaponVisuals();
        UpdateParticleEffects();
        ChangeState(WeaponState.Held);
    }
    
    private void Update()
    {
        HandleInput();
        UpdateCharge();
        UpdateCamera();
        UpdateWeaponBehavior();
        UpdateWeaponVisuals();
        UpdateParticleEffects();
    }
    
    private void SpawnHitEffect(Vector3 position)
    {
        if (hitEffectPrefab == null) return;
        
        // Calculate effect lifetime based on charge
        float chargeLevel = currentCharge / maxCharge;
        float effectLifetime = Mathf.Lerp(hitEffectMinLifetime, hitEffectMaxLifetime, chargeLevel);
        
        // Don't spawn if lifetime would be 0
        if (effectLifetime <= 0) return;
        
        // Spawn the effect
        GameObject effect = Instantiate(hitEffectPrefab, position + hitEffectOffset, Quaternion.identity);
        
        // Scale particle system lifetime if it has one
        ParticleSystem effectParticles = effect.GetComponent<ParticleSystem>();
        if (effectParticles != null)
        {
            var main = effectParticles.main;
            main.duration = effectLifetime;
            main.startLifetime = effectLifetime;
            
            // Optionally scale other properties based on charge
            var emission = effectParticles.emission;
            emission.rateOverTime = emission.rateOverTime.constant * chargeLevel;
            
            // Play the particle system
            effectParticles.Play();
        }
        
        // Destroy after lifetime (add a small buffer for particle fade)
        Destroy(effect, effectLifetime + 0.5f);
    }
    
    private void HandleInput()
    {
        if (currentState == WeaponState.Dropped)
        {
            float distance = Vector3.Distance(transform.position, originalParent.position);
            if (distance <= pickupRange && Input.GetKeyDown(KeyCode.E))
            {
                PickupWeapon();
            }
            return;
        }

        if (currentState == WeaponState.Thrown || currentState == WeaponState.Returning || currentState == WeaponState.Swinging)
        {
            return;
        }

        // Right mouse button for aiming
        if (Input.GetMouseButtonDown(1))
        {
            StartAiming();
        }
        else if (Input.GetMouseButton(1))
        {
            ContinueAiming();
        }
        else if (Input.GetMouseButtonUp(1))
        {
            StopAiming();
        }

        // Left mouse button for throwing/swinging
        if (Input.GetMouseButtonDown(0))
        {
            // Only throw if not over a UI element
            bool pointerOverUI = false;
#if UNITY_STANDALONE || UNITY_EDITOR
            if (UnityEngine.EventSystems.EventSystem.current != null)
            {
                pointerOverUI = UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
            }
#endif
            if (!pointerOverUI && currentCharge >= minChargeToThrow)
            {
                ThrowWeapon();
            }
            else if (!pointerOverUI)
            {
                SwingWeapon();
            }
        }
    }
    
    private void StartAiming()
    {
        if (currentCharge < minChargeToThrow) return;
        
        isAiming = true;
        aimStartTime = Time.time;
        currentIntensity = 1f;
        ChangeState(WeaponState.Aiming);
        
        if (crosshairTexture != null)
        {
            Vector2 hotspot = new Vector2(crosshairTexture.width / 2, crosshairTexture.height / 2);
            Cursor.SetCursor(crosshairTexture, hotspot, CursorMode.Auto);
        }
        
        targetCameraSize = zoomFOV;
    }
    
    private void ContinueAiming()
    {
        if (!isAiming) return;
        
        Vector3 mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0f;
        throwDirection = (mousePos - weaponTransform.position).normalized;
        
        float aimDuration = Time.time - aimStartTime;
        currentIntensity = 1f + Mathf.Clamp01(aimDuration / maxAimTime) * (maxIntensityMultiplier - 1f);
    }

    private void StopAiming()
    {
        isAiming = false;
        currentIntensity = 1f;

        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        targetCameraSize = normalFOV;
        
        ChangeState(WeaponState.Held);
    }
    
    private void ThrowWeapon()
    {
        if (currentCharge < minChargeToThrow) return;
        
        float accuracyMultiplier = 1f;
        float throwIntensity = 1f;
        
        if (isAiming)
        {
            float aimDuration = Time.time - aimStartTime;
            accuracyMultiplier = Mathf.Clamp01(aimDuration / aimTime);
            throwIntensity = currentIntensity;
        }
        else
        {
            Vector3 mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            mousePos.z = 0f;
            throwDirection = (mousePos - weaponTransform.position).normalized;
            accuracyMultiplier = 0.3f;
            throwIntensity = 1f;
        }
        
        thrownIntensity = throwIntensity;
        
        if (isAiming)
        {
            StopAiming();
        }
        
        if (accuracyMultiplier < 1f)
        {
            float inaccuracy = (1f - accuracyMultiplier) * accuracyRadius;
            Vector2 randomOffset = Random.insideUnitCircle * inaccuracy;
            Vector3 aimPoint = weaponTransform.position + throwDirection * 10f;
            aimPoint += (Vector3)randomOffset;
            throwDirection = (aimPoint - weaponTransform.position).normalized;
        }
        
        float chargeMultiplier = currentCharge / maxCharge;
        float speedMultiplier = Mathf.Lerp(0.5f, 1f, accuracyMultiplier) * throwIntensity;
        currentSpeed = Mathf.Lerp(minThrowSpeed, maxThrowSpeed, chargeMultiplier * speedMultiplier);
        
        remainingPenetration = Mathf.RoundToInt((throwIntensity - 1f) * maxPenetration / (maxIntensityMultiplier - 1f));
        
        velocity = throwDirection * currentSpeed;
        
        ChangeState(WeaponState.Thrown);
        
        if (weaponCollider != null)
        {
            weaponCollider.enabled = true;
        }
        
        PlaySound(throwSound);
        
        Debug.Log($"Weapon thrown! Intensity: {throwIntensity:F1}x, Penetration: {remainingPenetration}");
    }
    
    private void SwingWeapon()
    {
        if (Time.time - lastSwingTime < swingCooldown) return;
        if (isAiming) return;
        
        isSwinging = true;
        swingStartTime = Time.time;
        lastSwingTime = Time.time;
        swingHasHit = false;
        
        Vector3 mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0f;
        Vector3 direction = (mousePos - weaponTransform.position).normalized;
        swingStartAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - (swingAngle / 2f);
        
        ChangeState(WeaponState.Swinging);
        PlaySound(swingSound);
        
        Debug.Log("Weapon swing started!");
    }
    
    private void UpdateCamera()
    {
        if (playerCamera == null) return;
        
        float currentSize = playerCamera.m_Lens.OrthographicSize;
        float newSize = Mathf.Lerp(currentSize, targetCameraSize, cameraTransitionSpeed * Time.deltaTime);
        playerCamera.m_Lens.OrthographicSize = newSize;
    }
    
    private void UpdateCharge()
    {
        if (powerSystem == null) return;
        
        PowerHolder playerPower = powerSystem.GetPlayerPower();
        if (playerPower == null) return;
        
        if (currentState == WeaponState.Thrown || currentState == WeaponState.Returning)
        {
            float depletionRate = baseFlightChargeDepletion * thrownIntensity * intensityDepletionMultiplier;
            currentCharge = Mathf.Max(0f, currentCharge - depletionRate * Time.deltaTime);
            
            if (currentCharge <= 0f)
            {
                DropWeapon();
            }
        }
        else if (currentState != WeaponState.Dropped)
        {
            float regenRate = baseChargeRegenRate + (playerPower.currentPower * powerChargeMultiplier);
            currentCharge = Mathf.Min(maxCharge, currentCharge + regenRate * Time.deltaTime);
        }
        
        OnChargeChanged?.Invoke(currentCharge / maxCharge);
        
        // Update idle sound volume
        UpdateIdleSound();
    }
        
    private void UpdateWeaponBehavior()
    {
        switch (currentState)
        {
            case WeaponState.Held:
                UpdateHeldBehavior();
                break;
            case WeaponState.Aiming:
                UpdateAimingBehavior();
                break;
            case WeaponState.Swinging:
                UpdateSwingingBehavior();
                break;
            case WeaponState.Thrown:
                UpdateThrownBehavior();
                break;
            case WeaponState.Returning:
                UpdateReturningBehavior();
                break;
            case WeaponState.Dropped:
                UpdateDroppedBehavior();
                break;
        }
    }
    
    private void UpdateHeldBehavior()
    {
        // Reset charge rotation when not aiming
        if (currentChargeRotation != 0f)
        {
            currentChargeRotation = Mathf.MoveTowards(currentChargeRotation, 0f, chargeRotationSpeed * 2f * Time.deltaTime);
            if (weaponSprite != null)
            {
                weaponSprite.transform.localRotation = Quaternion.Euler(0, 0, currentChargeRotation);
            }
        }

        // Mouse tracking when enabled and not moving
        if (enableMouseTracking)
        {
            UpdateMouseRotation();
        }
        else
        {
            // Return to neutral position when not moving and not tracking
            weaponContainer.localRotation = Quaternion.Lerp(weaponContainer.localRotation, Quaternion.identity, Time.deltaTime * 5f);
        }
    }

    private void UpdateMouseRotation()
    {
        if (mainCamera == null) return;
        
        Vector3 mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0f;
        
        Vector2 direction = (mousePos - weaponContainer.position).normalized;
        weaponContainer.right = direction;
        
        // Handle sprite flipping like the villagers do
        Vector2 scale = weaponContainer.localScale;
        if (direction.x < 0)
        {
            scale.y = -1;
        }
        else
        {
            scale.y = 1;
        }
        weaponContainer.localScale = scale;
    
    }

    // private void ReturnToIdleRotation()
    // {
    //     weaponTransform.localScale = Vector3.one;
    //     weaponTransform.localRotation = Quaternion.Lerp(weaponTransform.localRotation, Quaternion.identity, 5 * Time.deltaTime);
    // }

    private bool IsPlayerMoving()
    {
        return playerMovement != null && playerMovement.IsMoving();
    }
    
    private void UpdateAimingBehavior()
    {
        if (throwDirection != Vector3.zero)
        {
            // Rotate container to face throw direction
            float angle = Mathf.Atan2(throwDirection.y, throwDirection.x) * Mathf.Rad2Deg;
            weaponContainer.rotation = Quaternion.Euler(0, 0, angle);
            
            // Add charging rotation to weapon sprite
            if (weaponSprite != null)
            {
                // Calculate charge rotation based on intensity
                float targetChargeRotation = (currentIntensity - 1f) / (maxIntensityMultiplier - 1f) * maxChargeRotation;
                currentChargeRotation = Mathf.MoveTowards(currentChargeRotation, targetChargeRotation, chargeRotationSpeed * Time.deltaTime);
                
                // Apply both the sprite offset and the charge rotation
                weaponSprite.transform.localPosition = weaponSpriteOffset;
                weaponSprite.transform.localRotation = Quaternion.Euler(0, 0, currentChargeRotation);
            }
        }
    }
    
    private void UpdateSwingingBehavior()
    {
        float swingProgress = (Time.time - swingStartTime) / swingDuration;
        
        if (swingProgress >= 1f)
        {
            isSwinging = false;
            ChangeState(WeaponState.Held);
            return;
        }
        
        float currentSwingAngle = swingStartAngle + (swingAngle * swingProgress);
        weaponTransform.rotation = Quaternion.Euler(0, 0, currentSwingAngle);
        
        if (!swingHasHit && swingProgress > 0.3f && swingProgress < 0.7f)
        {
            CheckSwingDamage();
        }
    }
    
    private void UpdateThrownBehavior()
    {
        weaponTransform.position += velocity * Time.deltaTime;
        weaponTransform.Rotate(0, 0, 720f * Time.deltaTime);
        
        float distanceFromPlayer = Vector3.Distance(weaponTransform.position, originalParent.position);
        if (distanceFromPlayer > 20f)
        {
            StartReturn();
        }
    }
    
    private void UpdateReturningBehavior()
    {
        Vector3 playerPos = originalParent.position;
        Vector3 direction = (playerPos - weaponTransform.position).normalized;
        float returnSpeed = currentSpeed * returnSpeedMultiplier;
        
        weaponTransform.position += direction * returnSpeed * Time.deltaTime;
        weaponTransform.Rotate(0, 0, 1080f * Time.deltaTime);
        
        float distance = Vector3.Distance(weaponTransform.position, playerPos);
        if (distance < 0.5f)
        {
            ReturnToPlayer();
        }
    }
    
    private void UpdateDroppedBehavior()
    {
        // Check if weapon has landed on ground if it was falling
        Rigidbody2D rb = weaponTransform.GetComponent<Rigidbody2D>();
        if (rb != null && rb.velocity.magnitude < 0.1f)
        {
            CheckGroundStatus();
            if (isOnGround)
            {
                // Weapon has landed - remove physics
                Destroy(rb);
                Debug.Log("Weapon has landed on ground!");
            }
        }
    }
    
    private void CheckSwingDamage()
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(weaponTransform.position, swingRange, collisionLayers);
        
        foreach (Collider2D collider in colliders)
        {
            Vector3 directionToTarget = (collider.transform.position - weaponTransform.position).normalized;
            float angleToTarget = Mathf.Atan2(directionToTarget.y, directionToTarget.x) * Mathf.Rad2Deg;
            
            float normalizedSwingStart = swingStartAngle;
            float normalizedSwingEnd = swingStartAngle + swingAngle;
            float normalizedTargetAngle = angleToTarget;
            
            while (normalizedTargetAngle < normalizedSwingStart) normalizedTargetAngle += 360f;
            while (normalizedTargetAngle > normalizedSwingStart + 360f) normalizedTargetAngle -= 360f;
            
            if (normalizedTargetAngle >= normalizedSwingStart && normalizedTargetAngle <= normalizedSwingEnd)
            {
                int damage = Mathf.RoundToInt(swingDamage);
                bool hitSomething = false;
                
                // Check for enemy
                EnemyHealth enemyHealth = collider.GetComponent<EnemyHealth>();
                if (enemyHealth != null)
                {
                    enemyHealth.TakeDamage(damage);
                    OnDamageDealt?.Invoke(damage);
                    PlaySound(hitSound);
                    hitSomething = true;
                    Debug.Log($"Swing hit enemy {collider.name} for {damage} damage!");
                }
                
                // Check for villager health component (alternative)
                if (!hitSomething)
                {
                    VillagerHealth villagerHealth = collider.GetComponent<VillagerHealth>();
                    if (villagerHealth != null)
                    {
                        villagerHealth.TakeDamage(damage);
                        OnDamageDealt?.Invoke(damage);
                        PlaySound(hitSound);
                        hitSomething = true;
                        Debug.Log($"WARNING: Swing hit villager {collider.name} for {damage} damage! (Friendly Fire)");
                    }
                }
                
                if (hitSomething)
                {
                    swingHasHit = true;
                    break;
                }
            }
        }
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (currentState == WeaponState.Thrown)
        {
            // When thrown, check collision with all collision layers
            if (IsInLayerMask(other.gameObject.layer, collisionLayers))
            {
                HandleWeaponCollision(other);
            }
        }
        else if (currentState == WeaponState.Dropped)
        {
            // When dropped, check for ground triggers
            if (IsInLayerMask(other.gameObject.layer, groundLayers))
            {
                OnGroundTriggerHit();
            }
        }
    }

    private bool IsInLayerMask(int layer, LayerMask layerMask)
    {
        return (layerMask.value & (1 << layer)) != 0;
    }
    
    private void OnGroundTriggerHit()
    {
        // Weapon has hit ground trigger while dropped
        Rigidbody2D rb = weaponTransform.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            // Stop the weapon and remove physics
            rb.velocity = Vector2.zero;
            Destroy(rb);
            isOnGround = true;
            Debug.Log("Weapon landed on ground trigger!");
        }
    }
    
    private void HandleWeaponCollision(Collider2D hitCollider)
    {
        // Spawn hit effect at collision point
        SpawnHitEffect(hitCollider.ClosestPoint(transform.position));
        
        float chargeMultiplier = currentCharge / maxCharge;
        int damage = Mathf.RoundToInt(baseDamage * (1f + chargeMultiplier) * thrownIntensity);
        
        bool hitSomething = false;
        
        // Check for enemy
        EnemyHealth enemyHealth = hitCollider.GetComponent<EnemyHealth>();
        if (enemyHealth != null)
        {
            enemyHealth.TakeDamage(damage);
            OnDamageDealt?.Invoke(damage);
            PlaySound(hitSound);
            hitSomething = true;
            
            Debug.Log($"Weapon hit enemy {hitCollider.name} for {damage} damage! Penetration remaining: {remainingPenetration}");
            
            if (remainingPenetration > 0)
            {
                remainingPenetration--;
                currentSpeed *= 0.9f;
                velocity = velocity.normalized * currentSpeed;
                
                Debug.Log($"Weapon penetrated! Continuing with {remainingPenetration} penetrations left.");
                return;
            }
        }
        
        // Check for villager health component - PASS PLAYER AS DAMAGE SOURCE
        VillagerHealth villagerHealth = hitCollider.GetComponent<VillagerHealth>();
        if (villagerHealth != null)
        {
            // Find the player GameObject to pass as damage source
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            villagerHealth.TakeDamage(damage, player); // Pass player as damage source
            OnDamageDealt?.Invoke(damage);
            PlaySound(hitSound);
            hitSomething = true;
            
            Debug.Log($"Weapon hit villager {hitCollider.name} for {damage} damage! Starting return.");
            
            // All villagers (including rebels) should trigger weapon return
            StartReturn();
            return;
        }
        
        // Collision with anything else in collision layers triggers return
        if (hitSomething || IsInLayerMask(hitCollider.gameObject.layer, collisionLayers))
        {
            StartReturn();
        }
    }
    
    private void StartReturn()
    {
        if (currentCharge > 0f)
        {
            ChangeState(WeaponState.Returning);
        }
        else
        {
            DropWeapon();
        }
    }
    
    private void ReturnToPlayer()
    {
        // Re-parent the weapon to the player
        transform.SetParent(originalParent);
        
        ChangeState(WeaponState.Held);
        weaponContainer.localRotation = Quaternion.identity;
        currentChargeRotation = 0f; // Reset charge rotation
        
        // Reset weapon sprite rotation and position
        if (weaponSprite != null)
        {
            weaponSprite.transform.localPosition = weaponSpriteOffset;
            weaponSprite.transform.localRotation = Quaternion.identity;
        }
        
        velocity = Vector3.zero;
        remainingPenetration = 0;
        thrownIntensity = 1f;
        
        // Disable collider when weapon is held
        if (weaponCollider != null)
        {
            weaponCollider.enabled = false;
        }
        
        // Play pickup sound when weapon returns
        PlaySound(pickupSound);
    }
    
    private void DropWeapon()
    {
        // Unparent the weapon from the player
        transform.SetParent(null);

        ChangeState(WeaponState.Dropped);
        velocity = Vector3.zero;
        thrownIntensity = 1f;

        // Keep collider enabled for trigger detection with ground
        if (weaponCollider != null)
        {
            weaponCollider.enabled = true; // Keep enabled for ground trigger detection
        }

        // Check if weapon is on ground before applying physics
        CheckGroundStatus();

        if (!isOnGround)
        {
            // Weapon is in space - add physics to make it fall
            Rigidbody2D rb = weaponTransform.GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = weaponTransform.gameObject.AddComponent<Rigidbody2D>();
            }

            rb.gravityScale = fallGravity;
            rb.drag = fallDrag;

            Debug.Log("Weapon dropped in space - falling to ground!");
        }
        else
        {
            // Weapon is already on ground - no physics needed
            Debug.Log("Weapon dropped on ground - staying in place!");
        }
    }
    
    private void PickupWeapon()
    {
        // Remove any physics components
        Rigidbody2D rb = weaponTransform.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            Destroy(rb);
        }
        
        // Re-parent the weapon to the player
        transform.SetParent(originalParent);
        
        // Restore some charge
        currentCharge = Mathf.Min(maxCharge, currentCharge + pickupChargeRestore);
        isOnGround = false; // Reset ground status
        
        // Return to held state
        ReturnToPlayer();
        
        PlaySound(pickupSound);
        Debug.Log("Weapon picked up!");
    }
    
    private void UpdateWeaponVisuals()
    {
        if (weaponRenderer == null) return;
        
        float chargeLevel = currentCharge / maxCharge;
        
        Color baseColor = Color.Lerp(normalColor, chargedColor, chargeLevel);
        
        if (isAiming && currentIntensity > 1f)
        {
            float intensityGlow = (currentIntensity - 1f) / (maxIntensityMultiplier - 1f);
            baseColor = Color.Lerp(baseColor, Color.white, intensityGlow * 0.5f);
        }
        
        weaponRenderer.color = baseColor;
        
        if (weaponLight != null)
        {
            float lightIntensity = Mathf.Lerp(minLightIntensity, maxLightIntensity, chargeLevel);
            
            if (isAiming)
            {
                lightIntensity *= currentIntensity;
            }
            
            weaponLight.intensity = lightIntensity;
            weaponLight.color = Color.Lerp(Color.white, chargedColor, chargeLevel);
            
            if (chargeLevel < 0.2f)
            {
                float flicker = Mathf.Sin(Time.time * 8f) * 0.3f + 0.7f;
                weaponLight.intensity *= flicker;
            }
        }
    }
    
    private void UpdateParticleEffects()
    {
        if (weaponParticles == null) return;
        
        float chargeLevel = currentCharge / maxCharge;
        
        var main = weaponParticles.main;
        var emission = weaponParticles.emission;
        
        // Update lifetime
        float targetLifetime = Mathf.Lerp(minParticleLifetime, maxParticleLifetime, chargeLevel);
        main.startLifetime = targetLifetime;
        
        if (scaleParticleIntensityWithCharge)
        {
            // Update emission rate based on charge
            float emissionMultiplier = emissionCurve.Evaluate(chargeLevel);
            float targetEmissionRate = Mathf.Lerp(minEmissionRate, maxEmissionRate, emissionMultiplier);
            emission.rateOverTime = targetEmissionRate;
            
            // Update start size based on charge
            float sizeMultiplier = sizeCurve.Evaluate(chargeLevel);
            float targetMinSize = Mathf.Lerp(minStartSize, maxStartSize, sizeMultiplier) * 0.8f; // 80% for min
            float targetMaxSize = Mathf.Lerp(minStartSize, maxStartSize, sizeMultiplier);
            
            // Set random between two constants for size
            var startSize = main.startSize;
            startSize.mode = ParticleSystemCurveMode.TwoConstants;
            startSize.constantMin = targetMinSize;
            startSize.constantMax = targetMaxSize;
            main.startSize = startSize;
            
            // Additional intensity boost when aiming
            if (isAiming && currentIntensity > 1f)
            {
                float intensityBoost = currentIntensity / maxIntensityMultiplier;
                emission.rateOverTime = targetEmissionRate * (1f + intensityBoost);
                
                // Also boost size slightly when aiming
                startSize.constantMin = targetMinSize * (1f + intensityBoost * 0.2f);
                startSize.constantMax = targetMaxSize * (1f + intensityBoost * 0.2f);
                main.startSize = startSize;
            }
        }
        
        // Enable/disable particles based on state
        if (currentState == WeaponState.Dropped || chargeLevel < 0.05f)
        {
            if (weaponParticles.isPlaying)
            {
                weaponParticles.Stop();
            }
        }
        else
        {
            if (!weaponParticles.isPlaying)
            {
                weaponParticles.Play();
            }
        }
    }
    
    public void SetParticleEmissionRange(float minRate, float maxRate)
    {
        minEmissionRate = minRate;
        maxEmissionRate = maxRate;
        UpdateParticleEffects();
    }

    public void SetParticleSizeRange(float minSize, float maxSize)
    {
        minStartSize = minSize;
        maxStartSize = maxSize;
        UpdateParticleEffects();
    }

    public void SetParticleCurves(AnimationCurve emission, AnimationCurve size)
    {
        emissionCurve = emission;
        sizeCurve = size;
        UpdateParticleEffects();
    }
        
    private void CheckGroundStatus()
    {
        // Check if weapon is touching ground using raycast downward
        Vector3 weaponPosition = weaponTransform.position;
        RaycastHit2D groundHit = Physics2D.Raycast(
            weaponPosition,
            Vector2.down,
            groundCheckDistance,
            groundLayers
        );

        isOnGround = groundHit.collider != null;

        // Also check for overlapping ground colliders (in case weapon is inside ground)
        if (!isOnGround)
        {
            Collider2D groundOverlap = Physics2D.OverlapCircle(
                weaponPosition,
                0.1f,
                groundLayers
            );
            isOnGround = groundOverlap != null;
        }
    }
    
    private void ChangeState(WeaponState newState)
    {
        if (currentState == newState) return;
        
        WeaponState oldState = currentState;
        currentState = newState;
        
        OnStateChanged?.Invoke(newState);
        
        // Update idle sound based on state
        UpdateIdleSound();
        
        // Stop idle sound completely when dropped
        if (newState == WeaponState.Dropped && idleAudioSource != null)
        {
            idleAudioSource.Stop();
        }
        else if (oldState == WeaponState.Dropped && newState == WeaponState.Held && idleAudioSource != null && idleSound != null)
        {
            idleAudioSource.Play();
        }
        
        Debug.Log($"Weapon state changed: {oldState} → {newState}");
    }
    
    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip, volume);
        }
    }
    
    // Public getters
    public float GetChargeLevel() => currentCharge / maxCharge;
    public WeaponState GetCurrentState() => currentState;
    public bool CanThrow() => currentCharge >= minChargeToThrow && (currentState == WeaponState.Held || currentState == WeaponState.Aiming);
    public int GetCurrentDamage() => Mathf.RoundToInt(baseDamage + (baseDamage * damageMultiplierPerCharge * currentCharge));
    public bool IsDropped() => currentState == WeaponState.Dropped;
    public float GetPickupRange() => pickupRange;
    
    // Public setters
    public void SetParticleLifetimeRange(float minLifetime, float maxLifetime)
    {
        minParticleLifetime = minLifetime;
        maxParticleLifetime = maxLifetime;
        UpdateParticleEffects();
    }
    
    public void SetParticleIntensityScaling(bool enableScaling)
    {
        scaleParticleIntensityWithCharge = enableScaling;
    }
    
    private void OnDrawGizmosSelected()
    {
        if (currentState == WeaponState.Aiming && throwDirection != Vector3.zero)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(weaponTransform.position, throwDirection * 5f);
        }
        
        if (currentState == WeaponState.Dropped)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(weaponTransform.position, pickupRange);
        }
        
        if (currentState == WeaponState.Swinging)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(weaponTransform.position, swingRange);
        }
        
        // Draw ground check ray when dropped
        if (currentState == WeaponState.Dropped)
        {
            Gizmos.color = isOnGround ? Color.green : Color.red;
            Gizmos.DrawRay(weaponTransform.position, Vector2.down * groundCheckDistance);
        }
        
        // Draw pickup range when held (for reference)
        if (currentState == WeaponState.Held)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(weaponTransform.position, pickupRange);
        }
    }
}