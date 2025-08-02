using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Enhanced villager card component that properly updates all fields using actual component references.
/// Gets health from VillagerHealth, efficiency from VillagerCombat, and all other data from VillagerStats.
/// </summary>
public class VillagerCard : MonoBehaviour
{
    [Header("Text Components")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI roleText;
    public TextMeshProUGUI powerText;
    public TextMeshProUGUI tierText;
    public TextMeshProUGUI efficiencyText;
    
    [Header("Interactive Components")]
    public Slider powerSlider;
    public Button addPowerButton;
    public Button removePowerButton;
    
    [Header("Visual Components")]
    public Image roleIcon;
    public Image cardBackground;
    public Image powerFill;
    
    [Header("Status Bars")]
    public Slider healthBar;
    public Slider discontentBar;
    public Slider foodBar;
    public Slider efficiencyBar;
    
    [Header("Status Bar Fills (Alternative)")]
    public Image healthBarFill;
    public Image discontentBarFill;
    public Image foodBarFill;
    public Image efficiencyBarFill;
    
    [Header("Status Text Labels")]
    public TextMeshProUGUI healthText;
    public TextMeshProUGUI discontentText;
    public TextMeshProUGUI foodText;
    
    [Header("Status Indicators")]
    public GameObject rebelIndicator;
    public GameObject activeIndicator;
    public GameObject deadIndicator;
    public GameObject loyalIndicator;
    
    [Header("Colors")]
    public Color normalRoleColor = Color.white;
    public Color captainColor = new Color(0.6f, 0.4f, 0.8f);
    public Color mageColor = new Color(0.4f, 0.6f, 0.8f);
    public Color farmerColor = new Color(0.4f, 0.8f, 0.4f);
    public Color rebelColor = Color.red;
    
    [Header("Power Tier Colors")]
    public Color noPowerColor = Color.gray;
    public Color tier1PowerColor = Color.yellow;
    public Color tier2PowerColor = Color.green;
    public Color maxPowerColor = Color.cyan;
    
    [Header("Status Colors")]
    public Color goodColor = Color.green;
    public Color warningColor = Color.yellow;
    public Color dangerColor = Color.red;
    public Color excellentColor = new Color(0.2f, 1f, 0.2f);
    
    // Internal state
    [HideInInspector] public Villager assignedVillager;
    [HideInInspector] public VillagerHealth villagerHealth;
    [HideInInspector] public VillagerCombat villagerCombat;
    [HideInInspector] public int cardIndex;
    
    // Events
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
    /// Update the card display with villager data - ensures all fields are updated using correct components
    /// </summary>
    public void UpdateCard(Villager villager)
    {
        assignedVillager = villager;
        
        if (villager == null)
        {
            HideCard();
            return;
        }
        
        // Get component references
        villagerHealth = villager.GetComponent<VillagerHealth>();
        villagerCombat = villager.GetComponent<VillagerCombat>();
        
        ShowCard();
        
        VillagerStats stats = villager.GetStats();
        VillagerRole role = villager.GetRole();
        
        // Update all components in proper order
        UpdateBasicInfo(villager, stats, role);
        UpdatePowerControls(stats);
        UpdateStatusBars(villager, stats);
        UpdateStatusIndicators(villager);
        UpdateButtonStates(stats);
        
        // Force layout refresh
        LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
    }
    
    private void UpdateBasicInfo(Villager villager, VillagerStats stats, VillagerRole role)
    {
        // Name
        if (nameText != null)
            nameText.text = villager.name;
        
        // Role with colors
        if (roleText != null)
        {
            roleText.text = role.ToString();
            roleText.color = GetRoleColor(role);
        }
        
        // Tier display
        if (tierText != null)
        {
            tierText.text = $"Tier {stats.tier}";
        }
        
        // Efficiency - get from VillagerCombat component
        if (efficiencyText != null && villagerCombat != null)
        {
            float efficiency = villagerCombat.GetEfficiency() * 100f; // Convert to percentage
            efficiencyText.text = $"Efficiency: {efficiency:F0}%";
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
        
        // Power fill color based on tier and power level
        if (powerFill != null)
        {
            powerFill.color = GetPowerColor(stats.power, stats.tier);
        }
    }
    
    private void UpdateStatusBars(Villager villager, VillagerStats stats)
    {
        // Health bar - get from VillagerHealth component
        float healthPercent = GetHealthPercent();
        UpdateBar(healthBar, healthBarFill, healthText, healthPercent, "Health", true);
        
        // Discontent bar (0-100 scale)
        float discontentPercent = Mathf.Clamp01(stats.discontent / 100f);
        UpdateBar(discontentBar, discontentBarFill, discontentText, discontentPercent, "Discontent", false);
        
        // Food bar (0-1 scale from VillagerStats)
        UpdateBar(foodBar, foodBarFill, foodText, stats.food, "Food", true);
        
        // Efficiency bar - get from VillagerCombat component
        if (villagerCombat != null)
        {
            float efficiency = villagerCombat.GetEfficiency(); // Already 0-1 range
            UpdateBar(efficiencyBar, efficiencyBarFill, efficiencyText, efficiency, "Efficiency", true);
        }
    }
    
    private void UpdateBar(Slider slider, Image fill, TextMeshProUGUI text, float value, string label, bool higherIsBetter)
    {
        // Update slider
        if (slider != null)
        {
            slider.value = value;
        }
        
        // Update fill image
        if (fill != null)
        {
            fill.fillAmount = value;
            fill.color = GetStatusColor(value, higherIsBetter);
        }
        
        // Update text
        if (text != null)
        {
            if (label == "Efficiency")
                text.text = $"{label}: {value * 100:F0}%";
            else if (label == "Discontent")
                text.text = $"{label}: {value * 100:F0}%";
            else if (label == "Food")
                text.text = $"{label}: {value:P0}";
            else if (label == "Health")
                text.text = $"{label}: {value:P0}";
        }
    }
    
    private void UpdateStatusIndicators(Villager villager)
    {
        bool isLoyal = villager.IsLoyal();
        bool isRebel = villager.IsRebel();
        bool isAlive = !villagerHealth.IsDead(); // Use VillagerHealth component
        
        // Rebel indicator
        if (rebelIndicator != null)
            rebelIndicator.SetActive(isRebel);
        
        // Loyal indicator
        if (loyalIndicator != null)
            loyalIndicator.SetActive(isLoyal);
        
        // Dead indicator
        if (deadIndicator != null)
            deadIndicator.SetActive(!isAlive);
        
        // Active indicator (alive and loyal)
        if (activeIndicator != null)
            activeIndicator.SetActive(isAlive && isLoyal);
    }
    
    private void UpdateButtonStates(VillagerStats stats)
    {
        // Enable/disable power buttons based on constraints
        if (addPowerButton != null)
            addPowerButton.interactable = stats.power < 4;
        
        if (removePowerButton != null)
            removePowerButton.interactable = stats.power > 0;
    }
    
    private Color GetRoleColor(VillagerRole role)
    {
        switch (role)
        {
            case VillagerRole.Captain:
                return captainColor;
            case VillagerRole.Mage:
                return mageColor;
            case VillagerRole.Farmer:
                return farmerColor;
            default:
                return normalRoleColor;
        }
    }
    
    private Color GetPowerColor(int power, int tier)
    {
        if (power >= 4)
            return maxPowerColor;
        else if (tier >= 2)
            return tier2PowerColor;
        else if (tier >= 1)
            return tier1PowerColor;
        else
            return noPowerColor;
    }
    
    private Color GetStatusColor(float value, bool higherIsBetter)
    {
        if (higherIsBetter)
        {
            if (value >= 0.8f)
                return excellentColor;
            else if (value >= 0.5f)
                return goodColor;
            else if (value >= 0.3f)
                return warningColor;
            else
                return dangerColor;
        }
        else
        {
            if (value <= 0.2f)
                return excellentColor;
            else if (value <= 0.5f)
                return goodColor;
            else if (value <= 0.7f)
                return warningColor;
            else
                return dangerColor;
        }
    }
    
    private float GetHealthPercent()
    {
        if (villagerHealth != null)
        {
            return villagerHealth.GetHealthPercentage(); // Use actual VillagerHealth method
        }
        
        return 1f; // Fallback
    }
    
    public void ShowCard()
    {
        gameObject.SetActive(true);
    }
    
    public void HideCard()
    {
        gameObject.SetActive(false);
        assignedVillager = null;
        villagerHealth = null;
        villagerCombat = null;
    }
    
    public bool HasVillager()
    {
        return assignedVillager != null;
    }
    
    public Villager GetVillager()
    {
        return assignedVillager;
    }
    
    /// <summary>
    /// Force a complete refresh of all card elements
    /// </summary>
    public void ForceRefresh()
    {
        if (assignedVillager != null)
        {
            UpdateCard(assignedVillager);
        }
    }
    
    /// <summary>
    /// Update only the power-related elements (for frequent updates)
    /// </summary>
    public void UpdatePowerOnly()
    {
        if (assignedVillager != null)
        {
            VillagerStats stats = assignedVillager.GetStats();
            UpdatePowerControls(stats);
            UpdateButtonStates(stats);
        }
    }
    
    /// <summary>
    /// Update only the status bars (for real-time updates during combat)
    /// </summary>
    public void UpdateStatusOnly()
    {
        if (assignedVillager != null)
        {
            VillagerStats stats = assignedVillager.GetStats();
            UpdateStatusBars(assignedVillager, stats);
            UpdateStatusIndicators(assignedVillager);
        }
    }
}