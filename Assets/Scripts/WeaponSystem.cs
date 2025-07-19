using UnityEngine;

public class WeaponSystem : MonoBehaviour
{
    [Header("Weapon References")]
    [SerializeField] private Transform weaponTransform;
    [SerializeField] private SpriteRenderer weaponRenderer;
    
    [Header("Damage Configuration")]
    [SerializeField] private int baseDamage = 1;
    [SerializeField] private float damageMultiplierPerPower = 0.1f;
    [SerializeField] private int maxDamageBonus = 10;
    
    [Header("Visual Effects")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color chargedColor = Color.cyan;
    [SerializeField] private float chargeColorThreshold = 0.3f; // When to start color change
    
    [Header("Animation Settings")]
    [SerializeField] private float walkTiltAngle = 15f;
    [SerializeField] private float walkTiltSpeed = 3f;
    [SerializeField] private float spinSpeedMultiplier = 180f; // Degrees per second per power percentage
    [SerializeField] private float chargeThreshold = 0.2f; // When spinning starts (20% charge)
    
    [Header("Attack Settings")]
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float attackCooldown = 0.5f;
    [SerializeField] private LayerMask enemyLayer = -1;
    
    // Components and references
    private PlayerMovement playerMovement;
    private PowerSystem powerSystem;
    
    // Animation state
    private float currentTilt = 0f;
    private float currentSpinAngle = 0f;
    private bool isCharged = false;
    private float lastAttackTime = 0f;
    
    // Weapon stats
    private int currentDamage;
    private float currentChargeLevel;
    
    // Events
    public System.Action<int> OnDamageChanged;
    public System.Action<float> OnChargeChanged; // 0-1 charge level
    
    private void Start()
    {
        InitializeComponents();
        SetupWeapon();
    }
    
    private void InitializeComponents()
    {
        // Get player movement component
        playerMovement = GetComponentInParent<PlayerMovement>();
        if (playerMovement == null)
        {
            playerMovement = FindObjectOfType<PlayerMovement>();
        }
        
        // Get power system
        if (PowerSystem.Instance == null)
        {
            Debug.LogError("WeaponSystem: PowerSystem not found!");
        }
        else
        {
            powerSystem = PowerSystem.Instance;
            powerSystem.OnPlayerPowerChanged += UpdateWeaponCharge;
        }
        
        // Auto-find weapon transform if not assigned
        if (weaponTransform == null)
        {
            weaponTransform = transform;
        }
        
        // Auto-find weapon renderer if not assigned
        if (weaponRenderer == null)
        {
            weaponRenderer = GetComponent<SpriteRenderer>();
        }
    }
    
    private void SetupWeapon()
    {
        if (weaponRenderer != null)
        {
            weaponRenderer.color = normalColor;
        }
        
        UpdateWeaponStats();
    }
    
    private void Update()
    {
        HandleInput();
        UpdateWeaponAnimation();
        UpdateWeaponStats();
    }
    
    private void HandleInput()
    {
        // Attack input (Space or Left Click)
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
        {
            TryAttack();
        }
    }
    
    private void UpdateWeaponAnimation()
    {
        if (weaponTransform == null) return;
        
        // Get player movement state
        bool isMoving = playerMovement != null && playerMovement.IsMoving();
        
        if (isCharged)
        {
            // Spinning animation when charged
            UpdateSpinAnimation();
        }
        else
        {
            // Tilt animation when walking
            UpdateTiltAnimation(isMoving);
        }
    }
    
    private void UpdateSpinAnimation()
    {
        // Spin based on charge level
        float spinSpeed = currentChargeLevel * spinSpeedMultiplier;
        currentSpinAngle += spinSpeed * Time.deltaTime;
        
        // Keep angle in 0-360 range
        if (currentSpinAngle >= 360f)
        {
            currentSpinAngle -= 360f;
        }
        
        weaponTransform.rotation = Quaternion.Euler(0, 0, currentSpinAngle);
    }
    
    private void UpdateTiltAnimation(bool isMoving)
    {
        if (isMoving)
        {
            // Tilt side to side while walking
            float targetTilt = Mathf.Sin(Time.time * walkTiltSpeed) * walkTiltAngle;
            currentTilt = Mathf.LerpAngle(currentTilt, targetTilt, Time.deltaTime * walkTiltSpeed);
        }
        else
        {
            // Return to neutral position when not moving
            currentTilt = Mathf.LerpAngle(currentTilt, 0f, Time.deltaTime * walkTiltSpeed * 2f);
        }
        
        weaponTransform.rotation = Quaternion.Euler(0, 0, currentTilt);
    }
    
    private void UpdateWeaponStats()
    {
        if (powerSystem == null) return;
        
        PowerHolder playerPower = powerSystem.GetPlayerPower();
        if (playerPower == null) return;
        
        // Calculate charge level (0-1)
        currentChargeLevel = playerPower.GetPowerPercentage();
        
        // Calculate damage
        int damageBonus = Mathf.RoundToInt(playerPower.currentPower * damageMultiplierPerPower);
        damageBonus = Mathf.Min(damageBonus, maxDamageBonus);
        int newDamage = baseDamage + damageBonus;
        
        // Update weapon state
        bool wasCharged = isCharged;
        isCharged = currentChargeLevel >= chargeThreshold;
        
        // Update visual effects
        UpdateWeaponVisuals();
        
        // Fire events if values changed
        if (currentDamage != newDamage)
        {
            currentDamage = newDamage;
            OnDamageChanged?.Invoke(currentDamage);
        }
        
        OnChargeChanged?.Invoke(currentChargeLevel);
        
        // Reset spin angle when transitioning from charged to uncharged
        if (wasCharged && !isCharged)
        {
            currentSpinAngle = 0f;
        }
    }
    
    private void UpdateWeaponVisuals()
    {
        if (weaponRenderer == null) return;
        
        if (currentChargeLevel >= chargeColorThreshold)
        {
            // Interpolate between normal and charged color
            float colorT = (currentChargeLevel - chargeColorThreshold) / (1f - chargeColorThreshold);
            weaponRenderer.color = Color.Lerp(normalColor, chargedColor, colorT);
        }
        else
        {
            weaponRenderer.color = normalColor;
        }
    }
    
    private void UpdateWeaponCharge(PowerHolder playerPower)
    {
        // This gets called whenever player power changes
        UpdateWeaponStats();
    }
    
    private void TryAttack()
    {
        if (Time.time - lastAttackTime < attackCooldown) return;
        
        lastAttackTime = Time.time;
        PerformAttack();
    }
    
    private void PerformAttack()
    {
        // Find enemies in range
        Collider2D[] enemies = Physics2D.OverlapCircleAll(transform.position, attackRange, enemyLayer);
        
        foreach (Collider2D enemy in enemies)
        {
            EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(currentDamage);
                
                // Visual feedback for attack
                CreateAttackEffect(enemy.transform.position);
            }
        }
        
        // Attack animation/effect could go here
        Debug.Log($"Player attacked with {currentDamage} damage! Charge: {currentChargeLevel:P0}");
    }
    
    private void CreateAttackEffect(Vector3 position)
    {
        // You can add visual/audio effects here
        // For now, just debug visualization
        Debug.DrawLine(transform.position, position, Color.red, 0.2f);
    }
    
    // Public getters
    public int GetCurrentDamage() => currentDamage;
    public float GetChargeLevel() => currentChargeLevel;
    public bool IsCharged() => isCharged;
    public float GetAttackRange() => attackRange;
    
    // Public setters for customization
    public void SetBaseDamage(int damage)
    {
        baseDamage = damage;
        UpdateWeaponStats();
    }
    
    public void SetWeaponSprite(Sprite newSprite)
    {
        if (weaponRenderer != null)
        {
            weaponRenderer.sprite = newSprite;
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        // Draw attack range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        
        // Draw charge level indicator
        if (Application.isPlaying)
        {
            Gizmos.color = Color.Lerp(Color.white, Color.cyan, currentChargeLevel);
            Gizmos.DrawSphere(transform.position + Vector3.up * 2f, 0.2f);
        }
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from events
        if (powerSystem != null)
        {
            powerSystem.OnPlayerPowerChanged -= UpdateWeaponCharge;
        }
    }
}