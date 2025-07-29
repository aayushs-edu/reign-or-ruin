using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class VillageManager : MonoBehaviour
{
    [Header("Village Configuration")]
    [SerializeField] private List<Villager> villagers = new List<Villager>();
    [SerializeField] private DiscontentConstants discontentConstants;
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
    
    // Events
    public System.Action<Villager> OnVillagerRebel;
    public System.Action<List<Villager>> OnMassRebellion;
    public System.Action<int> OnFoodProductionChanged;
    public System.Action<bool> OnDayNightCycle;
    
    // Internal state
    private float lastRebellionCheck;
    private int totalVillagers;
    private int rebelCount = 0;
    
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
            // villager.SetDiscontentConstants(discontentConstants);
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
    
    private int CalculateFoodProduction()
    {
        int totalFood = 0;
        
        foreach (var villager in villagers)
        {
            if (villager.GetRole() == VillagerRole.Farmer && villager.IsActive())
            {
                VillagerStats stats = villager.GetStats();
                
                // Base production + tier bonus
                switch (stats.tier)
                {
                    case 0: // No power
                        totalFood += 2; // Base production
                        break;
                    case 1: // Tier 1 (2 power)
                        totalFood += 4;
                        break;
                    case 2: // Tier 2 (4 power)
                        totalFood += 8;
                        break;
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
        
        // Automatically end night after duration (for testing)
        Invoke(nameof(ProcessDayCycle), nightDuration);
    }
    
    public void ProcessDayCycle()
    {
        isNightTime = false;
        OnDayNightCycle?.Invoke(false);
        
        Debug.Log("Day cycle started");
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
    }
    
    private void CheckCascadeRebellion(Villager triggerVillager)
    {
        if (discontentConstants == null) return;
        
        List<Villager> newRebels = new List<Villager>();
        
        // Captain cascade: If Captain rebels, all Commoners with 50+ discontent rebel
        if (triggerVillager.GetRole() == VillagerRole.Captain)
        {
            foreach (var villager in villagers)
            {
                if (villager.GetRole() == VillagerRole.Commoner && 
                    villager.IsLoyal() && 
                    villager.GetStats().discontent >= discontentConstants.captainCascadeThreshold)
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
        
        if (currentRebels >= discontentConstants.massRebellionMinCount || 
            rebelPercent >= discontentConstants.massRebellionMinPercent)
        {
            foreach (var villager in villagers)
            {
                if (villager.IsLoyal() && 
                    villager.GetStats().discontent >= discontentConstants.massRebellionThreshold)
                {
                    villager.SetDiscontent(100f); // Force rebellion
                    newRebels.Add(villager);
                }
            }
            
            if (debugRebellion && newRebels.Count > 0)
            {
                Debug.Log($"Mass rebellion triggered! {newRebels.Count} additional villagers joined the revolt!");
                OnMassRebellion?.Invoke(newRebels);
            }
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
    
    // Test method for allocation phase
    public void TestPowerAllocation()
    {
        Dictionary<Villager, int> testAllocation = new Dictionary<Villager, int>();
        
        foreach (var villager in villagers)
        {
            // Give random power for testing
            int randomPower = Random.Range(0, 5);
            testAllocation[villager] = randomPower;
        }
        
        AllocatePowerToVillagers(testAllocation);
    }
}