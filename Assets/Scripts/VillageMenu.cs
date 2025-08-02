using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class RulerSectionUI
{
    [Header("Ruler Stats")]
    public TextMeshProUGUI powerAllocatedText;
    public TextMeshProUGUI currentDamageText;
    public TextMeshProUGUI healthGainText;
    public Slider rulerPowerSlider;
    public Button addRulerPowerButton;
    public Button removeRulerPowerButton;
    
    [Header("Ruler Abilities")]
    public Button thunderDashButton;
    public Button beamOfOrderButton;
    public Button emperorsWrathButton;
    public TextMeshProUGUI thunderDashCostText;
    public TextMeshProUGUI beamCostText;
    public TextMeshProUGUI wrathCostText;
}

[System.Serializable]
public class VillageSummaryUI
{
    [Header("Production Summary")]
    public TextMeshProUGUI foodProductionText;
    public TextMeshProUGUI totalPowerText;
    public TextMeshProUGUI unallocatedPowerText;
    public TextMeshProUGUI villagerCountText;
    public TextMeshProUGUI rebelCountText;
    
    [Header("Visual Elements")]
    public Image foodProductionBar;
    public Image powerAllocationBar;
    public Color goodColor = Color.green;
    public Color warningColor = Color.yellow;
    public Color dangerColor = Color.red;
}

public class VillageMenu : MonoBehaviour
{    
    [Header("UI Sections")]
    [SerializeField] private RulerSectionUI rulerSection;
    [SerializeField] private VillageSummaryUI villageSummary;
    [SerializeField] private VillagerCard[] villagerCards;
    
    [Header("Card Configuration")]
    [SerializeField] private Transform villagerCardContainer;
    [SerializeField] private GameObject villagerCardPrefab;
    [SerializeField] private int maxVillagerCards = 8;
    
    [Header("Village System References")]
    [SerializeField] private VillageManager villageManager;
    
    [Header("Settings")]
    [SerializeField] private bool debugCardUpdates = false;
    [SerializeField] private float updateInterval = 0.1f; // Update frequency for status bars
    
    // Internal state
    private List<Villager> currentVillagers = new List<Villager>();
    private Dictionary<Villager, VillagerCard> villagerToCardMap = new Dictionary<Villager, VillagerCard>();
    private Coroutine statusUpdateCoroutine;
    private int playerAllocatedPower = 0;
    
    private void Start()
    {
        InitializeUI();
    }

    private void InitializeUI()
    {
        // SetupVillagerCards();
        SetupRulerSection();
    }

    // private void SetupVillagerCards()
    // {
    //     if (villagerCardContainer == null)
    //     {
    //         Debug.LogError("VillagerCardContainer is not assigned!");
    //         return;
    //     }

    //     currentVillagers = villageManager.GetVillagers();
    //     foreach (Villager villager in currentVillagers)
    //     {
    //         GameObject newCard = Instantiate(villagerCardPrefab, villagerCardContainer);
    //         VillagerCard currCard = newCard.GetComponent<VillagerCard>();
    //         currCard.SetAssignedVillager(villager);
    //         villagerToCardMap[villager] = currCard;
    //     }
    // }

    private int GetVillagerRank(Villager villager)
    {
        // Dead villagers get the highest rank to appear at the end.
        if (villager.GetComponent<VillagerHealth>().IsDead())
            return 100;

        // Define role priorities.
        switch (villager.GetRole())
        {
            case VillagerRole.Captain:
                return 0;
            case VillagerRole.Mage:
                return 1;
            case VillagerRole.Farmer:
                return 2;
            default:
                return 3;
        }
    }
    
    
    private void SetupRulerSection()
    {
        if (rulerSection.rulerPowerSlider != null)
            rulerSection.rulerPowerSlider.onValueChanged.AddListener(OnRulerPowerChanged);

        if (rulerSection.addRulerPowerButton != null)
            rulerSection.addRulerPowerButton.onClick.AddListener(() => ModifyRulerPower(1));

        if (rulerSection.removeRulerPowerButton != null)
            rulerSection.removeRulerPowerButton.onClick.AddListener(() => ModifyRulerPower(-1));
    }
    
    // private void UpdateVillagerCards()
    // {
    //     // Hide all cards first
    //     foreach (var cardUI in villagerCards)
    //     {
    //         cardUI.villagerCard.HideCard();
    //         cardUI.assignedVillager = null;
    //     }

    //     // Assign villagers to cards and update all data fields
    //     for (int i = 0; i < Mathf.Min(currentVillagers.Count, villagerCards.Length); i++)
    //     {
    //         Villager villager = currentVillagers[i];
    //         VillagerCardUI cardUI = villagerCards[i];
    //         cardUI.assignedVillager = villager;
    //         cardUI.cardIndex = i;
    //         cardUI.villagerCard.cardIndex = i;
    //         cardUI.villagerCard.UpdateCard(villager);
    //         villagerToCardMap[villager] = cardUI;
    //         if (debugCardUpdates)
    //             Debug.Log($"Updated card {i} for {villager.GetRole()} ({villager.name})");
    //     }
    // }
    
    private void UpdateVillageSummary()
    {
        if (villageSummary == null) return;
        
        // Food production - use actual VillageManager method
        int foodProduction = villageManager != null ? villageManager.CalculateFoodProduction() : 0;
        if (villageSummary.foodProductionText != null)
            villageSummary.foodProductionText.text = $"{foodProduction}";
        
        // Power totals - use actual PowerSystem if available
        int totalPower = GetTotalPower();
        int unallocatedPower = GetUnallocatedPower();
        
        if (villageSummary.totalPowerText != null)
            villageSummary.totalPowerText.text = $"{totalPower}";
        if (villageSummary.unallocatedPowerText != null)
            villageSummary.unallocatedPowerText.text = $"{unallocatedPower}";
        
        // Villager counts
        int totalVillagers = currentVillagers.Count;
        int rebelCount = currentVillagers.Count(v => v.IsRebel());
        
        if (villageSummary.villagerCountText != null)
            villageSummary.villagerCountText.text = $"{totalVillagers}";
        if (villageSummary.rebelCountText != null)
            villageSummary.rebelCountText.text = $"{rebelCount}";
        
        // Update visual bars
        UpdateSummaryBars(foodProduction, totalPower, unallocatedPower);
    }
    
    private void UpdateSummaryBars(int foodProduction, int totalPower, int unallocatedPower)
    {
        if (villageSummary.foodProductionBar != null)
        {
            float foodRatio = Mathf.Clamp01(foodProduction / 20f); // Assuming max 20 food
            villageSummary.foodProductionBar.fillAmount = foodRatio;
            villageSummary.foodProductionBar.color = GetSummaryBarColor(foodRatio);
        }
        
        if (villageSummary.powerAllocationBar != null)
        {
            float allocationRatio = totalPower > 0 ? (float)(totalPower - unallocatedPower) / totalPower : 0f;
            villageSummary.powerAllocationBar.fillAmount = allocationRatio;
            villageSummary.powerAllocationBar.color = GetSummaryBarColor(allocationRatio);
        }
    }
    
    private Color GetSummaryBarColor(float ratio)
    {
        if (ratio >= 0.7f)
            return villageSummary.goodColor;
        else if (ratio >= 0.4f)
            return villageSummary.warningColor;
        else
            return villageSummary.dangerColor;
    }
    
    private void UpdateRulerSection()
    {
        if (rulerSection == null) return;
        
        if (rulerSection.powerAllocatedText != null)
            rulerSection.powerAllocatedText.text = $"{playerAllocatedPower}";
        
        if (rulerSection.rulerPowerSlider != null)
            rulerSection.rulerPowerSlider.value = playerAllocatedPower;
        
        // Update ability costs and availability
        UpdateRulerAbilities();
    }
    
    private void UpdateRulerAbilities()
    {
        // Basic ability cost displays
        if (rulerSection.thunderDashCostText != null)
            rulerSection.thunderDashCostText.text = "Cost: 2";
        if (rulerSection.beamCostText != null)
            rulerSection.beamCostText.text = "Cost: 3";
        if (rulerSection.wrathCostText != null)
            rulerSection.wrathCostText.text = "Cost: 5";
    }
    
    private void OnRulerPowerChanged(float value)
    {
        int newPower = Mathf.RoundToInt(value);
        
        // Check if we have enough unallocated power
        int difference = newPower - playerAllocatedPower;
        if (difference > 0 && GetUnallocatedPower() < difference)
            return;
        
        playerAllocatedPower = newPower;
        UpdateVillageSummary();
    }
    
    private void ModifyRulerPower(int amount)
    {
        int newPower = Mathf.Max(0, playerAllocatedPower + amount);
        
        // Check if we have enough unallocated power for increases
        if (amount > 0 && GetUnallocatedPower() < amount)
            return;
        
        playerAllocatedPower = newPower;
        UpdateRulerSection();
        UpdateVillageSummary();
    }
    
    private int GetTotalPower()
    {
        return PowerSystem.Instance.GetTotalCommunalPower();
    }
    
    private int GetUnallocatedPower()
    {
        return PowerSystem.Instance.GetUnallocatedPower();
    }
    
    
    
    public void ShowUI()
    {
        gameObject.SetActive(true);
    }
    
    public void HideUI()
    {
        gameObject.SetActive(false);
    }
    
    
}