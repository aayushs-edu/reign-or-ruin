using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class VillagerStatsUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private GameObject uiPanel;
    
    [Header("Power UI")]
    [SerializeField] private Slider powerBar;
    [SerializeField] private TextMeshProUGUI powerText;
    [SerializeField] private Image powerFill;
    [SerializeField] private Color noPowerColor = Color.gray;
    [SerializeField] private Color tier1PowerColor = Color.yellow;
    [SerializeField] private Color tier2PowerColor = Color.cyan;
    
    [Header("Food UI")]
    [SerializeField] private Slider foodBar;
    [SerializeField] private TextMeshProUGUI foodText;
    [SerializeField] private Image foodFill;
    [SerializeField] private Color fullFoodColor = Color.green;
    [SerializeField] private Color lowFoodColor = Color.red;
    
    [Header("Discontent UI")]
    [SerializeField] private Slider discontentBar;
    [SerializeField] private TextMeshProUGUI discontentText;
    [SerializeField] private Image discontentFill;
    [SerializeField] private Color lowDiscontentColor = Color.green;
    [SerializeField] private Color mediumDiscontentColor = Color.yellow;
    [SerializeField] private Color highDiscontentColor = Color.red;
    
    [Header("Role & State UI")]
    [SerializeField] private TextMeshProUGUI roleText;
    [SerializeField] private TextMeshProUGUI tierText;
    [SerializeField] private Image stateIndicator;
    [SerializeField] private Color loyalStateColor = Color.blue;
    [SerializeField] private Color angryStateColor = new Color(1f, 0.65f, 0f);
    [SerializeField] private Color rebelStateColor = Color.red;
    
    [Header("Health UI")]
    [SerializeField] private Slider healthBar;
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private Image healthFill;
    [SerializeField] private Color fullHealthColor = Color.green;
    [SerializeField] private Color lowHealthColor = Color.red;
    
    [Header("Display Settings")]
    [SerializeField] private bool alwaysShow = false;
    [SerializeField] private float showDistance = 5f;
    [SerializeField] private bool autoHideWhenFull = true;
    [SerializeField] private Vector3 uiOffset = new Vector3(0, 2f, 0);
    
    // References
    private Camera playerCamera;
    private Transform villagerTransform;
    private Villager villager;
    private bool isVisible = true;
    
    private void Start()
    {
        InitializeUI();
        SetupReferences();
    }
    
    private void InitializeUI()
    {
        // Auto-find components if not assigned
        if (canvas == null)
            canvas = GetComponentInChildren<Canvas>();
        
        if (uiPanel == null)
            uiPanel = transform.GetChild(0).gameObject;
        
        // Setup canvas for world space UI
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            
            // Make UI smaller for world space
            canvas.transform.localScale = Vector3.one * 0.01f;
        }
        
        // Initialize bar max values
        if (powerBar != null) powerBar.maxValue = 2f; // Start with tier 0 requirement
        if (foodBar != null) foodBar.maxValue = 1f;
        if (discontentBar != null) discontentBar.maxValue = 100f;
        if (healthBar != null) healthBar.maxValue = 100f; // Will update dynamically
    }
    
    private void SetupReferences()
    {
        playerCamera = Camera.main;
        villagerTransform = transform.parent;
        villager = GetComponentInParent<Villager>();
        
        if (villager == null)
        {
            Debug.LogWarning("VillagerStatsUI: No Villager component found in parent!");
        }
    }
    
    private void Update()
    {
        UpdateUIPosition();
        UpdateVisibility();
        
        // UI stays at fixed rotation - no longer faces camera
    }
    
    private void UpdateUIPosition()
    {
        if (villagerTransform != null)
        {
            transform.position = villagerTransform.position + uiOffset;
        }
    }
    
    private void UpdateVisibility()
    {
        if (alwaysShow)
        {
            SetUIVisible(true);
            return;
        }
        
        bool shouldShow = false;
        
        // Show if player is nearby
        if (playerCamera != null && villagerTransform != null)
        {
            float distance = Vector3.Distance(playerCamera.transform.position, villagerTransform.position);
            shouldShow = distance <= showDistance;
        }
        
        // Always show if villager is angry or rebel
        if (villager != null)
        {
            VillagerState state = villager.GetState();
            if (state == VillagerState.Angry || state == VillagerState.Rebel)
            {
                shouldShow = true;
            }
        }
        
        SetUIVisible(shouldShow);
    }
    
    private void SetUIVisible(bool visible)
    {
        if (isVisible != visible)
        {
            isVisible = visible;
            if (uiPanel != null)
            {
                uiPanel.SetActive(visible);
            }
        }
    }
    
    public void UpdateStats(VillagerStats stats)
    {
        UpdatePowerUI(stats);
        UpdateFoodUI(stats);
        UpdateDiscontentUI(stats);
        UpdateHealthUI(stats);
        UpdateRoleUI(stats);
    }
    
    private void UpdatePowerUI(VillagerStats stats)
    {
        // Calculate tier requirements and progress
        int currentTierRequirement = GetTierRequirement(stats.tier);
        int nextTierRequirement = GetTierRequirement(stats.tier + 1);
        int powerInCurrentTier = stats.power - GetTotalPowerForTier(stats.tier - 1);
        
        if (powerBar != null)
        {
            powerBar.maxValue = currentTierRequirement;
            powerBar.value = powerInCurrentTier;
        }
        
        if (powerText != null)
        {
            powerText.text = $"Power: {powerInCurrentTier}/{currentTierRequirement}";
        }
        
        if (powerFill != null)
        {
            // Color based on tier
            if (stats.tier >= 2)
                powerFill.color = tier2PowerColor;
            else if (stats.tier >= 1)
                powerFill.color = tier1PowerColor;
            else
                powerFill.color = noPowerColor;
        }
    }
    
    private int GetTierRequirement(int tier)
    {
        switch (tier)
        {
            case 0: return 2; // Need 2 power to reach tier 1
            case 1: return 2; // Need 2 more power to reach tier 2 (4 total)
            case 2: return 0; // Max tier reached
            default: return 2;
        }
    }
    
    private int GetTotalPowerForTier(int tier)
    {
        if (tier < 0) return 0;
        if (tier == 0) return 2;  // Tier 1 needs 2 total
        if (tier == 1) return 4;  // Tier 2 needs 4 total
        return 4; // Max
    }
    
    private void UpdateFoodUI(VillagerStats stats)
    {
        if (foodBar != null)
        {
            foodBar.value = stats.food;
        }
        
        if (foodText != null)
        {
            foodText.text = $"Food: {stats.food:P0}";
        }
        
        if (foodFill != null)
        {
            foodFill.color = Color.Lerp(lowFoodColor, fullFoodColor, stats.food);
        }
    }
    
    private void UpdateDiscontentUI(VillagerStats stats)
    {
        if (discontentBar != null)
        {
            discontentBar.value = stats.discontent;
        }
        
        if (discontentText != null)
        {
            discontentText.text = $"Discontent: {stats.discontent:F0}%";
        }
        
        if (discontentFill != null)
        {
            // Color based on discontent level
            if (stats.discontent >= 80f)
                discontentFill.color = highDiscontentColor;
            else if (stats.discontent >= 50f)
                discontentFill.color = mediumDiscontentColor;
            else
                discontentFill.color = lowDiscontentColor;
        }
    }
    
    private void UpdateHealthUI(VillagerStats stats)
    {
        if (healthBar != null)
        {
            healthBar.maxValue = stats.maxHP;
            healthBar.value = stats.currentHP;
        }
        
        if (healthText != null)
        {
            healthText.text = $"HP: {stats.currentHP}/{stats.maxHP}";
        }
        
        if (healthFill != null)
        {
            float healthPercent = stats.maxHP > 0 ? (float)stats.currentHP / stats.maxHP : 0f;
            healthFill.color = Color.Lerp(lowHealthColor, fullHealthColor, healthPercent);
        }
    }
    
    private void UpdateRoleUI(VillagerStats stats)
    {
        if (roleText != null && villager != null)
        {
            roleText.text = villager.GetRole().ToString();
        }
        
        if (tierText != null)
        {
            tierText.text = $"Tier {stats.tier}";
        }
        
        if (stateIndicator != null && villager != null)
        {
            switch (villager.GetState())
            {
                case VillagerState.Loyal:
                    stateIndicator.color = loyalStateColor;
                    break;
                case VillagerState.Angry:
                    stateIndicator.color = angryStateColor;
                    break;
                case VillagerState.Rebel:
                    stateIndicator.color = rebelStateColor;
                    break;
            }
        }
    }
    
    // Public methods for external control
    public void SetAlwaysShow(bool always)
    {
        alwaysShow = always;
    }
    
    public void SetShowDistance(float distance)
    {
        showDistance = distance;
    }
    
    public void ForceShow(bool show)
    {
        SetUIVisible(show);
    }
    
    // Debug method
    public void TestStatsDisplay()
    {
        VillagerStats testStats = new VillagerStats();
        testStats.power = 2;
        testStats.food = 0.7f;
        testStats.discontent = 45f;
        testStats.currentHP = 80;
        testStats.maxHP = 100;
        testStats.tier = 1;
        
        UpdateStats(testStats);
    }
}