using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class VillagerCardUI
{
    [Header("Card Component")]
    public VillagerCard villagerCard;
    
    [HideInInspector]
    public Villager assignedVillager;
    [HideInInspector]
    public int cardIndex;
}

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

public class VillagePowerAllocationUI : MonoBehaviour
{
    public static VillagePowerAllocationUI Instance { get; private set; }
    
    [Header("UI Sections")]
    [SerializeField] private RulerSectionUI rulerSection;
    [SerializeField] private VillageSummaryUI villageSummary;
    [SerializeField] private VillagerCardUI[] villagerCards;
    
    [Header("Card Configuration")]
    [SerializeField] private Transform villagerCardContainer;
    [SerializeField] private int maxVillagerCards = 8;
    
    [Header("Village System References")]
    [SerializeField] private VillageManager villageManager;
    
    [Header("Settings")]
    [SerializeField] private bool debugCardUpdates = false;
    [SerializeField] private float updateInterval = 0.1f; // Update frequency for status bars
    
    // Internal state
    private List<Villager> currentVillagers = new List<Villager>();
    private Dictionary<Villager, VillagerCardUI> villagerToCardMap = new Dictionary<Villager, VillagerCardUI>();
    private Coroutine statusUpdateCoroutine;
    private int playerAllocatedPower = 0;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }
    
    private void Start()
    {
        InitializeUI();
    }
    
    private void OnEnable()
    {
        // Start status update coroutine for real-time updates
        if (statusUpdateCoroutine == null)
            statusUpdateCoroutine = StartCoroutine(StatusUpdateLoop());
    }
    
    private void OnDisable()
    {
        // Stop status update coroutine
        if (statusUpdateCoroutine != null)
        {
            StopCoroutine(statusUpdateCoroutine);
            statusUpdateCoroutine = null;
        }
    }
    
    private void InitializeUI()
    {
        if (villagerCards == null || villagerCards.Length == 0)
        {
            SetupCardsFromContainer();
        }
        else
        {
            SetupExistingCards();
        }
        
        SetupRulerSection();
        RefreshAllUI();
        
        if (debugCardUpdates)
            Debug.Log($"VillagePowerAllocationUI initialized with {villagerCards.Length} cards");
    }
    
    private void SetupCardsFromContainer()
    {
        if (villagerCardContainer == null) return;
        
        VillagerCard[] cardComponents = villagerCardContainer.GetComponentsInChildren<VillagerCard>(true);
        villagerCards = new VillagerCardUI[Mathf.Min(cardComponents.Length, maxVillagerCards)];
        
        for (int i = 0; i < villagerCards.Length; i++)
        {
            VillagerCard cardComponent = cardComponents[i];
            if (cardComponent == null)
            {
                Debug.LogWarning($"Missing VillagerCard component on card {i}");
                continue;
            }
            
            VillagerCardUI cardUI = new VillagerCardUI();
            cardUI.villagerCard = cardComponent;
            cardUI.cardIndex = i;
            
            cardComponent.OnPowerChanged += (card, newPower) => OnCardPowerChanged(i, newPower);
            villagerCards[i] = cardUI;
            cardComponent.HideCard();
        }
    }
    
    private void SetupExistingCards()
    {
        for (int i = 0; i < villagerCards.Length; i++)
        {
            if (villagerCards[i].villagerCard != null)
            {
                villagerCards[i].cardIndex = i;
                villagerCards[i].villagerCard.OnPowerChanged += (card, newPower) => OnCardPowerChanged(i, newPower);
            }
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
    
    #region Status Update Loop
    
    private System.Collections.IEnumerator StatusUpdateLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(updateInterval);
            
            // Update only status bars for performance
            UpdateVillagerCardStatus();
        }
    }
    
    private void UpdateVillagerCardStatus()
    {
        foreach (var cardUI in villagerCards)
        {
            if (cardUI.villagerCard.HasVillager())
            {
                cardUI.villagerCard.UpdateStatusOnly();
            }
        }
    }
    
    #endregion
    
    #region Public Interface
    
    public void RefreshAllUI()
    {
        RefreshVillagerList();
        UpdateVillagerCards();
        UpdateVillageSummary();
        UpdateRulerSection();
        
        if (debugCardUpdates)
            Debug.Log("RefreshAllUI completed");
    }
    
    public void UpdateVillagerCardsPowerOnly()
    {
        foreach (var cardUI in villagerCards)
        {
            if (cardUI.villagerCard.HasVillager())
            {
                cardUI.villagerCard.UpdatePowerOnly();
            }
        }
        
        UpdateVillageSummary();
    }
    
    public void ApplyAllPowerAllocations()
    {
        foreach (var cardUI in villagerCards)
        {
            if (cardUI.assignedVillager != null)
            {
                // Power allocations are already applied through the card events
                // This method can trigger additional processing if needed
                if (debugCardUpdates)
                    Debug.Log($"Applied power allocation for {cardUI.assignedVillager.GetRole()}");
            }
        }
    }
    
    #endregion
    
    private void RefreshVillagerList()
    {
        currentVillagers.Clear();
        villagerToCardMap.Clear();
        
        if (villageManager != null)
        {
            var allVillagers = villageManager.GetVillagers();
            currentVillagers.AddRange(allVillagers);
            
            if (debugCardUpdates)
                Debug.Log($"Found {currentVillagers.Count} villagers");
        }
    }
    
    private void UpdateVillagerCards()
    {
        // Hide all cards first
        foreach (var cardUI in villagerCards)
        {
            cardUI.villagerCard.HideCard();
            cardUI.assignedVillager = null;
        }

        // Assign villagers to cards and update all data fields
        for (int i = 0; i < Mathf.Min(currentVillagers.Count, villagerCards.Length); i++)
        {
            Villager villager = currentVillagers[i];
            VillagerCardUI cardUI = villagerCards[i];
            cardUI.assignedVillager = villager;
            cardUI.cardIndex = i;
            cardUI.villagerCard.cardIndex = i;
            cardUI.villagerCard.UpdateCard(villager);
            villagerToCardMap[villager] = cardUI;
            if (debugCardUpdates)
                Debug.Log($"Updated card {i} for {villager.GetRole()} ({villager.name})");
        }
    }
    
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
    
    #region Event Handlers
    
    private void OnCardPowerChanged(int cardIndex, int newPower)
    {
        if (cardIndex >= villagerCards.Length) return;
        
        VillagerCardUI cardUI = villagerCards[cardIndex];
        if (cardUI.assignedVillager == null) return;
        
        VillagerStats stats = cardUI.assignedVillager.GetStats();
        int difference = newPower - stats.power;
        
        // Check power constraints
        if (difference > 0 && GetUnallocatedPower() < difference) 
        {
            // Reset the card to current power (invalid change)
            cardUI.villagerCard.UpdateCard(cardUI.assignedVillager);
            return;
        }
        
        // Apply the change using existing Villager method
        cardUI.assignedVillager.AllocatePower(newPower);
        
        // Refresh UI
        UpdateVillagerCardsPowerOnly();
        
        if (debugCardUpdates)
            Debug.Log($"Power changed for {cardUI.assignedVillager.GetRole()}: {stats.power} -> {newPower}");
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
    
    #endregion
    
    #region Power Management
    
    public void SetVillagerPower(Villager villager, int power)
    {
        if (villagerToCardMap.ContainsKey(villager))
        {
            villager.AllocatePower(power);
            
            VillagerCardUI cardUI = villagerToCardMap[villager];
            cardUI.villagerCard.UpdateCard(villager);
            
            UpdateVillagerCardsPowerOnly();
            UpdateVillageSummary();
        }
    }
    
    private int GetTotalPower()
    {
        // Try to get from PowerSystem if available
        if (PowerSystem.Instance != null)
        {
            return PowerSystem.Instance.GetTotalCommunalPower();
        }
        
        // Fallback calculation
        return 100; // Default fallback value
    }
    
    private int GetUnallocatedPower()
    {
        int totalPower = GetTotalPower();
        int allocatedPower = playerAllocatedPower;
        
        // Add up all villager power allocations
        foreach (var villager in currentVillagers)
        {
            allocatedPower += villager.GetStats().power;
        }
        
        return Mathf.Max(0, totalPower - allocatedPower);
    }
    
    #endregion
    
    #region UI Show/Hide
    
    public void ShowUI()
    {
        gameObject.SetActive(true);
        RefreshAllUI();
    }
    
    public void HideUI()
    {
        gameObject.SetActive(false);
    }
    
    #endregion
}