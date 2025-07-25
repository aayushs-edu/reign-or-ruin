using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class VillagerStatsUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private GameObject uiPanel;
    [SerializeField] private GameObject healthOnlyPanel; // Always visible health display
    
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
    
    [Header("Power Allocation")]
    [SerializeField] private Button addPowerButton;
    [SerializeField] private Button removePowerButton;
    [SerializeField] private int powerPerClick = 1;
    
    [Header("Display Settings")]
    [SerializeField] private bool alwaysShow = false;
    [SerializeField] private float showDistance = 5f;
    [SerializeField] private bool autoHideWhenFull = true;
    [SerializeField] private Vector3 uiOffset = new Vector3(0, 2f, 0);
    
    // References
    private Camera playerCamera;
    private Transform villagerTransform;
    private Villager villager;
    private bool isPanelVisible = false;
    private Collider2D villagerCollider;
    
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
        
        // Get villager collider for mouse detection
        if (villagerTransform != null)
        {
            villagerCollider = villagerTransform.GetComponent<Collider2D>();
            if (villagerCollider == null)
            {
                Debug.LogWarning("VillagerStatsUI: No Collider2D found on villager for mouse detection!");
            }
        }
        
        // Setup power allocation buttons
        SetupPowerButtons();
        
        // Initialize panel visibility
        SetPanelVisible(alwaysShow);
    }
    
    private void SetupPowerButtons()
    {
        if (addPowerButton != null)
        {
            addPowerButton.onClick.AddListener(() => ModifyVillagerPower(powerPerClick));
        }
        
        if (removePowerButton != null)
        {
            removePowerButton.onClick.AddListener(() => ModifyVillagerPower(-powerPerClick));
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
            SetPanelVisible(true);
            return;
        }
        
        bool shouldShow = false;
        
        // Check if mouse is over villager using raycast
        if (IsMouseOverVillager())
        {
            shouldShow = true;
        }
        
        // Check if mouse is over UI panel
        if (isPanelVisible && IsMouseOverUIPanel())
        {
            shouldShow = true;
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
        
        SetPanelVisible(shouldShow);
    }
    
    private bool IsMouseOverVillager()
    {
        if (playerCamera == null || villagerCollider == null) return false;
        
        Vector3 mouseWorldPos = playerCamera.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0f; // Ensure we're on the 2D plane
        
        return villagerCollider.OverlapPoint(mouseWorldPos);
    }
    
    private bool IsMouseOverUIPanel()
    {
        if (uiPanel == null || !uiPanel.activeInHierarchy) return false;
        
        // Check if mouse is over any UI element in the panel
        var eventSystem = UnityEngine.EventSystems.EventSystem.current;
        if (eventSystem == null) return false;
        
        var pointerEventData = new UnityEngine.EventSystems.PointerEventData(eventSystem);
        pointerEventData.position = Input.mousePosition;
        
        var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
        UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointerEventData, results);
        
        foreach (var result in results)
        {
            if (result.gameObject.transform.IsChildOf(uiPanel.transform) || result.gameObject == uiPanel)
            {
                return true;
            }
        }
        
        return false;
    }
    
    private void SetPanelVisible(bool visible)
    {
        if (isPanelVisible != visible)
        {
            isPanelVisible = visible;
            if (uiPanel != null)
            {
                uiPanel.SetActive(visible);
            }
        }
        
        // Health panel is always visible
        if (healthOnlyPanel != null)
        {
            healthOnlyPanel.SetActive(true);
        }
    }
    
    public void UpdateStats(VillagerStats stats)
    {
        UpdatePowerUI(stats);
        UpdateFoodUI(stats);
        UpdateDiscontentUI(stats);
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
        
        // Update button states
        UpdatePowerButtons(stats);
    }
    
    private void UpdatePowerButtons(VillagerStats stats)
    {
        if (addPowerButton != null)
        {
            // Enable if villager isn't at max tier and player has power available
            bool canAddPower = stats.tier < 2 && CanPlayerAllocatePower(powerPerClick);
            addPowerButton.interactable = canAddPower;
        }
        
        if (removePowerButton != null)
        {
            // Enable if villager has power to remove
            bool canRemovePower = stats.power > 0;
            removePowerButton.interactable = canRemovePower;
        }
    }
    
    private bool CanPlayerAllocatePower(int amount)
    {
        // Check if player has enough power to allocate
        // This would connect to your power system
        if (PowerSystem.Instance != null)
        {
            return PowerSystem.Instance.GetTotalCommunalPower() >= amount;
        }
        return true; // Default to true for testing
    }
    
    private void ModifyVillagerPower(int amount)
    {
        if (villager == null) return;
        
        VillagerStats stats = villager.GetStats();
        int newPowerAmount = Mathf.Max(0, stats.power + amount);
        
        // Cap at max tier power (4)
        newPowerAmount = Mathf.Min(4, newPowerAmount);
        
        if (newPowerAmount != stats.power)
        {
            // Handle power allocation through proper systems
            if (amount > 0)
            {
                // Adding power - check if player has enough
                if (CanPlayerAllocatePower(amount))
                {
                    AllocatePowerToVillager(newPowerAmount);
                }
            }
            else
            {
                // Removing power - always allowed
                AllocatePowerToVillager(newPowerAmount);
            }
        }
    }
    
    private void AllocatePowerToVillager(int totalPower)
    {
        if (villager == null) return;
        
        // This should integrate with your power allocation system
        villager.AllocatePower(totalPower);
        
        // Optionally notify power system of the change
        if (PowerSystem.Instance != null)
        {
            // PowerSystem.Instance.OnVillagerPowerChanged(villager, totalPower);
        }
        
        Debug.Log($"Allocated {totalPower} power to {villager.GetRole()} villager");
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
        SetPanelVisible(show);
    }
    
    public void SetPowerPerClick(int amount)
    {
        powerPerClick = amount;
    }
    
    // Manual power allocation methods
    public void AddPowerToVillager()
    {
        ModifyVillagerPower(powerPerClick);
    }
    
    public void RemovePowerFromVillager()
    {
        ModifyVillagerPower(-powerPerClick);
    }
    
    private void OnDestroy()
    {
        // Clean up button listeners
        if (addPowerButton != null)
        {
            addPowerButton.onClick.RemoveAllListeners();
        }
        
        if (removePowerButton != null)
        {
            removePowerButton.onClick.RemoveAllListeners();
        }
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