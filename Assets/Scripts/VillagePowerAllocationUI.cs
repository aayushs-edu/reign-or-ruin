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
    [Header("UI Sections")]
    [SerializeField] private RulerSectionUI rulerSection;
    [SerializeField] private VillageSummaryUI villageSummary;
    [SerializeField] private VillagerCardUI[] villagerCards;
    
    [Header("Card Configuration")]
    [SerializeField] private Transform villagerCardContainer;
    [SerializeField] private GameObject villagerCardPrefab; // Should have VillagerCard component
    [SerializeField] private int maxVillagerCards = 8;
    [SerializeField] private int powerIncrement = 2;
    
    [Header("Power Requirements")]
    [SerializeField] private int thunderDashRequirement = 35;
    [SerializeField] private int beamOfOrderRequirement = 250;
    [SerializeField] private int emperorsWrathRequirement = 500;
    
    [Header("References")]
    [SerializeField] private VillageManager villageManager;
    [SerializeField] private PowerSystem powerSystem;
    
    // Internal state
    private List<Villager> currentVillagers = new List<Villager>();
    private Dictionary<Villager, VillagerCardUI> villagerToCardMap = new Dictionary<Villager, VillagerCardUI>();
    private int playerAllocatedPower = 0;
    private bool isUIActive = false;
    
    public static VillagePowerAllocationUI Instance { get; private set; }
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    
    private void Start()
    {
        InitializeReferences();
        SetupEventListeners();
        InitializeCards();
        
        // Start inactive
        gameObject.SetActive(false);
    }
    
    private void InitializeReferences()
    {
        if (villageManager == null)
            villageManager = FindObjectOfType<VillageManager>();
        if (powerSystem == null)
            powerSystem = FindObjectOfType<PowerSystem>();
    }
    
    private void SetupEventListeners()
    {
        // Listen to day/night cycle
        if (DayNightCycleManager.Instance != null)
        {
            DayNightCycleManager.Instance.OnDayStarted += ShowUI;
            DayNightCycleManager.Instance.OnNightStarted += HideUI;
        }
        
        // Setup ruler section buttons
        if (rulerSection.addRulerPowerButton != null)
            rulerSection.addRulerPowerButton.onClick.AddListener(() => ModifyRulerPower(powerIncrement));
        if (rulerSection.removeRulerPowerButton != null)
            rulerSection.removeRulerPowerButton.onClick.AddListener(() => ModifyRulerPower(-powerIncrement));
            
        // Setup ability buttons
        if (rulerSection.thunderDashButton != null)
            rulerSection.thunderDashButton.onClick.AddListener(() => AllocateToAbility("ThunderDash", thunderDashRequirement));
        if (rulerSection.beamOfOrderButton != null)
            rulerSection.beamOfOrderButton.onClick.AddListener(() => AllocateToAbility("BeamOfOrder", beamOfOrderRequirement));
        if (rulerSection.emperorsWrathButton != null)
            rulerSection.emperorsWrathButton.onClick.AddListener(() => AllocateToAbility("EmperorsWrath", emperorsWrathRequirement));
    }
    
    private void InitializeCards()
    {
        // Initialize existing cards or create new ones
        if (villagerCards == null || villagerCards.Length == 0)
        {
            CreateVillagerCards();
        }
        else
        {
            SetupExistingCards();
        }
    }
    
    private void CreateVillagerCards()
    {
        if (villagerCardPrefab == null || villagerCardContainer == null) return;
        
        villagerCards = new VillagerCardUI[maxVillagerCards];
        
        for (int i = 0; i < maxVillagerCards; i++)
        {
            GameObject cardObj = Instantiate(villagerCardPrefab, villagerCardContainer);
            VillagerCard cardComponent = cardObj.GetComponent<VillagerCard>();
            
            if (cardComponent == null)
            {
                Debug.LogError($"VillagerCard prefab missing VillagerCard component! {cardObj.name}");
                continue;
            }
            
            VillagerCardUI cardUI = new VillagerCardUI();
            cardUI.villagerCard = cardComponent;
            cardUI.cardIndex = i;
            
            // Setup event listeners
            cardComponent.OnPowerChanged += (card, newPower) => OnCardPowerChanged(i, newPower);
            
            villagerCards[i] = cardUI;
            
            // Initially hide all cards
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
                
                // Setup event listeners
                villagerCards[i].villagerCard.OnPowerChanged += (card, newPower) => OnCardPowerChanged(i, newPower);
            }
        }
    }
    
    private void OnCardPowerChanged(int cardIndex, int newPower)
    {
        if (cardIndex >= villagerCards.Length) return;
        
        VillagerCardUI cardUI = villagerCards[cardIndex];
        if (cardUI.assignedVillager == null) return;
        
        VillagerStats stats = cardUI.assignedVillager.GetStats();
        int difference = newPower - stats.power;
        
        // Check if we have enough unallocated power for increases
        if (difference > 0 && GetUnallocatedPower() < difference) 
        {
            // Reset the card to current power (invalid change)
            cardUI.villagerCard.UpdateCard(cardUI.assignedVillager);
            return;
        }
        
        // Apply the change
        cardUI.assignedVillager.AllocatePower(newPower);
        
        // Refresh all UI
        RefreshAllUI();
    }
    
    #region UI Show/Hide
    
    private void ShowUI()
    {
        isUIActive = true;
        gameObject.SetActive(true);
        RefreshAllUI();
    }
    
    private void HideUI()
    {
        isUIActive = false;
        gameObject.SetActive(false);
    }
    
    #endregion
    
    #region UI Population
    
    public void RefreshAllUI()
    {
        if (!isUIActive) return;
        
        PopulateVillagerCards();
        UpdateRulerSection();
        UpdateVillageSummary();
        UpdateAllCardConstraints();
    }
    
    private void PopulateVillagerCards()
    {
        // Get all villagers in scene
        Villager[] allVillagers = FindObjectsOfType<Villager>();
        currentVillagers.Clear();
        villagerToCardMap.Clear();
        
        // Sort villagers: Captain, Mage, Farmer first, then others
        var sortedVillagers = allVillagers.OrderBy(v => GetRolePriority(v.GetRole())).ToArray();
        
        // Populate cards
        for (int i = 0; i < villagerCards.Length; i++)
        {
            if (i < sortedVillagers.Length)
            {
                SetupVillagerCard(villagerCards[i], sortedVillagers[i]);
                currentVillagers.Add(sortedVillagers[i]);
                villagerToCardMap[sortedVillagers[i]] = villagerCards[i];
            }
            else
            {
                // Hide unused cards
                villagerCards[i].villagerCard.HideCard();
                villagerCards[i].assignedVillager = null;
            }
        }
    }
    
    private int GetRolePriority(VillagerRole role)
    {
        switch (role)
        {
            case VillagerRole.Captain: return 0;
            case VillagerRole.Mage: return 1;
            case VillagerRole.Farmer: return 2;
            case VillagerRole.Builder: return 3;
            case VillagerRole.Commoner: return 4;
            default: return 5;
        }
    }
    
    private void SetupVillagerCard(VillagerCardUI cardUI, Villager villager)
    {
        cardUI.assignedVillager = villager;
        
        // Update the card component with villager data
        cardUI.villagerCard.UpdateCard(villager);
        
        // Update power constraints based on available power
        int unallocatedPower = GetUnallocatedPower();
        VillagerStats stats = villager.GetStats();
        
        bool canAddPower = stats.power < 4 && unallocatedPower >= powerIncrement;
        bool canRemovePower = stats.power > 0;
        
        cardUI.villagerCard.SetPowerConstraints(canAddPower, canRemovePower);
    }
    
    #endregion
    
    #region Ruler Section
    
    private void UpdateRulerSection()
    {
        if (rulerSection.powerAllocatedText != null)
            rulerSection.powerAllocatedText.text = $"{playerAllocatedPower}";
            
        if (rulerSection.currentDamageText != null)
        {
            float damage = CalculatePlayerDamage(playerAllocatedPower);
            rulerSection.currentDamageText.text = $"{damage:F1}";
        }
        
        if (rulerSection.healthGainText != null)
        {
            float healthGain = CalculatePlayerHealthGain(playerAllocatedPower);
            rulerSection.healthGainText.text = $"+{healthGain:F1} / night";
        }
        
        if (rulerSection.rulerPowerSlider != null)
        {
            rulerSection.rulerPowerSlider.value = playerAllocatedPower;
        }
        
        // Update ability buttons
        UpdateAbilityButtons();
        
        // Update ruler power buttons
        bool canAddRulerPower = GetUnallocatedPower() >= powerIncrement;
        bool canRemoveRulerPower = playerAllocatedPower > 0;
        
        if (rulerSection.addRulerPowerButton != null)
            rulerSection.addRulerPowerButton.interactable = canAddRulerPower;
        if (rulerSection.removeRulerPowerButton != null)
            rulerSection.removeRulerPowerButton.interactable = canRemoveRulerPower;
    }
    
    private void UpdateAbilityButtons()
    {
        int totalPlayerPower = playerAllocatedPower; // This should include accumulated power
        
        if (rulerSection.thunderDashButton != null)
        {
            rulerSection.thunderDashButton.interactable = totalPlayerPower >= thunderDashRequirement;
            if (rulerSection.thunderDashCostText != null)
                rulerSection.thunderDashCostText.text = $"Cost: {thunderDashRequirement}";
        }
        
        if (rulerSection.beamOfOrderButton != null)
        {
            rulerSection.beamOfOrderButton.interactable = totalPlayerPower >= beamOfOrderRequirement;
            if (rulerSection.beamCostText != null)
                rulerSection.beamCostText.text = $"Cost: {beamOfOrderRequirement}";
        }
        
        if (rulerSection.emperorsWrathButton != null)
        {
            rulerSection.emperorsWrathButton.interactable = totalPlayerPower >= emperorsWrathRequirement;
            if (rulerSection.wrathCostText != null)
                rulerSection.wrathCostText.text = $"Cost: {emperorsWrathRequirement}";
        }
    }
    
    private float CalculatePlayerDamage(int power)
    {
        return 10f + (power * 2f); // Base damage + power scaling
    }
    
    private float CalculatePlayerHealthGain(int power)
    {
        return 5f + (power * 1.5f); // Base health + power scaling
    }
    
    #endregion
    
    #region Village Summary
    
    private void UpdateVillageSummary()
    {
        // Food production
        int foodProduction = villageManager != null ? villageManager.CalculateFoodProduction() : 0;
        if (villageSummary.foodProductionText != null)
            villageSummary.foodProductionText.text = $"{foodProduction}";
        
        // Power totals
        int totalPower = GetTotalPower();
        int unallocatedPower = GetUnallocatedPower();
        
        if (villageSummary.totalPowerText != null)
            villageSummary.totalPowerText.text = $"{totalPower}";
        if (villageSummary.unallocatedPowerText != null)
            villageSummary.unallocatedPowerText.text = $"{unallocatedPower}";
        
        // Villager counts
        int totalVillagers = currentVillagers.Count;
        int rebelCount = currentVillagers.Count(v => !v.IsLoyal());
        
        if (villageSummary.villagerCountText != null)
            villageSummary.villagerCountText.text = $"{totalVillagers}";
        if (villageSummary.rebelCountText != null)
        {
            villageSummary.rebelCountText.text = $"{rebelCount}";;
        }
        
        // Update visual bars
        if (villageSummary.foodProductionBar != null)
        {
            float foodRatio = Mathf.Clamp01(foodProduction / 20f); // Assuming max 20 food
            villageSummary.foodProductionBar.fillAmount = foodRatio;
            villageSummary.foodProductionBar.color = foodRatio >= 0.5f ? villageSummary.goodColor : villageSummary.warningColor;
        }
        
        if (villageSummary.powerAllocationBar != null)
        {
            float allocationRatio = totalPower > 0 ? (float)(totalPower - unallocatedPower) / totalPower : 0f;
            villageSummary.powerAllocationBar.fillAmount = allocationRatio;
        }
    }
    
    #endregion
    
    #region Power Management
    
    public void SetVillagerPower(Villager villager, int power)
    {
        if (villagerToCardMap.ContainsKey(villager))
        {
            villager.AllocatePower(power);
            
            // Update the specific card
            VillagerCardUI cardUI = villagerToCardMap[villager];
            cardUI.villagerCard.UpdateCard(villager);
            
            // Update power constraints for all cards
            UpdateAllCardConstraints();
            
            // Refresh summary
            UpdateVillageSummary();
        }
    }
    
    private void UpdateAllCardConstraints()
    {
        int unallocatedPower = GetUnallocatedPower();
        
        foreach (var cardUI in villagerCards)
        {
            if (cardUI.villagerCard.HasVillager())
            {
                VillagerStats stats = cardUI.assignedVillager.GetStats();
                
                bool canAddPower = stats.power < 4 && unallocatedPower >= powerIncrement;
                bool canRemovePower = stats.power > 0;
                
                cardUI.villagerCard.SetPowerConstraints(canAddPower, canRemovePower);
            }
        }
    }
    
    private void ModifyRulerPower(int amount)
    {
        int newPower = Mathf.Max(0, playerAllocatedPower + amount);
        
        if (amount > 0 && GetUnallocatedPower() < amount) return;
        
        playerAllocatedPower = newPower;
        RefreshAllUI();
    }
    
    private void AllocateToAbility(string abilityName, int cost)
    {
        if (playerAllocatedPower >= cost)
        {
            // This should unlock the ability
            Debug.Log($"Unlocked ability: {abilityName} for {cost} power");
            // Connect to your player ability system here
        }
    }
    
    #endregion
    
    #region Power Calculations
    
    private int GetTotalPower()
    {
        if (powerSystem != null)
            return powerSystem.GetTotalCommunalPower();
        return 100; // Fallback value
    }
    
    private int GetUnallocatedPower()
    {
        int totalPower = GetTotalPower();
        int allocatedPower = playerAllocatedPower;
        
        foreach (var villager in currentVillagers)
        {
            allocatedPower += villager.GetStats().power;
        }
        
        return totalPower - allocatedPower;
    }
    
    #endregion
    
    #region Public Interface
    
    public void SetPlayerPower(int power)
    {
        playerAllocatedPower = power;
        RefreshAllUI();
    }
    
    public bool IsUIActive() => isUIActive;
    
    #endregion
    
    private void OnDestroy()
    {
        // Clean up event listeners
        if (DayNightCycleManager.Instance != null)
        {
            DayNightCycleManager.Instance.OnDayStarted -= ShowUI;
            DayNightCycleManager.Instance.OnNightStarted -= HideUI;
        }
        
        // Clean up card event listeners
        if (villagerCards != null)
        {
            foreach (var cardUI in villagerCards)
            {
                if (cardUI.villagerCard != null)
                {
                    cardUI.villagerCard.OnPowerChanged -= (card, newPower) => OnCardPowerChanged(cardUI.cardIndex, newPower);
                }
            }
        }
    }
}