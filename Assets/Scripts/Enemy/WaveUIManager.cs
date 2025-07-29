using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class EnemyUIElement
{
    [Header("UI References")]
    public GameObject enemyUIGroup;
    public TextMeshProUGUI enemyCountText;
    public Image enemyImage;
    
    [Header("Enemy Configuration")]
    public string enemyTypeName;
    public Sprite enemySprite;
    
    [HideInInspector]
    public int currentCount = 0;
    [HideInInspector]
    public bool isActiveInWave = false;
}

public class WaveUIManager : MonoBehaviour
{
    [Header("Wave UI References")]
    [SerializeField] private GameObject waveSection;
    [SerializeField] private TextMeshProUGUI waveText;
    
    [Header("Enemy UI Elements")]
    [SerializeField] private EnemyUIElement[] enemyUIElements;
    
    [Header("Wave States")]
    [SerializeField] private string waveTextFormat = "WAVE {0}";
    [SerializeField] private string cooldownTextFormat = "Next Wave: {0:F0}s";
    [SerializeField] private string waveCompleteText = "WAVE COMPLETE";
    
    // Internal state
    private EnemyWaveSystem waveSystem;
    private bool isInCooldown = false;
    private float cooldownTimer = 0f;
    private int currentWaveNumber = 0;
    
    // Enemy tracking
    private Dictionary<string, EnemyUIElement> enemyUIMap = new Dictionary<string, EnemyUIElement>();
    
    private void Start()
    {
        InitializeWaveUI();
        SetupEventListeners();
        SetupEnemyUIMap();
    }
    
    private void InitializeWaveUI()
    {
        waveSystem = FindObjectOfType<EnemyWaveSystem>();
        if (waveSystem == null)
        {
            Debug.LogError("WaveUIManager: EnemyWaveSystem not found!");
            return;
        }
        
        // Ensure wave section is active
        if (waveSection != null)
        {
            waveSection.SetActive(true);
        }
        
        // Initialize all enemy UI elements as inactive
        foreach (var enemyUI in enemyUIElements)
        {
            if (enemyUI.enemyUIGroup != null)
            {
                enemyUI.enemyUIGroup.SetActive(false);
            }
        }
        
        UpdateWaveDisplay(0, false);
    }
    
    private void SetupEventListeners()
    {
        if (waveSystem != null)
        {
            waveSystem.OnWaveStart += HandleWaveStart;
            waveSystem.OnWaveComplete += HandleWaveComplete;
            waveSystem.OnEnemySpawned += HandleEnemySpawned;
        }
    }
    
    private void SetupEnemyUIMap()
    {
        enemyUIMap.Clear();
        
        foreach (var enemyUI in enemyUIElements)
        {
            if (!string.IsNullOrEmpty(enemyUI.enemyTypeName))
            {
                enemyUIMap[enemyUI.enemyTypeName] = enemyUI;
                
                // Set up the UI image if sprite is assigned
                if (enemyUI.enemyImage != null && enemyUI.enemySprite != null)
                {
                    enemyUI.enemyImage.sprite = enemyUI.enemySprite;
                }
            }
        }
    }
    
    private void Update()
    {
        if (isInCooldown)
        {
            UpdateCooldownDisplay();
        }
        
        UpdateEnemyCounts();
    }
    
    private void HandleWaveStart(int waveNumber)
    {
        currentWaveNumber = waveNumber;
        isInCooldown = false;
        
        UpdateWaveDisplay(waveNumber, false);
        
        // Set up enemy UI after a brief delay to ensure enemies are spawned
        Invoke(nameof(RefreshEnemyUI), 0.1f);
        
        Debug.Log($"WaveUIManager: Wave {waveNumber} started");
    }
    
    private void RefreshEnemyUI()
    {
        if (waveSystem == null) return;
        
        var allEnemies = waveSystem.GetCurrentWaveEnemies();
        var enemyCounts = new Dictionary<string, int>();
        
        // Count enemies by type
        foreach (var enemy in allEnemies)
        {
            if (enemy != null)
            {
                var identifier = enemy.GetComponent<EnemyTypeIdentifier>();
                if (identifier != null)
                {
                    string typeName = identifier.GetTypeName();
                    enemyCounts[typeName] = enemyCounts.ContainsKey(typeName) ? enemyCounts[typeName] + 1 : 1;
                }
            }
        }
        
        // Update UI for each enemy type
        foreach (var enemyUI in enemyUIElements)
        {
            bool hasEnemiesOfThisType = enemyCounts.ContainsKey(enemyUI.enemyTypeName);
            
            enemyUI.isActiveInWave = hasEnemiesOfThisType;
            enemyUI.currentCount = hasEnemiesOfThisType ? enemyCounts[enemyUI.enemyTypeName] : 0;
            
            if (enemyUI.enemyUIGroup != null)
            {
                enemyUI.enemyUIGroup.SetActive(hasEnemiesOfThisType);
            }
            
            UpdateEnemyCountDisplay(enemyUI);
        }
    }
    
    private void HandleWaveComplete(int waveNumber)
    {
        // Show wave complete briefly, then start cooldown
        if (waveText != null)
        {
            waveText.text = waveCompleteText;
        }
        
        // Hide all enemy UI elements immediately
        foreach (var enemyUI in enemyUIElements)
        {
            if (enemyUI.enemyUIGroup != null)
            {
                enemyUI.enemyUIGroup.SetActive(false);
            }
            enemyUI.isActiveInWave = false;
            enemyUI.currentCount = 0;
        }
        
        // Start cooldown after a brief delay
        Invoke(nameof(StartCooldownDisplay), 1f);
        
        Debug.Log($"WaveUIManager: Wave {waveNumber} completed");
    }
    
    private void HandleEnemySpawned(EnemyType enemyType)
    {
        // This is called when an enemy is spawned
        // We'll update counts in UpdateEnemyCounts() instead
    }
    
    private void StartCooldownDisplay()
    {
        isInCooldown = true;
        cooldownTimer = GetNextWaveCooldown();
    }
    
    private float GetNextWaveCooldown()
    {
        // Get cooldown time from wave system
        if (waveSystem != null)
        {
            return waveSystem.GetTimeBetweenWaves();
        }
        return 15f; // Default fallback
    }
    
    private void UpdateCooldownDisplay()
    {
        cooldownTimer -= Time.deltaTime;
        
        if (cooldownTimer <= 0f)
        {
            isInCooldown = false;
            cooldownTimer = 0f;
        }
        
        if (waveText != null)
        {
            waveText.text = string.Format(cooldownTextFormat, Mathf.Max(0f, cooldownTimer));
        }
    }
    
    private void UpdateWaveDisplay(int waveNumber, bool inCooldown)
    {
        if (waveText != null && !inCooldown)
        {
            waveText.text = string.Format(waveTextFormat, waveNumber);
        }
    }
    
    private void SetupEnemyUIForWave()
    {
        if (waveSystem == null || !waveSystem.IsWaveInProgress())
        {
            // Hide all enemy UI if no wave is active
            foreach (var enemyUI in enemyUIElements)
            {
                enemyUI.isActiveInWave = false;
                if (enemyUI.enemyUIGroup != null)
                {
                    enemyUI.enemyUIGroup.SetActive(false);
                }
            }
            return;
        }
        
        // Get the current wave composition from the wave system
        var currentWaveEnemies = GetCurrentWaveEnemyTypes();
        
        Debug.Log($"Setting up UI for wave with {currentWaveEnemies.Count} enemy types");
        
        // Reset all enemy UI elements
        foreach (var enemyUI in enemyUIElements)
        {
            enemyUI.isActiveInWave = false;
            enemyUI.currentCount = 0;
            
            if (enemyUI.enemyUIGroup != null)
            {
                enemyUI.enemyUIGroup.SetActive(false);
            }
        }
        
        // Activate UI elements for enemies in this wave
        foreach (var enemyTypeName in currentWaveEnemies.Keys)
        {
            Debug.Log($"Found enemy type: {enemyTypeName} with count: {currentWaveEnemies[enemyTypeName]}");
            
            if (enemyUIMap.ContainsKey(enemyTypeName))
            {
                var enemyUI = enemyUIMap[enemyTypeName];
                enemyUI.isActiveInWave = true;
                enemyUI.currentCount = currentWaveEnemies[enemyTypeName];
                
                if (enemyUI.enemyUIGroup != null)
                {
                    enemyUI.enemyUIGroup.SetActive(true);
                    Debug.Log($"Activated UI for {enemyTypeName}");
                }
                
                UpdateEnemyCountDisplay(enemyUI);
            }
            else
            {
                Debug.LogWarning($"No UI element found for enemy type: {enemyTypeName}");
            }
        }
    }
    
    private Dictionary<string, int> GetCurrentWaveEnemyTypes()
    {
        var enemyTypes = new Dictionary<string, int>();
        
        if (waveSystem != null)
        {
            // Count enemies by type name
            var currentEnemies = waveSystem.GetCurrentWaveEnemies();
            
            foreach (var enemy in currentEnemies)
            {
                if (enemy != null)
                {
                    // Try to get enemy type from the EnemyHealth or a tag
                    string typeName = GetEnemyTypeName(enemy);
                    
                    if (!string.IsNullOrEmpty(typeName))
                    {
                        if (enemyTypes.ContainsKey(typeName))
                        {
                            enemyTypes[typeName]++;
                        }
                        else
                        {
                            enemyTypes[typeName] = 1;
                        }
                    }
                }
            }
        }
        
        return enemyTypes;
    }
    
    private string GetEnemyTypeName(GameObject enemy)
    {
        // Method 1: Check for EnemyTypeIdentifier component
        var typeIdentifier = enemy.GetComponent<EnemyTypeIdentifier>();
        if (typeIdentifier != null)
        {
            string typeName = typeIdentifier.GetTypeName();
            Debug.Log($"Enemy {enemy.name} has type identifier: {typeName}");
            return typeName;
        }
        
        // Method 2: Check object name (with cleanup)
        string enemyName = enemy.name.Replace("(Clone)", "").Trim();
        Debug.Log($"Checking enemy name: {enemyName}");
        
        if (enemyName.Contains("Ghost") && enemyName.Contains("Mage"))
        {
            return "Ghost Mage";
        }
        else if (enemyName.Contains("Ghost"))
        {
            return "Ghost";
        }
        
        // Method 3: Check tag
        if (enemy.CompareTag("EnemyGhostMage"))
        {
            return "Ghost Mage";
        }
        else if (enemy.CompareTag("EnemyGhost"))
        {
            return "Ghost";
        }
        
        Debug.LogWarning($"Could not identify enemy type for: {enemy.name}");
        return "Unknown";
    }
    
    private void UpdateEnemyCounts()
    {
        // Only update counts during active waves
        if (isInCooldown || waveSystem == null || !waveSystem.IsWaveInProgress()) 
        {
            // Hide all enemy UI if not in an active wave
            foreach (var enemyUI in enemyUIElements)
            {
                if (enemyUI.enemyUIGroup != null)
                {
                    enemyUI.enemyUIGroup.SetActive(false);
                }
                enemyUI.isActiveInWave = false;
            }
            return;
        }
        
        var allEnemies = waveSystem.GetCurrentWaveEnemies();
        var enemyCounts = new Dictionary<string, int>();
        
        // Count current enemies by type
        foreach (var enemy in allEnemies)
        {
            if (enemy != null)
            {
                var identifier = enemy.GetComponent<EnemyTypeIdentifier>();
                if (identifier != null)
                {
                    string typeName = identifier.GetTypeName();
                    enemyCounts[typeName] = enemyCounts.ContainsKey(typeName) ? enemyCounts[typeName] + 1 : 1;
                }
            }
        }
        
        // Update UI for active enemy types
        foreach (var enemyUI in enemyUIElements)
        {
            if (enemyUI.isActiveInWave)
            {
                int newCount = enemyCounts.ContainsKey(enemyUI.enemyTypeName) ? enemyCounts[enemyUI.enemyTypeName] : 0;
                
                if (newCount != enemyUI.currentCount)
                {
                    enemyUI.currentCount = newCount;
                    UpdateEnemyCountDisplay(enemyUI);
                }
            }
        }
    }
    
    private void UpdateEnemyCountDisplay(EnemyUIElement enemyUI)
    {
        if (enemyUI.enemyCountText != null)
        {
            enemyUI.enemyCountText.text = $"x {enemyUI.currentCount}";
        }
    }
    
    // Public methods for manual control
    public void SetEnemyCount(string enemyTypeName, int count)
    {
        if (enemyUIMap.ContainsKey(enemyTypeName))
        {
            var enemyUI = enemyUIMap[enemyTypeName];
            enemyUI.currentCount = count;
            UpdateEnemyCountDisplay(enemyUI);
        }
    }
    
    public void ShowEnemyUI(string enemyTypeName, bool show)
    {
        if (enemyUIMap.ContainsKey(enemyTypeName))
        {
            var enemyUI = enemyUIMap[enemyTypeName];
            if (enemyUI.enemyUIGroup != null)
            {
                enemyUI.enemyUIGroup.SetActive(show);
            }
            enemyUI.isActiveInWave = show;
        }
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from events
        if (waveSystem != null)
        {
            waveSystem.OnWaveStart -= HandleWaveStart;
            waveSystem.OnWaveComplete -= HandleWaveComplete;
            waveSystem.OnEnemySpawned -= HandleEnemySpawned;
        }
    }
}