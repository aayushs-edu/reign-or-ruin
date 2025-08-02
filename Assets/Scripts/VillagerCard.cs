using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Component for individual villager cards in the power allocation UI.
/// Attach this to your villager card prefab and assign all fields in the inspector.
/// </summary>
public class VillagerCard : MonoBehaviour
{
    [Header("Text Components")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI roleText;
    // public TextMeshProUGUI statsText;
    [Header("Efficiency Bar")]
    public Slider efficiencyBar;
    public TextMeshProUGUI powerText;
    public TextMeshProUGUI tierText;
    
    [Header("Interactive Components")]
    public Slider powerSlider;
    public Button addPowerButton;
    public Button removePowerButton;
    
    [Header("Visual Components")]
    public Image roleIcon;
    public Image cardBackground;
    public Image powerFill;
    
    [Header("Status Bars")]
    public Image healthBarFill;
    public Image discontentBarFill;
    public Image foodBarFill;
    public Slider healthBar;
    public Slider discontentBar;
    public Slider foodBar;
    
    [Header("Status Indicators")]
    public GameObject rebelIndicator;
    public GameObject activeIndicator;
    public GameObject deadIndicator;
    
    [Header("Colors")]
    public Color normalRoleColor = Color.white;
    public Color captainColor = new Color(0.6f, 0.4f, 0.8f); // Purple
    public Color mageColor = new Color(0.6f, 0.4f, 0.8f);
    public Color farmerColor = new Color(0.6f, 0.4f, 0.8f);
    public Color rebelColor = Color.red;
    
    [Header("Power Tier Colors")]
    public Color noPowerColor = Color.gray;
    public Color tier1PowerColor = Color.yellow;
    public Color tier2PowerColor = Color.green;
    
    [Header("Status Colors")]
    public Color goodColor = Color.green;
    public Color warningColor = Color.yellow;
    public Color dangerColor = Color.red;
    
    // Internal state
    [HideInInspector] public Villager assignedVillager;
    [HideInInspector] public VillagerCombat assignedCombat;
    [HideInInspector] public int cardIndex;
    
    // Events for power allocation
    public System.Action<VillagerCard, int> OnPowerChanged;

    private void Start()
    {
        SetupEventListeners();
        
    }
    
    private void SetupEventListeners()
    {
        if (addPowerButton != null)
            addPowerButton.onClick.AddListener(() => ModifyPower(1));
        
        if (removePowerButton != null)
            removePowerButton.onClick.AddListener(() => ModifyPower(-1));
        
        if (powerSlider != null)
            powerSlider.onValueChanged.AddListener(OnSliderChanged);
    }
    
    private void ModifyPower(int amount)
    {
        if (assignedVillager == null) return;
        
        VillagerStats stats = assignedVillager.GetStats();
        
        int newPower = Mathf.Clamp(stats.power + amount, 0, 4);
        
        if (newPower != stats.power)
        {
            OnPowerChanged?.Invoke(this, newPower);
        }
    }
    
    private void OnSliderChanged(float value)
    {
        if (assignedVillager == null) return;
        
        int newPower = Mathf.RoundToInt(value);
        VillagerStats stats = assignedVillager.GetStats();
        
        if (newPower != stats.power)
        {
            OnPowerChanged?.Invoke(this, newPower);
        }
    }
    
    /// <summary>
    /// Update the card display with villager data
    /// </summary>
    public void UpdateCard(Villager villager)
    {
        assignedVillager = villager;
        assignedCombat = villager != null ? villager.GetComponent<VillagerCombat>() : null;
        
        if (villager == null)
        {
            HideCard();
            return;
        }
        
        ShowCard();
        
        VillagerStats stats = villager.GetStats();
        VillagerRole role = villager.GetRole();
        
        // Update basic info
        UpdateBasicInfo(villager, stats, role);
        
        // Update power controls
        UpdatePowerControls(stats);
        
        // Update status bars
        UpdateStatusBars(villager, stats);
        
        // Update indicators
        UpdateStatusIndicators(villager);
        
        // Update button states
        UpdateButtonStates(stats);
    }
    
    private void UpdateBasicInfo(Villager villager, VillagerStats stats, VillagerRole role)
    {
        // Name
        if (nameText != null)
            nameText.text = villager.name;
        
        // Role with special colors
        if (roleText != null)
        {
            roleText.text = role.ToString();
            roleText.color = GetRoleColor(role);
        }

        // Efficiency bar
        if (efficiencyBar != null)
        {
            float efficiency = assignedCombat != null ? assignedCombat.GetEfficiency() : 1f;
            efficiencyBar.value = efficiency;
            if (efficiency >= 80f)
                efficiencyBar.fillRect.GetComponent<Image>().color = goodColor;
            else if (efficiency >= 50f)
                efficiencyBar.fillRect.GetComponent<Image>().color = warningColor;
            else
                efficiencyBar.fillRect.GetComponent<Image>().color = dangerColor;
        }
        
        // Tier display
        if (tierText != null)
        {
            tierText.text = $"Tier: {stats.tier}";
        }
    }
    
    private void UpdatePowerControls(VillagerStats stats)
    {
        // Power slider
        if (powerSlider != null)
        {
            powerSlider.maxValue = 4;
            powerSlider.value = stats.power;
        }
        
        // Power text
        if (powerText != null)
        {
            powerText.text = $"Power: {stats.power}/4";
        }
        
        // Power fill color
        if (powerFill != null)
        {
            if (stats.tier >= 2)
                powerFill.color = tier2PowerColor;
            else if (stats.tier >= 1)
                powerFill.color = tier1PowerColor;
            else
                powerFill.color = noPowerColor;
        }
    }
    
    private void UpdateStatusBars(Villager villager, VillagerStats stats)
    {
        // Health bar
        float healthPercent = GetHealthPercent(villager);
        if (healthBar != null)
            healthBar.value = healthPercent;
        if (healthBarFill != null)
        {
            healthBarFill.fillAmount = healthPercent;
            healthBarFill.color = healthPercent >= 0.5f ? goodColor : dangerColor;
        }
        
        // Discontent bar
        float discontentPercent = stats.discontent / 100f;
        if (discontentBar != null)
            discontentBar.value = discontentPercent;
        if (discontentBarFill != null)
        {
            discontentBarFill.fillAmount = discontentPercent;
            
            if (stats.discontent >= 80f)
                discontentBarFill.color = dangerColor;
            else if (stats.discontent >= 50f)
                discontentBarFill.color = warningColor;
            else
                discontentBarFill.color = goodColor;
        }
        
        // Food bar
        if (foodBar != null)
            foodBar.value = stats.food;
        if (foodBarFill != null)
        {
            foodBarFill.fillAmount = stats.food;
            foodBarFill.color = stats.food >= 0.5f ? goodColor : warningColor;
        }
    }
    
    private void UpdateStatusIndicators(Villager villager)
    {
        // Rebel indicator
        if (rebelIndicator != null)
            rebelIndicator.SetActive(!villager.IsLoyal());
        
        // Active indicator
        if (activeIndicator != null)
            activeIndicator.SetActive(villager.IsActive());
        
        // Dead indicator (if you have death system)
        if (deadIndicator != null)
            deadIndicator.SetActive(false); // Implement if needed
    }
    
    private void UpdateButtonStates(VillagerStats stats)
    {
        // This will be set by the parent UI system
        // We'll provide the current state but let the parent decide interactability
        
        if (addPowerButton != null)
        {
            // Enable if not at max power
            bool canAdd = stats.power < 4;
            addPowerButton.interactable = canAdd;
        }
        
        if (removePowerButton != null)
        {
            // Enable if has power to remove
            bool canRemove = stats.power > 0;
            removePowerButton.interactable = canRemove;
        }
    }
    
    private Color GetRoleColor(VillagerRole role)
    {
        switch (role)
        {
            case VillagerRole.Captain: return captainColor;
            case VillagerRole.Mage: return mageColor;
            case VillagerRole.Farmer: return farmerColor;
            default: return normalRoleColor;
        }
    }
    
    private float GetHealthPercent(Villager villager)
    {
        // Get health from villager health component
        VillagerHealth health = villager.GetComponent<VillagerHealth>();
        if (health != null)
        {
            return health.GetHealthPercentage();
        }
        return 1f; // Default to full health
    }
    
    /// <summary>
    /// Show this card
    /// </summary>
    public void ShowCard()
    {
        gameObject.SetActive(true);
    }
    
    /// <summary>
    /// Hide this card
    /// </summary>
    public void HideCard()
    {
        gameObject.SetActive(false);
        assignedVillager = null;
    }
    
    /// <summary>
    /// Set power allocation constraints
    /// </summary>
    public void SetPowerConstraints(bool canAddPower, bool canRemovePower)
    {
        if (addPowerButton != null)
            addPowerButton.interactable = canAddPower;
        if (removePowerButton != null)
            removePowerButton.interactable = canRemovePower;
    }
    
    /// <summary>
    /// Get the current power allocation
    /// </summary>
    public int GetCurrentPower()
    {
        return assignedVillager != null ? assignedVillager.GetStats().power : 0;
    }
    
    /// <summary>
    /// Check if this card represents a valid villager
    /// </summary>
    public bool HasVillager()
    {
        return assignedVillager != null;
    }
    
    private void OnDestroy()
    {
        // Clean up event listeners
        if (addPowerButton != null)
            addPowerButton.onClick.RemoveAllListeners();
        if (removePowerButton != null)
            removePowerButton.onClick.RemoveAllListeners();
        if (powerSlider != null)
            powerSlider.onValueChanged.RemoveAllListeners();
    }
    
    #if UNITY_EDITOR
    [ContextMenu("Test Card Update")]
    private void TestCardUpdate()
    {
        if (Application.isPlaying && assignedVillager != null)
        {
            UpdateCard(assignedVillager);
        }
    }
    #endif
}