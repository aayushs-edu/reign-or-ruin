using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class VillageManager : MonoBehaviour
{
    [Header("Village Configuration")]
    [SerializeField] private List<Villager> villagers = new List<Villager>();
    [SerializeField] private Settings settings;
    [SerializeField] private int totalFoodProduction = 8; // Base food production

    [Header("Power Allocation")]
    [SerializeField] private int availablePower = 0;
    [SerializeField] private int playerPower = 0;

    [Header("Rebellion System")]
    [SerializeField] private float rebellionCheckInterval = 0.5f;
    [SerializeField] private bool debugRebellion = true;

    [Header("Night Cycle")]
    [SerializeField] private bool isNightTime = false;
    [SerializeField] private float nightDuration = 30f;
    [SerializeField] private bool useExternalDayNightControl = true;

    // Events
    public System.Action<Villager> OnVillagerRebel;
    public System.Action<List<Villager>> OnMassRebellion;
    public System.Action<int> OnFoodProductionChanged;
    public System.Action<bool> OnDayNightCycle;

    // Internal state
    private float lastRebellionCheck;
    private int totalVillagers;
    private int rebelCount = 0;

    private MassRebellionUI rebellionUI;

    // Singleton for easy access
    public static VillageManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        rebellionUI = GetComponent<MassRebellionUI>();
    }

    private void Start()
    {
        InitializeVillage();
        SetupEventListeners();
    }

    private void Update()
    {
        if (Time.time - lastRebellionCheck >= rebellionCheckInterval)
        {
            CheckForRebellions();
            lastRebellionCheck = Time.time;
        }
    }

    private void InitializeVillage()
    {
        // Auto-find villagers if list is empty
        if (villagers.Count == 0)
        {
            villagers = FindObjectsOfType<Villager>().ToList();
        }

        totalVillagers = villagers.Count;

        // Set discontent constants for all villagers
        foreach (var villager in villagers)
        {
            // Assign discontent constants through reflection or public setter
            // villager.Setsettings(settings);
        }

        Debug.Log($"VillageManager initialized with {totalVillagers} villagers");

        // Initial food distribution
        DistributeFood();
    }

    private void SetupEventListeners()
    {
        foreach (var villager in villagers)
        {
            villager.OnVillagerRebel += HandleVillagerRebellion;
            villager.OnVillagerDeath += HandleVillagerDeath;
            villager.OnDiscontentChanged += HandleDiscontentChanged;
        }
    }

    public void AllocatePowerToVillagers(Dictionary<Villager, int> powerAllocations)
    {
        foreach (var allocation in powerAllocations)
        {
            if (villagers.Contains(allocation.Key))
            {
                allocation.Key.AllocatePower(allocation.Value);
            }
        }

        // Process discontent after allocation
        foreach (var villager in villagers)
        {
            villager.ProcessDiscontentAtAllocation();
        }

        Debug.Log("Power allocated to all villagers");
    }

    public void DistributeFood()
    {
        // Calculate total food production from farmers
        int totalFood = CalculateFoodProduction();

        // Distribute food evenly among all villagers
        float foodPerVillager = villagers.Count > 0 ? (float)totalFood / villagers.Count : 0f;

        foreach (var villager in villagers)
        {
            villager.SetFoodLevel(Mathf.Min(1f, foodPerVillager));
        }

        OnFoodProductionChanged?.Invoke(totalFood);

        Debug.Log($"Distributed {totalFood} food among {villagers.Count} villagers ({foodPerVillager:F2} per villager)");
    }

    public int CalculateFoodProduction()
    {
        int totalFood = 0;

        foreach (var villager in villagers)
        {
            if (villager.GetRole() == VillagerRole.Farmer && villager.IsActive())
            {
                // Get farmer combat component to calculate food production
                FarmerCombat farmerCombat = villager.GetComponent<FarmerCombat>();
                if (farmerCombat != null)
                {
                    totalFood += farmerCombat.CalculateFoodProduction();
                }
                else
                {
                    // Fallback to default calculation if FarmerCombat component is missing
                    VillagerStats stats = villager.GetStats();
                    switch (stats.tier)
                    {
                        case 0:
                            totalFood += settings != null ? settings.farmerBaseFoodProduction : 2;
                            break;
                        case 1:
                            totalFood += settings != null ? settings.farmerTier1FoodProduction : 4;
                            break;
                        case 2:
                            totalFood += settings != null ? settings.farmerTier2FoodProduction : 8;
                            break;
                    }
                }

                if (debugRebellion) // Using existing debug flag
                {
                    Debug.Log($"Farmer {villager.name} (Tier {villager.GetStats().tier}) producing {(farmerCombat != null ? farmerCombat.CalculateFoodProduction() : "fallback")} food");
                }
            }
        }

        return totalFood;
    }

    public void ProcessNightCycle()
    {
        isNightTime = true;
        OnDayNightCycle?.Invoke(true);

        // Distribute food
        DistributeFood();

        // Process nightly recovery for all villagers
        foreach (var villager in villagers)
        {
            villager.ProcessNightlyRecovery();
        }

        Debug.Log("Night cycle processed - food distributed and discontent recovery applied");

        // Only auto-end night if not using external control
        if (!useExternalDayNightControl)
        {
            Invoke(nameof(ProcessDayCycle), nightDuration);
        }
    }

    public void ProcessDayCycle()
    {
        isNightTime = false;
        OnDayNightCycle?.Invoke(false);

        Debug.Log("Day cycle started");

        // Process any day-start effects here
        // Food distribution already happened at night end
        // Power allocation UI will be handled by DayNightCycleManager
    }

    /// <summary>
    /// Apply power allocations made during the day - called by DayNightCycleManager
    /// </summary>
    public void ApplyDayEndAllocations()
    {
        // Process discontent calculations for all villagers based on their allocated power
        foreach (var villager in villagers)
        {
            villager.ProcessDiscontentAtAllocation();
        }

        Debug.Log("Applied day-end power allocations and calculated discontent");
    }

    /// <summary>
    /// Get villager composition for wave preview
    /// </summary>
    public Dictionary<VillagerRole, int> GetVillagerComposition()
    {
        var composition = new Dictionary<VillagerRole, int>();

        foreach (var villager in villagers)
        {
            if (villager.IsLoyal()) // Only count loyal villagers
            {
                VillagerRole role = villager.GetRole();
                if (composition.ContainsKey(role))
                    composition[role]++;
                else
                    composition[role] = 1;
            }
        }

        return composition;
    }

    /// <summary>
    /// Set whether to use external day/night control
    /// </summary>
    public void SetExternalDayNightControl(bool external)
    {
        useExternalDayNightControl = external;

        if (external)
        {
            // Cancel any pending day cycle invocations
            CancelInvoke(nameof(ProcessDayCycle));
        }
    }

    /// <summary>
    /// Get detailed village stats for UI display
    /// </summary>
    public VillageStats GetVillageStats()
    {
        return new VillageStats
        {
            totalVillagers = villagers.Count,
            loyalVillagers = GetLoyalCount(),
            rebelVillagers = GetRebelCount(),
            foodProduction = CalculateFoodProduction(),
            averageDiscontent = CalculateAverageDiscontent(),
            isNightTime = isNightTime
        };
    }

    private float CalculateAverageDiscontent()
    {
        if (villagers.Count == 0) return 0f;

        float totalDiscontent = 0f;
        foreach (var villager in villagers)
        {
            totalDiscontent += villager.GetStats().discontent;
        }

        return totalDiscontent / villagers.Count;
    }

    private void CheckForRebellions()
    {
        // This is called periodically to check for immediate rebellions
        // The actual rebellion trigger is handled in individual villagers
        // Here we handle cascade effects
    }

    private void HandleVillagerRebellion(Villager rebelliousVillager)
    {
        rebelCount++;
        OnVillagerRebel?.Invoke(rebelliousVillager);

        if (debugRebellion)
        {
            Debug.Log($"Villager rebellion: {rebelliousVillager.GetRole()} {rebelliousVillager.name}");
        }

        // Check for cascade effects
        CheckCascadeRebellion(rebelliousVillager);

        // NEW: Additional check for witness-triggered mass rebellions
        int rebelsAfterCascade = villagers.Count(v => v.IsRebel());
        float rebelPercentAfterCascade = (float)rebelsAfterCascade / villagers.Count;

        // If witness deaths caused enough rebellions to trigger mass rebellion UI
        if ((rebelsAfterCascade >= settings.massRebellionMinCount ||
            rebelPercentAfterCascade >= settings.massRebellionMinPercent))
        {
            MassRebellionUI rebellionUI = GetComponent<MassRebellionUI>();
            if (rebellionUI != null && !rebellionUI.IsPlaying()) // Don't trigger if already playing
            {
                TriggerMassRebellionUI();

                if (debugRebellion)
                {
                    Debug.Log($"Witness-triggered mass rebellion reached threshold: {rebelsAfterCascade} rebels ({rebelPercentAfterCascade:P0})");
                }
            }
        }
    }

    private void CheckCascadeRebellion(Villager triggerVillager)
    {
        if (settings == null) return;

        List<Villager> newRebels = new List<Villager>();

        // Captain cascade: If Captain rebels, all Commoners with 50+ discontent rebel
        if (triggerVillager.GetRole() == VillagerRole.Captain)
        {
            foreach (var villager in villagers)
            {
                if (villager.GetRole() == VillagerRole.Commoner &&
                    villager.IsLoyal() &&
                    villager.GetStats().discontent >= settings.captainCascadeThreshold)
                {
                    villager.SetDiscontent(100f); // Force rebellion
                    newRebels.Add(villager);
                }
            }

            if (debugRebellion && newRebels.Count > 0)
            {
                Debug.Log($"Captain rebellion triggered {newRebels.Count} commoner rebellions!");
            }
        }

        // Mass rebellion: If 3+ rebels or 40%+ villagers rebel, others with 80+ discontent join
        int currentRebels = villagers.Count(v => v.IsRebel());
        float rebelPercent = (float)currentRebels / villagers.Count;

        if (currentRebels >= settings.massRebellionMinCount ||
            rebelPercent >= settings.massRebellionMinPercent)
        {
            foreach (var villager in villagers)
            {
                if (villager.IsLoyal() &&
                    villager.GetStats().discontent >= settings.massRebellionThreshold)
                {
                    villager.SetDiscontent(100f); // Force rebellion
                    newRebels.Add(villager);
                }
            }

            if (debugRebellion && newRebels.Count > 0)
            {
                Debug.Log($"Mass rebellion triggered! {newRebels.Count} additional villagers joined the revolt!");
                OnMassRebellion?.Invoke(newRebels);

                // NEW: Trigger Mass Rebellion UI Animation
                TriggerMassRebellionUI();
            }
        }
    }

    // NEW: Add this method to VillageManager.cs
    private void TriggerMassRebellionUI()
    {
        if (rebellionUI != null)
        {
            rebellionUI.TriggerMassRebellion();

            if (debugRebellion)
            {
                Debug.Log("VillageManager: Triggered Mass Rebellion UI animation");
            }
        }
        else
        {
            Debug.LogWarning("VillageManager: MassRebellionUI component not found on this GameObject!");
        }
    }

    private void HandleVillagerDeath(Villager deadVillager)
    {
        villagers.Remove(deadVillager);

        if (deadVillager.IsRebel())
        {
            rebelCount--;
        }

        Debug.Log($"Villager died: {deadVillager.GetRole()} {deadVillager.name}. Remaining: {villagers.Count}");

        // Redistribute food after villager count change
        DistributeFood();
    }

    private void HandleDiscontentChanged(Villager villager, float newDiscontent)
    {
        // You could add analytics or warnings here
        if (newDiscontent >= 80f && debugRebellion)
        {
            Debug.LogWarning($"{villager.GetRole()} {villager.name} discontent at {newDiscontent:F0}% - rebellion imminent!");
        }
    }

    // Public getters for UI and other systems
    public List<Villager> GetVillagers() => villagers;
    public List<Villager> GetLoyalVillagers() => villagers.Where(v => v.IsLoyal()).ToList();
    public List<Villager> GetRebelVillagers() => villagers.Where(v => v.IsRebel()).ToList();
    public int GetTotalVillagers() => villagers.Count;
    public int GetLoyalCount() => villagers.Count(v => v.IsLoyal());
    public int GetRebelCount() => villagers.Count(v => v.IsRebel());
    public float GetRebelPercentage() => villagers.Count > 0 ? (float)GetRebelCount() / villagers.Count : 0f;
    public bool IsNightTime() => isNightTime;

    // Debug and testing methods
    public void ForceRebellion(Villager villager)
    {
        if (villager != null && villager.IsLoyal())
        {
            villager.SetDiscontent(100f);
        }
    }

    public void AddDiscontentToAll(float amount)
    {
        foreach (var villager in villagers)
        {
            villager.AddDiscontent(amount);
        }
    }

    public void SetAllFood(float foodLevel)
    {
        foreach (var villager in villagers)
        {
            villager.SetFoodLevel(foodLevel);
        }
    }
}

// ADD this data structure:
[System.Serializable]
public struct VillageStats
{
    public int totalVillagers;
    public int loyalVillagers;
    public int rebelVillagers;
    public int foodProduction;
    public float averageDiscontent;
    public bool isNightTime;
}