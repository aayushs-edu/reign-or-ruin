using UnityEngine;
using Cinemachine;
using UnityEngine.Rendering.Universal;
using System.Collections;
using System.Collections.Generic;

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
    [SerializeField] private float cameraLookaheadDistance = 3f; // How far ahead to look
    [SerializeField] private float lookaheadTransitionSpeed = 2f; // How fast to transition lookahead

    [SerializeField] private float chargeRotationSpeed = 90f; // Degrees per second during charge
    [SerializeField] private float maxChargeRotation = 180f;
    
    [Header("Animation-Driven Melee")]
    [SerializeField] private Animator weaponAnimator; // Animator for melee swing animation
    [SerializeField] private string meleeAttackTrigger = "MeleeAttack"; // Animation trigger name
    [SerializeField] private float meleeAnimationDuration = 0.5f; // Duration of melee animation
    [SerializeField] private bool debugMeleeAnimation = false;
    
    [Header("Swing Attack")]
    [SerializeField] private float swingDamage = 15f;
    [SerializeField] private float swingRange = 2f;
    [SerializeField] private float swingAngle = 120f;
    [SerializeField] private float swingDuration = 0.5f;
    [SerializeField] private float swingCooldown = 1f;
    
    [Header("Mouse Control")]
    [SerializeField] private bool enableMouseTracking = true;
    
    [Header("Visual Effects")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color chargedColor = Color.cyan;
    [SerializeField] private float minLightIntensity = 0.3f;
    [SerializeField] private float maxLightIntensity = 1.5f;
    
    [Header("Particle Configuration")]
    [SerializeField] private float minParticleLifetime = 0.5f;
    [SerializeField] private float maxParticleLifetime = 2f;
    [SerializeField] private bool scaleParticleIntensityWithCharge = true;
    [SerializeField] private AnimationCurve emissionCurve = AnimationCurve.Linear(0, 0.2f, 1, 1f);
    [SerializeField] private float minEmissionRate = 5f;
    [SerializeField] private float maxEmissionRate = 30f;
    
    [Header("Hit Effects")]
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private Vector3 hitEffectOffset = Vector3.zero;
    [SerializeField] private float hitEffectMinLifetime = 0.5f;
    [SerializeField] private float hitEffectMaxLifetime = 2f;
    
    [Header("Damage System")]
    [SerializeField] private int baseDamage = 25;
    [SerializeField] private float damageMultiplierPerCharge = 0.02f;
    
    [Header("Ground Detection")]
    [SerializeField] private LayerMask groundLayers = 1;
    [SerializeField] private float groundCheckDistance = 0.5f;
    [SerializeField] private float fallGravity = 2f;
    [SerializeField] private float fallDrag = 1f;
    
    [Header("Pickup System")]
    [SerializeField] private float pickupRange = 2f;
    [SerializeField] private float pickupChargeRestore = 30f;
    
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip throwSound;
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private AudioClip returnSound;
    [SerializeField] private AudioClip swingSound;
    [SerializeField] private AudioClip pickupSound;
    [SerializeField] private float volume = 1f;
    
    [Header("Debug")]
    [SerializeField] private bool debugCollision = false;
    [SerializeField] private bool debugCharge = false;
    [SerializeField] private bool debugMovement = false;
    
    // Core state
    private WeaponState currentState = WeaponState.Held;
    private Transform originalParent;
    private Camera mainCamera;
    private PowerSystem powerSystem;
    
    // Throwing state
    private Vector3 velocity;
    private Vector3 throwDirection;
    private float currentSpeed;
    private int remainingPenetration;
    private float thrownIntensity = 1f;
    private bool isOnGround = false;
    
    // Aiming state
    private bool isAiming = false;
    private float aimStartTime;
    private float currentIntensity = 1f;
    private float targetCameraSize;
    private Vector3 originalCameraOffset;
    private bool hasStoredOriginalOffset = false;
    
    // Charge state
    private float currentCharge = 100f;
    private float currentChargeRotation = 0f;
    
    // Animation-driven melee state
    private bool isMeleeAttacking = false;
    private HashSet<GameObject> meleeHitTargets = new HashSet<GameObject>(); // Track hit targets per swing
    
    // Legacy swing state (kept for compatibility)
    private bool isSwinging = false;
    private float swingStartTime;
    private float swingStartAngle;
    private float lastSwingTime;
    private bool swingHasHit = false;
    
    // Events
    public System.Action<float> OnChargeChanged;
    public System.Action<int> OnDamageDealt;
    
    private void Awake()
    {
        originalParent = transform.parent;
        mainCamera = Camera.main;
        powerSystem = PowerSystem.Instance;
        targetCameraSize = normalFOV;
        
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
    
    private void Start()
    {
        SetupWeapon();
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
            if (!pointerOverUI)
            {
                if (isAiming && currentCharge >= minChargeToThrow)
                {
                    ThrowWeapon();
                }
                else
                {
                    SwingWeapon();
                }
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
        
        // Store original camera offset if not already stored
        if (playerCamera != null && !hasStoredOriginalOffset)
        {
            originalCameraOffset = playerCamera.GetCinemachineComponent<CinemachineFramingTransposer>().m_TrackedObjectOffset;
            hasStoredOriginalOffset = true;
        }
        
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
        
        // Return camera to original offset
        if (playerCamera != null && hasStoredOriginalOffset)
        {
            var transposer = playerCamera.GetCinemachineComponent<CinemachineTransposer>();
            if (transposer != null)
            {
                transposer.m_FollowOffset = originalCameraOffset;
            }
        }
        
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
        float playerPower = 1f;
        if (powerSystem != null && powerSystem.GetPlayerPower() != null)
            playerPower = Mathf.Max(1f, powerSystem.GetPlayerPower().currentPower);
        // Scale throw speed and penetration with player power
        currentSpeed = Mathf.Lerp(minThrowSpeed, maxThrowSpeed, chargeMultiplier * speedMultiplier) * (1f + 0.1f * playerPower);
        remainingPenetration = Mathf.RoundToInt(((throwIntensity - 1f) * maxPenetration / (maxIntensityMultiplier - 1f)) * (1f + 0.1f * playerPower));
        velocity = throwDirection * currentSpeed;
        
        ChangeState(WeaponState.Thrown);
        
        if (weaponCollider != null)
        {
            weaponCollider.enabled = true;
        }
        
        PlaySound(throwSound);
        
        Debug.Log($"Weapon thrown! Speed: {currentSpeed:F1}, Intensity: {throwIntensity:F1}x, Penetration: {remainingPenetration}");
    }
    
    // Modified SwingWeapon method - now uses animation instead of script-driven rotation
    private void SwingWeapon()
    {
        if (Time.time - lastSwingTime < swingCooldown) return;
        if (isAiming || isMeleeAttacking) return;
        
        isMeleeAttacking = true;
        lastSwingTime = Time.time;
        meleeHitTargets.Clear(); // Reset hit targets for new swing
        
        // Start animation-driven swing
        StartCoroutine(PerformMeleeAttack());
        
        ChangeState(WeaponState.Swinging);
        PlaySound(swingSound);
        
        if (debugMeleeAnimation)
        {
            Debug.Log("Animation-driven weapon swing started!");
        }
    }

    // New coroutine for animation-driven melee attack
    private IEnumerator PerformMeleeAttack()
    {
        if (debugMeleeAnimation)
        {
            Debug.Log($"ThrowableWeapon: Starting melee attack animation at {Time.time:F2}");
        }
        
        // Trigger the melee animation
        if (weaponAnimator != null)
        {
            weaponAnimator.SetTrigger(meleeAttackTrigger);
            
            // Try to get actual animation length from animator
            AnimatorClipInfo[] clipInfo = weaponAnimator.GetCurrentAnimatorClipInfo(0);
            if (clipInfo.Length > 0)
            {
                meleeAnimationDuration = clipInfo[0].clip.length;
            }
        }
        else if (debugMeleeAnimation)
        {
            Debug.LogWarning("ThrowableWeapon: No weapon animator found!");
        }
        
        // Enable weapon collider for damage detection during swing
        if (weaponCollider != null)
        {
            weaponCollider.enabled = true;
        }
        
        // Wait for animation to complete
        yield return new WaitForSeconds(meleeAnimationDuration);
        
        // Disable collider after swing completes
        if (weaponCollider != null && currentState == WeaponState.Held)
        {
            weaponCollider.enabled = false;
        }
        
        isMeleeAttacking = false;
        
        // Return to held state if we're still holding the weapon
        if (currentState == WeaponState.Swinging)
        {
            ChangeState(WeaponState.Held);
        }
        
        if (debugMeleeAnimation)
        {
            Debug.Log($"ThrowableWeapon: Melee attack completed at {Time.time:F2}");
        }
    }
    
    private void UpdateCamera()
    {
        if (playerCamera == null) return;
        
        // Update camera size (zoom)
        float currentSize = playerCamera.m_Lens.OrthographicSize;
        float newSize = Mathf.Lerp(currentSize, targetCameraSize, cameraTransitionSpeed * Time.deltaTime);
        playerCamera.m_Lens.OrthographicSize = newSize;
        
        // Update camera lookahead when aiming
        var transposer = playerCamera.GetCinemachineComponent<CinemachineTransposer>();
        if (transposer != null && hasStoredOriginalOffset)
        {
            Vector3 targetOffset = originalCameraOffset;
            
            if (isAiming && throwDirection != Vector3.zero)
            {
                // Calculate lookahead offset based on aim direction
                Vector3 lookaheadOffset = throwDirection * cameraLookaheadDistance;
                targetOffset = originalCameraOffset + lookaheadOffset;
            }
            
            // Smoothly transition to target offset
            Vector3 currentOffset = transposer.m_FollowOffset;
            Vector3 newOffset = Vector3.Lerp(currentOffset, targetOffset, lookaheadTransitionSpeed * Time.deltaTime);
            transposer.m_FollowOffset = newOffset;
        }
    }
    
    private void UpdateCharge()
    {
        if (powerSystem == null) return;
        
        PowerHolder playerPower = powerSystem.GetPlayerPower();
        if (playerPower == null) return;
        
        if (currentState == WeaponState.Thrown || currentState == WeaponState.Returning)
        {
            float playerPowerVal = playerPower.currentPower > 0 ? playerPower.currentPower : 1f;
            float flightDepletion = baseFlightChargeDepletion * (1f + thrownIntensity * intensityDepletionMultiplier) / playerPowerVal;
            currentCharge = Mathf.Max(0f, currentCharge - flightDepletion * Time.deltaTime);
            
            if (debugCharge)
            {
                Debug.Log($"Flight charge depletion: {flightDepletion:F2}/s, Current charge: {currentCharge:F1}");
            }
        }
        else if (currentState == WeaponState.Held || currentState == WeaponState.Aiming)
        {
            float regenRate = baseChargeRegenRate;
            
            if (playerPower.currentPower > 0)
            {
                regenRate += playerPower.currentPower * powerChargeMultiplier;
            }
            
            if (isAiming)
            {
                regenRate *= intensityChargeMultiplier;
            }
            
            currentCharge = Mathf.Min(maxCharge, currentCharge + regenRate * Time.deltaTime);
            
            if (debugCharge)
            {
                Debug.Log($"Charge regen: {regenRate:F2}/s, Current charge: {currentCharge:F1}");
            }
        }
        
        OnChargeChanged?.Invoke(currentCharge / maxCharge);
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
    
    private void UpdateAimingBehavior()
    {
        if (!isAiming) return;
        
        // Charge rotation effect during aiming
        float chargeLevel = currentCharge / maxCharge;
        float targetRotation = Mathf.Lerp(0f, maxChargeRotation, chargeLevel);
        
        currentChargeRotation = Mathf.MoveTowards(currentChargeRotation, targetRotation, chargeRotationSpeed * Time.deltaTime);
        
        if (weaponSprite != null)
        {
            weaponSprite.transform.localRotation = Quaternion.Euler(0, 0, currentChargeRotation);
        }
        
        Vector3 mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0f;
        
        Vector2 direction = (mousePos - weaponContainer.position).normalized;
        weaponContainer.right = direction;
        
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
    
    // Modified UpdateSwingingBehavior - now just handles early exit if needed
    private void UpdateSwingingBehavior()
    {
        // Animation handles the actual swing, just check for early exit conditions
        if (!isMeleeAttacking && currentState == WeaponState.Swinging)
        {
            ChangeState(WeaponState.Held);
        }
    }
    
    private void UpdateThrownBehavior()
    {
        weaponTransform.position += velocity * Time.deltaTime;
        
        // Apply spin effect to the weapon sprite during throwing
        if (weaponSprite != null)
        {
            weaponSprite.transform.Rotate(0, 0, 720f * Time.deltaTime);
        }
        else
        {
            // Fallback: rotate the weapon transform if no separate sprite
            weaponTransform.Rotate(0, 0, 720f * Time.deltaTime);
        }
        
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
        
        // Apply faster spin effect during return
        if (weaponSprite != null)
        {
            weaponSprite.transform.Rotate(0, 0, 1080f * Time.deltaTime);
        }
        else
        {
            // Fallback: rotate the weapon transform if no separate sprite
            weaponTransform.Rotate(0, 0, 1080f * Time.deltaTime);
        }
        
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
    
    private void CheckGroundStatus()
    {
        RaycastHit2D hit = Physics2D.Raycast(weaponTransform.position, Vector2.down, groundCheckDistance, groundLayers);
        isOnGround = hit.collider != null;
        
        if (debugMovement)
        {
            Debug.Log($"Ground check: {isOnGround}");
        }
    }
    
    // Modified OnTriggerEnter2D to handle melee damage during animation
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (debugCollision)
        {
            Debug.Log($"Weapon trigger entered by: {other.gameObject.name} (State: {currentState}, IsMeleeAttacking: {isMeleeAttacking})");
        }
        
        // Handle melee damage during animation-driven swing
        if (currentState == WeaponState.Swinging && isMeleeAttacking)
        {
            HandleMeleeDamage(other);
            return;
        }
        
        // Handle thrown weapon collisions (existing logic)
        if (currentState == WeaponState.Thrown || currentState == WeaponState.Returning)
        {
            HandleThrownWeaponCollision(other);
        }
    }

    // New method to handle melee damage during animation
    private void HandleMeleeDamage(Collider2D hitCollider)
    {
        // Don't hit the same target twice in one swing
        if (meleeHitTargets.Contains(hitCollider.gameObject))
        {
            return;
        }
        
        // Check if target is in valid damage layers
        if (!IsInLayerMask(hitCollider.gameObject.layer, collisionLayers))
        {
            return;
        }
        
        bool hitSomething = false;
        
        // Handle enemy damage
        Health enemyHealth = hitCollider.GetComponent<Health>();
        if (enemyHealth != null)
        {
            enemyHealth.TakeDamage((int)swingDamage);
            OnDamageDealt?.Invoke((int)swingDamage);
            meleeHitTargets.Add(hitCollider.gameObject);
            hitSomething = true;
            
            if (debugMeleeAnimation)
            {
                Debug.Log($"Melee hit enemy {hitCollider.name} for {swingDamage} damage!");
            }
        }
        
        // Handle villager damage (friendly fire)
        VillagerHealth villagerHealth = hitCollider.GetComponent<VillagerHealth>();
        if (villagerHealth != null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            villagerHealth.TakeDamage((int)swingDamage, player);
            OnDamageDealt?.Invoke((int)swingDamage);
            meleeHitTargets.Add(hitCollider.gameObject);
            hitSomething = true;
            
            if (debugMeleeAnimation)
            {
                Debug.Log($"Melee hit villager {hitCollider.name} for {swingDamage} damage!");
            }
        }
        
        if (hitSomething)
        {
            PlaySound(hitSound);
        }
    }
    
    private void HandleThrownWeaponCollision(Collider2D hitCollider)
    {
        bool hitSomething = false;
        int damage = GetCurrentDamage();
        
        // Check for enemy health component first
        Health enemyHealth = hitCollider.GetComponent<Health>();
        if (enemyHealth != null)
        {
            enemyHealth.TakeDamage(damage); // Pass player as damage source
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
        }
    }
    
    private void ChangeState(WeaponState newState)
    {
        if (currentState == newState) return;
        
        WeaponState oldState = currentState;
        currentState = newState;
        
        if (debugMovement)
        {
            Debug.Log($"Weapon state changed: {oldState} -> {newState}");
        }
    }
    
    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip, volume);
        }
    }

    // Helper method to check if object is in layer mask
    private bool IsInLayerMask(int layer, LayerMask layerMask)
    {
        return layerMask == (layerMask | (1 << layer));
    }

    // Add this property for external checking (similar to villager combat systems)
    public bool IsMeleeAttacking()
    {
        return isMeleeAttacking;
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