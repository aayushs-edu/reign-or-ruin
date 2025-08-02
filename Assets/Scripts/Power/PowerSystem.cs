using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class PowerHolder
{
    public string holderName;
    public int currentPower;
    public int maxPower;
    public bool isPlayer;
    public Transform holderTransform; // Reference to the villager/player
    
    public PowerHolder(string name, int maxPow, bool player = false)
    {
        holderName = name;
        maxPower = maxPow;
        currentPower = 0;
        isPlayer = player;
    }
    
    public float GetPowerPercentage() => maxPower > 0 ? (float)currentPower / maxPower : 0f;
    public bool IsFull() => currentPower >= maxPower;
    public int GetAvailableSpace() => maxPower - currentPower;
}

public class PowerSystem : MonoBehaviour
{
    [Header("Power Configuration")]
    [SerializeField] private int totalCommunalPower = 0;
    [SerializeField] private int playerMaxPower = 50;
    [SerializeField] private int villagerMaxPower = 30;
    
    [Header("Power Distribution")]
    [SerializeField] private float powerShareRadius = 5f;
    [SerializeField] private float autoDistributionRate = 2f; // Power per second when near villagers
    [SerializeField] private bool enableAutoDistribution = true;
    
    [Header("Rebellion System")]
    [SerializeField] private float greedThreshold = 0.7f; // Rebellion chance starts when player has 70% of total power
    [SerializeField] private float maxRebellionChance = 0.3f; // 30% max chance
    [SerializeField] private float rebellionCheckInterval = 5f;
    
    [Header("Debug")]
    [SerializeField] private bool debugMode = false;
    
    // Power holders
    private PowerHolder playerPower;
    private List<PowerHolder> villagerPowers = new List<PowerHolder>();
    private List<Transform> villagerTransforms = new List<Transform>();
    
    // Events
    public System.Action<int> OnTotalPowerChanged;
    public System.Action<PowerHolder> OnPlayerPowerChanged;
    public System.Action<PowerHolder> OnVillagerPowerChanged;
    public System.Action<float> OnRebellionRiskChanged; // 0-1 risk level
    
    // References
    private Transform playerTransform;
    private float lastRebellionCheck;
    private float lastAutoDistribution;
    
    // Singleton
    public static PowerSystem Instance { get; private set; }
    
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
        InitializePowerSystem();
    }
    
    private void Update()
    {
        if (enableAutoDistribution)
        {
            HandleAutoDistribution();
        }
        
        HandleRebellionCheck();
    }
    
    private void InitializePowerSystem()
    {
        // Find player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
            playerPower = new PowerHolder("Player", playerMaxPower, true);
            playerPower.holderTransform = playerTransform;
        }
        
        // Find villagers (you can tag them or find by component)
        FindVillagers();
        
        if (debugMode)
        {
            Debug.Log($"PowerSystem initialized: Player + {villagerPowers.Count} villagers");
        }
    }
    
    private void FindVillagers()
    {
        // Find all GameObjects tagged as "Villager" 
        GameObject[] villagers = GameObject.FindGameObjectsWithTag("Villager");
        
        for (int i = 0; i < villagers.Length; i++)
        {
            PowerHolder villager = new PowerHolder($"Villager {i + 1}", villagerMaxPower);
            villager.holderTransform = villagers[i].transform;
            villagerPowers.Add(villager);
            villagerTransforms.Add(villagers[i].transform);
        }
    }
    
    public void AddPowerFromEnemy(int powerAmount)
    {
        totalCommunalPower += powerAmount;
        OnTotalPowerChanged?.Invoke(totalCommunalPower);
        
        if (debugMode)
        {
            Debug.Log($"Power gained from enemy: +{powerAmount}. Total: {totalCommunalPower}");
        }
    }
    
    public bool TransferPowerToPlayer(int amount)
    {
        if (totalCommunalPower < amount) return false;
        
        int powerToAdd = Mathf.Min(amount, playerPower.GetAvailableSpace());
        if (powerToAdd <= 0) return false;
        
        totalCommunalPower -= powerToAdd;
        playerPower.currentPower += powerToAdd;
        
        OnTotalPowerChanged?.Invoke(totalCommunalPower);
        OnPlayerPowerChanged?.Invoke(playerPower);
        
        if (debugMode)
        {
            Debug.Log($"Transferred {powerToAdd} power to player. Player: {playerPower.currentPower}/{playerPower.maxPower}");
        }
        
        return true;
    }
    
    public bool TransferPowerToVillager(int villagerIndex, int amount)
    {
        if (villagerIndex < 0 || villagerIndex >= villagerPowers.Count) return false;
        if (totalCommunalPower < amount) return false;
        
        PowerHolder villager = villagerPowers[villagerIndex];
        int powerToAdd = Mathf.Min(amount, villager.GetAvailableSpace());
        if (powerToAdd <= 0) return false;
        
        totalCommunalPower -= powerToAdd;
        villager.currentPower += powerToAdd;
        
        OnTotalPowerChanged?.Invoke(totalCommunalPower);
        OnVillagerPowerChanged?.Invoke(villager);
        
        if (debugMode)
        {
            Debug.Log($"Transferred {powerToAdd} power to {villager.holderName}. Villager: {villager.currentPower}/{villager.maxPower}");
        }
        
        return true;
    }
    
    public bool SharePlayerPowerWithVillagers(int amount)
    {
        if (playerPower.currentPower < amount) return false;
        
        // Find nearby villagers
        List<PowerHolder> nearbyVillagers = GetNearbyVillagers();
        if (nearbyVillagers.Count == 0) return false;
        
        int powerPerVillager = amount / nearbyVillagers.Count;
        int totalShared = 0;
        
        foreach (var villager in nearbyVillagers)
        {
            int powerToGive = Mathf.Min(powerPerVillager, villager.GetAvailableSpace());
            if (powerToGive > 0)
            {
                villager.currentPower += powerToGive;
                totalShared += powerToGive;
                OnVillagerPowerChanged?.Invoke(villager);
            }
        }
        
        playerPower.currentPower -= totalShared;
        OnPlayerPowerChanged?.Invoke(playerPower);
        
        if (debugMode)
        {
            Debug.Log($"Player shared {totalShared} power with {nearbyVillagers.Count} nearby villagers");
        }
        
        return totalShared > 0;
    }
    
    private List<PowerHolder> GetNearbyVillagers()
    {
        List<PowerHolder> nearby = new List<PowerHolder>();
        
        if (playerTransform == null) return nearby;
        
        foreach (var villager in villagerPowers)
        {
            if (villager.holderTransform != null)
            {
                float distance = Vector3.Distance(playerTransform.position, villager.holderTransform.position);
                if (distance <= powerShareRadius)
                {
                    nearby.Add(villager);
                }
            }
        }
        
        return nearby;
    }
    
    private void HandleAutoDistribution()
    {
        if (Time.time - lastAutoDistribution < 1f / autoDistributionRate) return;
        
        // Auto-distribute small amounts of communal power to nearby entities
        if (totalCommunalPower > 0)
        {
            List<PowerHolder> nearbyVillagers = GetNearbyVillagers();
            
            // Prioritize villagers, then player
            foreach (var villager in nearbyVillagers)
            {
                if (!villager.IsFull() && totalCommunalPower > 0)
                {
                    TransferPowerToVillager(villagerPowers.IndexOf(villager), 1);
                    break; // Only one per update for smooth distribution
                }
            }
        }
        
        lastAutoDistribution = Time.time;
    }
    
    private void HandleRebellionCheck()
    {
        if (Time.time - lastRebellionCheck < rebellionCheckInterval) return;
        
        float greedLevel = CalculatePlayerGreedLevel();
        float rebellionRisk = CalculateRebellionRisk(greedLevel);
        
        OnRebellionRiskChanged?.Invoke(rebellionRisk);
        
        // Trigger rebellion event if risk is high enough
        if (rebellionRisk > 0.8f && Random.Range(0f, 1f) < rebellionRisk * 0.1f) // Small chance per check
        {
            TriggerRebellion();
        }
        
        lastRebellionCheck = Time.time;
    }
    
    private float CalculatePlayerGreedLevel()
    {
        int totalPossiblePower = GetTotalPossiblePower();
        if (totalPossiblePower == 0) return 0f;
        
        return (float)playerPower.currentPower / totalPossiblePower;
    }
    
    private float CalculateRebellionRisk(float greedLevel)
    {
        if (greedLevel < greedThreshold) return 0f;
        
        float excessGreed = greedLevel - greedThreshold;
        float maxExcessGreed = 1f - greedThreshold;
        
        return Mathf.Clamp01(excessGreed / maxExcessGreed) * maxRebellionChance;
    }
    
    private void TriggerRebellion()
    {
        Debug.Log("REBELLION TRIGGERED! Villagers are unhappy with power distribution!");
        
        // You can implement rebellion effects here:
        // - Villagers refuse to give power
        // - Villagers become hostile
        // - Power generation stops temporarily
        // - etc.
    }

    public int GetUnallocatedPower()
    {
        int allocatedPower = playerPower.currentPower;

        // Add up all villager power allocations
        foreach (var villager in villagerPowers)
        {
            allocatedPower += villager.currentPower;
        }

        return Mathf.Max(0, totalCommunalPower - allocatedPower);
    }

    // Public getters
    public int GetTotalCommunalPower() => totalCommunalPower;
    public int GetTotalPossiblePower() => playerPower.maxPower + villagerPowers.Sum(v => v.maxPower);
    public int GetTotalCurrentPower() => playerPower.currentPower + villagerPowers.Sum(v => v.currentPower);
    public PowerHolder GetPlayerPower() => playerPower;
    public List<PowerHolder> GetVillagerPowers() => villagerPowers;
    public float GetPlayerGreedLevel() => CalculatePlayerGreedLevel();
    public float GetRebellionRisk() => CalculateRebellionRisk(CalculatePlayerGreedLevel());
    
    // Debug methods
    public void AddDebugPower(int amount)
    {
        AddPowerFromEnemy(amount);
    }
    
    private void OnDrawGizmosSelected()
    {
        if (playerTransform != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(playerTransform.position, powerShareRadius);
        }
    }
}