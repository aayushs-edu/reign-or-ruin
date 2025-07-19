using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class PowerUISystem : MonoBehaviour
{
    [Header("Communal Power UI")]
    [SerializeField] private Slider communalPowerBar;
    [SerializeField] private TextMeshProUGUI communalPowerText;
    [SerializeField] private int maxCommunalPowerDisplay = 100;
    
    [Header("Player Power UI")]
    [SerializeField] private Slider playerPowerBar;
    [SerializeField] private TextMeshProUGUI playerPowerText;
    [SerializeField] private Image playerPowerFill;
    [SerializeField] private Color normalPowerColor = Color.blue;
    [SerializeField] private Color chargedPowerColor = Color.cyan;
    
    [Header("Villager Power UI")]
    [SerializeField] private Transform villagerPowerContainer;
    [SerializeField] private GameObject villagerPowerPrefab; // Prefab for individual villager power bars
    
    [Header("Rebellion Warning UI")]
    [SerializeField] private GameObject rebellionWarning;
    [SerializeField] private Slider rebellionRiskBar;
    [SerializeField] private TextMeshProUGUI rebellionRiskText;
    [SerializeField] private Color lowRiskColor = Color.green;
    [SerializeField] private Color highRiskColor = Color.red;
    
    [Header("Controls Info")]
    [SerializeField] private GameObject controlsPanel;
    [SerializeField] private TextMeshProUGUI controlsText;
    
    // References
    private PowerSystem powerSystem;
    private List<VillagerPowerUI> villagerUIs = new List<VillagerPowerUI>();
    
    // UI Components for individual villager power bars
    [System.Serializable]
    public class VillagerPowerUI
    {
        public GameObject uiObject;
        public Slider powerBar;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI powerText;
        public int villagerIndex;
    }
    
    private void Start()
    {
        InitializeReferences();
        SetupEventListeners();
        CreateVillagerUIs();
        SetupControlsText();
        
        // Initial UI update
        UpdateAllUI();
    }
    
    private void InitializeReferences()
    {
        powerSystem = PowerSystem.Instance;
        if (powerSystem == null)
        {
            Debug.LogError("PowerUISystem: PowerSystem not found!");
        }
    }
    
    private void SetupEventListeners()
    {
        if (powerSystem != null)
        {
            powerSystem.OnTotalPowerChanged += UpdateCommunalPowerUI;
            powerSystem.OnPlayerPowerChanged += UpdatePlayerPowerUI;
            powerSystem.OnVillagerPowerChanged += UpdateVillagerPowerUI;
            powerSystem.OnRebellionRiskChanged += UpdateRebellionUI;
        }
    }
    
    private void CreateVillagerUIs()
    {
        if (powerSystem == null || villagerPowerContainer == null || villagerPowerPrefab == null) return;
        
        List<PowerHolder> villagers = powerSystem.GetVillagerPowers();
        
        for (int i = 0; i < villagers.Count; i++)
        {
            GameObject villagerUI = Instantiate(villagerPowerPrefab, villagerPowerContainer);
            
            VillagerPowerUI ui = new VillagerPowerUI();
            ui.uiObject = villagerUI;
            ui.villagerIndex = i;
            
            // Get UI components from prefab
            ui.powerBar = villagerUI.GetComponentInChildren<Slider>();
            ui.nameText = villagerUI.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
            ui.powerText = villagerUI.transform.Find("PowerText")?.GetComponent<TextMeshProUGUI>();
            
            // Setup initial values
            if (ui.nameText != null)
            {
                ui.nameText.text = villagers[i].holderName;
            }
            
            if (ui.powerBar != null)
            {
                ui.powerBar.maxValue = villagers[i].maxPower;
                ui.powerBar.value = villagers[i].currentPower;
            }
            
            villagerUIs.Add(ui);
        }
    }
    
    private void SetupControlsText()
    {
        if (controlsText != null)
        {
            controlsText.text = "Controls:\n" +
                               "WASD/Arrows - Move\n" +
                               "Space/Click - Attack\n" +
                               "Q - Share Power\n" +
                               "E - Hoard Power\n" +
                               "Tab - Toggle UI";
        }
    }
    
    private void Update()
    {
        HandleInput();
        
        // Update UI periodically even without events
        if (Time.frameCount % 30 == 0) // Every 30 frames
        {
            UpdateAllUI();
        }
    }
    
    private void HandleInput()
    {
        // Toggle controls panel
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (controlsPanel != null)
            {
                controlsPanel.SetActive(!controlsPanel.activeInHierarchy);
            }
        }
        
        // Power management shortcuts
        if (powerSystem != null)
        {
            // Share power with villagers
            if (Input.GetKeyDown(KeyCode.Q))
            {
                powerSystem.SharePlayerPowerWithVillagers(5);
            }
            
            // Take power for player
            if (Input.GetKeyDown(KeyCode.E))
            {
                powerSystem.TransferPowerToPlayer(5);
            }
        }
    }
    
    private void UpdateAllUI()
    {
        if (powerSystem == null) return;
        
        UpdateCommunalPowerUI(powerSystem.GetTotalCommunalPower());
        UpdatePlayerPowerUI(powerSystem.GetPlayerPower());
        UpdateRebellionUI(powerSystem.GetRebellionRisk());
        
        // Update all villager UIs
        List<PowerHolder> villagers = powerSystem.GetVillagerPowers();
        for (int i = 0; i < villagers.Count && i < villagerUIs.Count; i++)
        {
            UpdateVillagerPowerUI(villagers[i]);
        }
    }
    
    private void UpdateCommunalPowerUI(int communalPower)
    {
        if (communalPowerBar != null)
        {
            communalPowerBar.maxValue = maxCommunalPowerDisplay;
            communalPowerBar.value = communalPower;
        }
        
        if (communalPowerText != null)
        {
            communalPowerText.text = $"Communal Power: {communalPower}";
        }
    }
    
    private void UpdatePlayerPowerUI(PowerHolder playerPower)
    {
        if (playerPower == null) return;
        
        if (playerPowerBar != null)
        {
            playerPowerBar.maxValue = playerPower.maxPower;
            playerPowerBar.value = playerPower.currentPower;
        }
        
        if (playerPowerText != null)
        {
            playerPowerText.text = $"Player: {playerPower.currentPower}/{playerPower.maxPower}";
        }
        
        // Color the power bar based on charge level
        if (playerPowerFill != null)
        {
            float chargeLevel = playerPower.GetPowerPercentage();
            playerPowerFill.color = Color.Lerp(normalPowerColor, chargedPowerColor, chargeLevel);
        }
    }
    
    private void UpdateVillagerPowerUI(PowerHolder villagerPower)
    {
        if (villagerPower == null) return;
        
        // Find the matching UI
        VillagerPowerUI ui = villagerUIs.Find(v => v.villagerIndex < powerSystem.GetVillagerPowers().Count && 
                                                   powerSystem.GetVillagerPowers()[v.villagerIndex] == villagerPower);
        
        if (ui == null) return;
        
        if (ui.powerBar != null)
        {
            ui.powerBar.value = villagerPower.currentPower;
        }
        
        if (ui.powerText != null)
        {
            ui.powerText.text = $"{villagerPower.currentPower}/{villagerPower.maxPower}";
        }
    }
    
    private void UpdateRebellionUI(float rebellionRisk)
    {
        bool showWarning = rebellionRisk > 0.1f; // Show warning at 10% risk
        
        if (rebellionWarning != null)
        {
            rebellionWarning.SetActive(showWarning);
        }
        
        if (rebellionRiskBar != null)
        {
            rebellionRiskBar.value = rebellionRisk;
            
            // Color the bar based on risk level
            Image fill = rebellionRiskBar.fillRect?.GetComponent<Image>();
            if (fill != null)
            {
                fill.color = Color.Lerp(lowRiskColor, highRiskColor, rebellionRisk);
            }
        }
        
        if (rebellionRiskText != null)
        {
            rebellionRiskText.text = $"Rebellion Risk: {rebellionRisk:P0}";
        }
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from events
        if (powerSystem != null)
        {
            powerSystem.OnTotalPowerChanged -= UpdateCommunalPowerUI;
            powerSystem.OnPlayerPowerChanged -= UpdatePlayerPowerUI;
            powerSystem.OnVillagerPowerChanged -= UpdateVillagerPowerUI;
            powerSystem.OnRebellionRiskChanged -= UpdateRebellionUI;
        }
    }
}