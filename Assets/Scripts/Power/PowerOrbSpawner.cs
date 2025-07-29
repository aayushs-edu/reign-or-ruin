using UnityEngine;

public class PowerOrbSpawner : MonoBehaviour
{
    [Header("Orb Prefab")]
    [SerializeField] private GameObject powerOrbPrefab;
    
    [Header("Spawn Configuration")]
    [SerializeField] private float spawnRadius = 1f;
    [SerializeField] private int maxOrbsPerSpawn = 1;
    [SerializeField] private float orbSpacing = 0.5f;
    
    [Header("Orb Properties")]
    [SerializeField] private Color defaultOrbColor = Color.cyan;
    [SerializeField] private float orbLifetime = 30f;
    [SerializeField] private bool customizeOrbsByValue = true;
    
    [Header("Value-Based Customization")]
    [SerializeField] private OrbColorConfig[] orbColorConfigs;
    
    [System.Serializable]
    public class OrbColorConfig
    {
        public int minValue = 1;
        public int maxValue = 1;
        public Color orbColor = Color.cyan;
        public float sizeMultiplier = 1f;
        
        public bool IsInRange(int value)
        {
            return value >= minValue && value <= maxValue;
        }
    }
    
    // Singleton for easy access
    public static PowerOrbSpawner Instance { get; private set; }
    
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
        ValidateConfiguration();
    }
    
    private void ValidateConfiguration()
    {
        if (powerOrbPrefab == null)
        {
            Debug.LogError("PowerOrbSpawner: No power orb prefab assigned!");
        }
        
        // Ensure orb prefab has PowerOrb component
        if (powerOrbPrefab != null && powerOrbPrefab.GetComponent<PowerOrb>() == null)
        {
            Debug.LogWarning("PowerOrbSpawner: Assigned prefab doesn't have PowerOrb component!");
        }
        
        // Set up default color configs if none exist
        if (orbColorConfigs == null || orbColorConfigs.Length == 0)
        {
            SetupDefaultColorConfigs();
        }
    }
    
    private void SetupDefaultColorConfigs()
    {
        orbColorConfigs = new OrbColorConfig[]
        {
            new OrbColorConfig { minValue = 1, maxValue = 1, orbColor = Color.cyan, sizeMultiplier = 1f },
            new OrbColorConfig { minValue = 2, maxValue = 3, orbColor = Color.yellow, sizeMultiplier = 1.2f },
            new OrbColorConfig { minValue = 4, maxValue = 5, orbColor = Color.magenta, sizeMultiplier = 1.4f },
            new OrbColorConfig { minValue = 6, maxValue = 10, orbColor = Color.red, sizeMultiplier = 1.6f }
        };
    }
    
    public void SpawnPowerOrb(Vector3 position, int powerValue = 1)
    {
        if (powerOrbPrefab == null)
        {
            Debug.LogError("PowerOrbSpawner: Cannot spawn orb - no prefab assigned!");
            return;
        }
        
        // Calculate spawn position with slight randomness
        Vector3 spawnPosition = position + (Vector3)Random.insideUnitCircle * spawnRadius;
        spawnPosition.z = 0f; // Ensure 2D positioning
        
        // Instantiate the orb
        GameObject orbInstance = Instantiate(powerOrbPrefab, spawnPosition, Quaternion.identity);
        
        // Configure the orb
        PowerOrb orbComponent = orbInstance.GetComponent<PowerOrb>();
        if (orbComponent != null)
        {
            ConfigureOrb(orbComponent, powerValue);
        }
        else
        {
            Debug.LogError("PowerOrbSpawner: Spawned orb doesn't have PowerOrb component!");
        }
    }
    
    public void SpawnMultiplePowerOrbs(Vector3 position, int totalPowerValue)
    {
        if (totalPowerValue <= 0) return;
        
        // Determine how many orbs to spawn
        int orbCount = Mathf.Min(totalPowerValue, maxOrbsPerSpawn);
        int powerPerOrb = totalPowerValue / orbCount;
        int remainingPower = totalPowerValue % orbCount;
        
        // Spawn orbs in a circular pattern
        for (int i = 0; i < orbCount; i++)
        {
            float angle = (i / (float)orbCount) * 2f * Mathf.PI;
            Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * orbSpacing;
            Vector3 spawnPosition = position + offset;
            
            // Give extra power to the first orb if there's remainder
            int orbPowerValue = powerPerOrb + (i == 0 ? remainingPower : 0);
            
            SpawnPowerOrb(spawnPosition, orbPowerValue);
        }
    }
    
    private void ConfigureOrb(PowerOrb orb, int powerValue)
    {
        // Set power value
        orb.SetPowerValue(powerValue);
        
        // Set lifetime
        orb.SetLifetime(orbLifetime);
        
        // Customize based on value if enabled
        if (customizeOrbsByValue)
        {
            ApplyValueBasedCustomization(orb, powerValue);
        }
        else
        {
            // Use default color
            orb.SetOrbColor(defaultOrbColor);
        }
    }
    
    private void ApplyValueBasedCustomization(PowerOrb orb, int powerValue)
    {
        // Find matching color config
        OrbColorConfig matchingConfig = null;
        
        foreach (var config in orbColorConfigs)
        {
            if (config.IsInRange(powerValue))
            {
                matchingConfig = config;
                break;
            }
        }
        
        // Apply configuration or use default
        if (matchingConfig != null)
        {
            orb.SetOrbColor(matchingConfig.orbColor);
            orb.transform.localScale = Vector3.one * matchingConfig.sizeMultiplier;
        }
        else
        {
            orb.SetOrbColor(defaultOrbColor);
        }
    }
    
    // Convenience methods for different spawn scenarios
    public void SpawnFromEnemyDeath(Vector3 enemyPosition, int powerValue)
    {
        // Add slight upward force for enemy death
        Vector3 spawnPos = enemyPosition + Vector3.up * 0.5f;
        SpawnPowerOrb(spawnPos, powerValue);
    }
    
    public void SpawnFromVillagerDeath(Vector3 villagerPosition, int allocatedPower)
    {
        if (allocatedPower <= 0) return;
        
        // Villagers might drop multiple orbs if they had lots of power
        if (allocatedPower > 3)
        {
            SpawnMultiplePowerOrbs(villagerPosition, allocatedPower);
        }
        else
        {
            SpawnPowerOrb(villagerPosition, allocatedPower);
        }
    }
    
    public void SpawnFromBuilding(Vector3 buildingPosition, int powerValue)
    {
        // Buildings might spawn orbs in a spread pattern
        SpawnMultiplePowerOrbs(buildingPosition, powerValue);
    }
    
    // Public configuration methods
    public void SetOrbPrefab(GameObject prefab)
    {
        powerOrbPrefab = prefab;
        ValidateConfiguration();
    }
    
    public void SetSpawnRadius(float radius)
    {
        spawnRadius = radius;
    }
    
    public void SetOrbLifetime(float lifetime)
    {
        orbLifetime = lifetime;
    }
    
    public void SetMaxOrbsPerSpawn(int maxOrbs)
    {
        maxOrbsPerSpawn = Mathf.Max(1, maxOrbs);
    }
    
    public void AddColorConfig(int minValue, int maxValue, Color color, float sizeMultiplier = 1f)
    {
        var newConfig = new OrbColorConfig
        {
            minValue = minValue,
            maxValue = maxValue,
            orbColor = color,
            sizeMultiplier = sizeMultiplier
        };
        
        // Add to array
        System.Array.Resize(ref orbColorConfigs, orbColorConfigs.Length + 1);
        orbColorConfigs[orbColorConfigs.Length - 1] = newConfig;
    }
    
    // Debug methods
    [ContextMenu("Test Spawn Single Orb")]
    public void TestSpawnSingleOrb()
    {
        SpawnPowerOrb(transform.position, 1);
    }
    
    [ContextMenu("Test Spawn Multiple Orbs")]
    public void TestSpawnMultipleOrbs()
    {
        SpawnMultiplePowerOrbs(transform.position, 5);
    }
    
    private void OnDrawGizmosSelected()
    {
        // Draw spawn radius
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
        
        // Draw orb spacing for multiple spawns
        if (maxOrbsPerSpawn > 1)
        {
            Gizmos.color = Color.yellow;
            for (int i = 0; i < maxOrbsPerSpawn; i++)
            {
                float angle = (i / (float)maxOrbsPerSpawn) * 2f * Mathf.PI;
                Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * orbSpacing;
                Vector3 orbPosition = transform.position + offset;
                Gizmos.DrawWireSphere(orbPosition, 0.2f);
            }
        }
    }
    
    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}