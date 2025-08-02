using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections;
using TMPro;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections.Generic;

public class DayNightCycleManager : MonoBehaviour
{
    [System.Serializable]
    public class LightingSettings
    {
        [Header("Day Lighting")]
        public Color dayColor = new Color(1f, 0.95f, 0.8f);
        public float dayIntensity = 1.2f;
        
        [Header("Night Lighting")]
        public Color nightColor = new Color(0.3f, 0.4f, 0.6f);
        public float nightIntensity = 0.6f;
        
        [Header("Transition Settings")]
        public float lightTransitionDuration = 2f;
        public Ease lightTransitionEase = Ease.InOutSine;
    }
    
    [System.Serializable]
    public class UITransitionSettings
    {
        [Header("Animation Durations")]
        public float waveTextExitDuration = 1f;
        public float topAreaEnterDuration = 1f;
        public float mainPanelEnterDuration = 1.2f;
        public float waveTextEnterDuration = 0.8f;
        public float mainPanelExitDuration = 1f;
        
        [Header("Animation Eases")]
        public Ease waveTextEase = Ease.OutCubic;
        public Ease topAreaEase = Ease.OutBack;
        public Ease mainPanelEase = Ease.OutBack;
        
        [Header("Offset Distances")]
        public float topAreaOffsetY = 200f;
        public float mainPanelOffsetY = -500f;
        public float waveTextOffsetY = 300f;
        
        [Header("Wave Display Settings")]
        public float waveTextCenterDisplayTime = 2f;
    }
    
    [Header("References")]
    [SerializeField] private Light2D globalLight;
    [SerializeField] private Transform waveSection;
    [SerializeField] private Transform topArea;
    [SerializeField] private Transform mainPanel;
    [SerializeField] private Button skipButton;
    [SerializeField] private TextMeshProUGUI countdownText;
    
    [Header("Wave Section Components")]
    [SerializeField] private TextMeshProUGUI nightText;
    [SerializeField] private TextMeshProUGUI ghostText;
    [SerializeField] private TextMeshProUGUI ghostMageText;
    
    [Header("Village System References")]
    [SerializeField] private Transform villagerContainer;
    [SerializeField] private Transform rulerSection;
    [SerializeField] private VillageManager villageManager;
    [SerializeField] private EnemyWaveSystem waveSystem;
    
    [Header("Settings")]
    [SerializeField] private LightingSettings lightingSettings;
    [SerializeField] private UITransitionSettings uiSettings;
    [SerializeField] private float dayDuration = 30f;
    [SerializeField] private bool debugTransitions = false;
    
    // Internal State
    private bool isDay = false;
    private bool isTransitioning = false;
    private int currentWave = 1;
    private float dayTimeRemaining;
    private Coroutine dayTimerCoroutine;
    
    // UI Original Positions
    private Vector3 waveSectionOriginalPos;
    private Vector3 topAreaOriginalPos;
    private Vector3 mainPanelOriginalPos;
    
    // Events
    public System.Action<bool> OnDayNightTransition;
    public System.Action OnDayStarted;
    public System.Action OnNightStarted;
    
    public static DayNightCycleManager Instance { get; private set; }
    
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
        InitializeSystem();
        SetupEventListeners();
        StoreOriginalPositions();
        
        // Start with night
        SetupNightImmediate();
    }
    
    private void InitializeSystem()
    {
        // Auto-find references if not assigned
        if (globalLight == null)
            globalLight = FindObjectOfType<Light2D>();
            
        if (waveSystem == null)
            waveSystem = FindObjectOfType<EnemyWaveSystem>();
            
        if (villageManager == null)
            villageManager = FindObjectOfType<VillageManager>();
            
        // Setup skip button
        if (skipButton != null)
        {
            skipButton.onClick.AddListener(SkipToNight);
        }
        
        // Enable manual wave control
        if (waveSystem != null)
        {
            waveSystem.SetManualWaveControl(true);
        }
        
        ValidateReferences();
    }
    
    private void ValidateReferences()
    {
        if (globalLight == null)
            Debug.LogError("DayNightCycleManager: No global Light2D found!");
            
        if (waveSystem == null)
            Debug.LogError("DayNightCycleManager: No EnemyWaveSystem found!");
    }
    
    private void SetupEventListeners()
    {
        // Listen to existing wave system events but don't let it auto-progress
        if (waveSystem != null)
        {
            waveSystem.OnWaveComplete += HandleWaveComplete;
            // Don't listen to OnWaveStart since we'll control that
        }
        
        // Listen to village manager events
        if (villageManager != null)
        {
            villageManager.OnDayNightCycle += HandleVillageManagerCycle;
        }
    }
    
    private void StoreOriginalPositions()
    {
        if (waveSection != null)
            waveSectionOriginalPos = waveSection.position;
        if (topArea != null)
            topAreaOriginalPos = topArea.position;
        if (mainPanel != null)
            mainPanelOriginalPos = mainPanel.position;
    }
    
    #region Wave Event Handlers
    
    private void HandleWaveComplete(int waveNumber)
    {
        if (debugTransitions)
            Debug.Log($"Wave {waveNumber} completed, starting day transition");
        
        // Update wave counter for next night
        currentWave = waveNumber + 1;
        
        // Transition to day for power allocation
        StartDayTransition();
    }
    
    private void HandleVillageManagerCycle(bool isNight)
    {
        // This handles legacy VillageManager day/night events
        // We don't need to do anything here since we're taking over control
        if (debugTransitions)
            Debug.Log($"VillageManager cycle event: isNight={isNight} (ignored - we control this now)");
    }
    
    #endregion
    
    #region Day/Night Transitions
    
    public void StartDayTransition()
    {
        if (isTransitioning) return;
        
        StartCoroutine(TransitionToDay());
    }
    
    public void StartNightTransition()
    {
        if (isTransitioning) return;
        
        StartCoroutine(TransitionToNight());
    }
    
    public void SkipToNight()
    {
        if (!isDay || isTransitioning) return;
        
        if (dayTimerCoroutine != null)
        {
            StopCoroutine(dayTimerCoroutine);
            dayTimerCoroutine = null;
        }
        
        StartNightTransition();
    }
    
    private IEnumerator TransitionToDay()
    {
        isTransitioning = true;
        isDay = true;
        
        if (debugTransitions)
            Debug.Log("Starting transition to day");
        
        // 1. Wave section moves up and out
        if (waveSection != null)
        {
            Vector3 exitPos = waveSectionOriginalPos + Vector3.up * uiSettings.waveTextOffsetY;
            waveSection.DOMove(exitPos, uiSettings.waveTextExitDuration)
                      .SetEase(uiSettings.waveTextEase);
            
            yield return new WaitForSeconds(uiSettings.waveTextExitDuration * 0.5f);
        }
        
        // 2. Transition lighting to day
        TransitionLighting(true);
        
        // 3. Top area transitions down from above
        if (topArea != null)
        {
            Vector3 startPos = topAreaOriginalPos + Vector3.up * uiSettings.topAreaOffsetY;
            topArea.position = startPos;
            topArea.gameObject.SetActive(true);
            
            topArea.DOMove(topAreaOriginalPos, uiSettings.topAreaEnterDuration)
                   .SetEase(uiSettings.topAreaEase);
        }
        
        // Small delay before main panel
        yield return new WaitForSeconds(0.3f);
        
        // 4. Main panel transitions up from below
        if (mainPanel != null)
        {
            Vector3 startPos = mainPanelOriginalPos + Vector3.up * uiSettings.mainPanelOffsetY;
            mainPanel.position = startPos;
            mainPanel.gameObject.SetActive(true);
            
            mainPanel.DOMove(mainPanelOriginalPos, uiSettings.mainPanelEnterDuration)
                     .SetEase(uiSettings.mainPanelEase);
        }
        
        // 5. Populate village stats and setup power allocation
        PopulateVillageUI();
        
        // 6. Start day timer
        dayTimeRemaining = dayDuration;
        dayTimerCoroutine = StartCoroutine(DayTimer());
        
        yield return new WaitForSeconds(uiSettings.mainPanelEnterDuration);
        
        isTransitioning = false;
        OnDayStarted?.Invoke();
        OnDayNightTransition?.Invoke(true);
        
        if (debugTransitions)
            Debug.Log("Day transition completed");
    }
    
    private IEnumerator TransitionToNight()
    {
        isTransitioning = true;
        isDay = false;
        
        if (debugTransitions)
            Debug.Log("Starting transition to night");
        
        // Stop day timer
        if (dayTimerCoroutine != null)
        {
            StopCoroutine(dayTimerCoroutine);
            dayTimerCoroutine = null;
        }
        
        // 1. Setup wave section behind main panel with updated enemy counts
        if (waveSection != null)
        {
            UpdateWaveSectionText();
            
            waveSection.position = waveSectionOriginalPos;
            waveSection.gameObject.SetActive(true);
            
            // Ensure it's behind the main panel initially
            Canvas waveCanvas = waveSection.GetComponent<Canvas>();
            Canvas mainCanvas = mainPanel != null ? mainPanel.GetComponent<Canvas>() : null;
            if (waveCanvas != null && mainCanvas != null)
            {
                waveCanvas.sortingOrder = mainCanvas.sortingOrder - 1;
            }
        }
        
        // 2. Main panel collapses down
        if (mainPanel != null)
        {
            Vector3 exitPos = mainPanelOriginalPos + Vector3.up * uiSettings.mainPanelOffsetY;
            mainPanel.DOMove(exitPos, uiSettings.mainPanelExitDuration)
                     .SetEase(uiSettings.mainPanelEase)
                     .OnComplete(() => mainPanel.gameObject.SetActive(false));
        }
        
        // 3. Top area moves up and out
        if (topArea != null)
        {
            Vector3 exitPos = topAreaOriginalPos + Vector3.up * uiSettings.topAreaOffsetY;
            topArea.DOMove(exitPos, uiSettings.waveTextExitDuration)
                   .SetEase(uiSettings.waveTextEase)
                   .OnComplete(() => topArea.gameObject.SetActive(false));
        }
        
        yield return new WaitForSeconds(uiSettings.mainPanelExitDuration);
        
        // 4. Wave section stays in center briefly
        yield return new WaitForSeconds(uiSettings.waveTextCenterDisplayTime);
        
        // 5. Wave section moves to top left
        if (waveSection != null)
        {
            Vector3 topLeftPos = GetWaveSectionTopLeftPosition();
            waveSection.DOMove(topLeftPos, uiSettings.waveTextEnterDuration)
                      .SetEase(uiSettings.waveTextEase);
        }
        
        // 6. Transition lighting to night
        TransitionLighting(false);
        
        // 7. Start the wave once night transition is complete
        StartCoroutine(StartWaveAfterTransition());
        
        yield return new WaitForSeconds(uiSettings.waveTextEnterDuration);
        
        isTransitioning = false;
        OnNightStarted?.Invoke();
        OnDayNightTransition?.Invoke(false);
        
        if (debugTransitions)
            Debug.Log("Night transition completed");
    }
    
    private IEnumerator StartWaveAfterTransition()
    {
        yield return new WaitForSeconds(uiSettings.waveTextEnterDuration);
        
        // Now start the wave through the wave system
        if (waveSystem != null)
        {
            // Re-enable wave system and manually start the wave
            waveSystem.enabled = true;
            
            // Use reflection or a new public method to start specific wave
            StartWaveManually();
            
            if (debugTransitions)
                Debug.Log($"Started wave {currentWave} after night transition");
        }
    }
    
    private void StartWaveManually()
    {
        if (waveSystem != null)
        {
            waveSystem.StartWaveManually();
        }
    }
    
    private void SetupNightImmediate()
    {
        isDay = false;
        
        // Set night lighting immediately
        if (globalLight != null)
        {
            globalLight.color = lightingSettings.nightColor;
            globalLight.intensity = lightingSettings.nightIntensity;
        }
        
        // Position UI elements for night
        if (topArea != null)
            topArea.gameObject.SetActive(false);
        if (mainPanel != null)
            mainPanel.gameObject.SetActive(false);
        if (waveSection != null)
        {
            waveSection.gameObject.SetActive(true);
            waveSection.position = GetWaveSectionTopLeftPosition();
            UpdateWaveSectionText(); // Initialize with wave 1 data
        }
    }
    
    #endregion
    
    #region Lighting Control
    
    private void TransitionLighting(bool toDay)
    {
        if (globalLight == null) return;
        
        Color targetColor = toDay ? lightingSettings.dayColor : lightingSettings.nightColor;
        float targetIntensity = toDay ? lightingSettings.dayIntensity : lightingSettings.nightIntensity;
        
        DOTween.To(() => globalLight.color, x => globalLight.color = x, targetColor, lightingSettings.lightTransitionDuration)
               .SetEase(lightingSettings.lightTransitionEase);
        
        DOTween.To(() => globalLight.intensity, 
                   x => globalLight.intensity = x, 
                   targetIntensity, 
                   lightingSettings.lightTransitionDuration)
               .SetEase(lightingSettings.lightTransitionEase);
    }
    
    #endregion
    
    #region Village UI Population
    
    private void PopulateVillageUI()
    {
        if (debugTransitions)
            Debug.Log("Populating village UI");
        
        // Process the start of day cycle in village manager
        if (villageManager != null)
        {
            villageManager.ProcessDayCycle();
        }
        
        PopulateRulerSection();
        PopulateVillagerCards();
        UpdateFoodProduction();
        UpdatePowerTotals();
        
        // Trigger VillagePowerAllocationUI to refresh
        if (VillagePowerAllocationUI.Instance != null)
        {
            VillagePowerAllocationUI.Instance.RefreshAllUI();
        }
    }
    
    private void PopulateRulerSection()
    {
        // Update ruler power, damage, and health info
        // This connects to the existing power system
        if (debugTransitions)
            Debug.Log("Populating ruler section");
    }
    
    private void PopulateVillagerCards()
    {
        if (villagerContainer == null) return;
        
        // Get villagers from VillageManager instead of finding them manually
        List<Villager> allVillagers = new List<Villager>();
        
        if (villageManager != null)
        {
            allVillagers = villageManager.GetVillagers();
        }
        else
        {
            // Fallback to finding villagers in scene
            allVillagers.AddRange(FindObjectsOfType<Villager>());
        }
        
        // Sort villagers: Captain, Mage, Farmer first (purple names), then others
        allVillagers.Sort((a, b) => {
            int GetPriority(VillagerRole role)
            {
                switch (role)
                {
                    case VillagerRole.Captain: return 0;
                    case VillagerRole.Mage: return 1;
                    case VillagerRole.Farmer: return 2;
                    default: return 3;
                }
            }
            
            return GetPriority(a.GetRole()).CompareTo(GetPriority(b.GetRole()));
        });
        
        // Update villager UI cards with stats
        for (int i = 0; i < allVillagers.Count; i++)
        {
            UpdateVillagerCard(allVillagers[i], i);
        }
        
        if (debugTransitions)
            Debug.Log($"Populated {allVillagers.Count} villager cards");
    }
    
    private void UpdateVillagerCard(Villager villager, int index)
    {
        // This should update the individual villager UI card
        // The VillagePowerAllocationUI handles this automatically
        if (debugTransitions)
            Debug.Log($"Updated villager card for {villager.GetRole()} at index {index}");
    }
    
    private void UpdateFoodProduction()
    {
        if (villageManager != null)
        {
            int foodProduction = villageManager.CalculateFoodProduction();
            if (debugTransitions)
                Debug.Log($"Food production: {foodProduction}");
        }
    }
    
    private void UpdatePowerTotals()
    {
        // Update total power and unallocated power displays
        // This is handled by the VillagePowerAllocationUI system
        if (debugTransitions)
            Debug.Log("Updated power totals");
    }
    
    #endregion
    
    #region Day Timer
    
    private IEnumerator DayTimer()
    {
        while (dayTimeRemaining > 0)
        {
            dayTimeRemaining -= Time.deltaTime;
            UpdateCountdownDisplay();
            yield return null;
        }
        
        // Day time ended, process end-of-day and transition to night
        ProcessEndOfDay();
        StartNightTransition();
    }
    
    private void ProcessEndOfDay()
    {
        if (debugTransitions)
            Debug.Log("Processing end of day - applying power allocations and distributing food");
        
        // Apply power allocations made during the day
        ApplyPowerAllocations();
        
        // Process night cycle in village manager (food distribution, etc.)
        if (villageManager != null)
        {
            villageManager.ProcessNightCycle();
        }
    }
    
    private void ApplyPowerAllocations()
    {
        // Get all current power allocations from the UI and apply them
        if (VillagePowerAllocationUI.Instance != null)
        {
            // The power allocation UI should handle finalizing allocations
            // This triggers discontent calculations based on power given vs. needed
            if (debugTransitions)
                Debug.Log("Applied power allocations - discontent will be calculated");
        }
        
        // Get all villagers and process their power allocation effects
        if (villageManager != null)
        {
            var villagers = villageManager.GetVillagers();
            foreach (var villager in villagers)
            {
                // This triggers the discontent calculation in each villager
                villager.ProcessDiscontentAtAllocation();
            }
        }
    }
    
    private void UpdateCountdownDisplay()
    {
        if (countdownText != null)
        {
            int minutes = Mathf.FloorToInt(dayTimeRemaining / 60);
            int seconds = Mathf.FloorToInt(dayTimeRemaining % 60);
            countdownText.text = $"{minutes:00}:{seconds:00}";
        }
    }
    
    #endregion
    
    #region Utility Methods
    
    private Vector3 GetWaveSectionTopLeftPosition()
    {
        // Calculate top-left position for wave section
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 screenPos = new Vector3(100, Screen.height - 100, 10);
            return cam.ScreenToWorldPoint(screenPos);
        }
        return waveSectionOriginalPos + Vector3.up * 4 + Vector3.left * 6;
    }
    
    private void UpdateWaveSectionText()
    {
        // Update the three text components with current wave data
        if (nightText != null)
        {
            nightText.text = $"NIGHT {currentWave}";
        }
        
        // Get enemy counts from wave system
        var enemyCounts = GetCurrentWaveEnemyCounts();
        
        if (ghostText != null)
        {
            int ghostCount = enemyCounts.ContainsKey("Ghost") ? enemyCounts["Ghost"] : 0;
            ghostText.text = ghostCount.ToString();
        }
        
        if (ghostMageText != null)
        {
            int ghostMageCount = enemyCounts.ContainsKey("GhostMage") ? enemyCounts["GhostMage"] : 0;
            ghostMageText.text = ghostMageCount.ToString();
        }
        
        if (debugTransitions)
        {
            Debug.Log($"Updated wave section for Night {currentWave}: Ghosts={ghostText?.text}, GhostMages={ghostMageText?.text}");
        }
    }
    
    private System.Collections.Generic.Dictionary<string, int> GetCurrentWaveEnemyCounts()
    {
        var enemyCounts = new System.Collections.Generic.Dictionary<string, int>();
        
        if (waveSystem != null)
        {
            // Get actual wave composition from the wave system
            enemyCounts = waveSystem.GetNextWaveComposition();
            
            if (debugTransitions)
            {
                Debug.Log($"Wave {currentWave} composition:");
                foreach (var kvp in enemyCounts)
                {
                    Debug.Log($"  {kvp.Key}: {kvp.Value}");
                }
            }
        }
        else
        {
            // Fallback values for testing
            enemyCounts["Ghost"] = 5;
            enemyCounts["GhostMage"] = 2;
        }
        
        return enemyCounts;
    }
    
    #endregion
    
    #region Public Interface
    
    public bool IsDay() => isDay;
    public bool IsTransitioning() => isTransitioning;
    public float GetDayTimeRemaining() => dayTimeRemaining;
    public int GetCurrentWave() => currentWave;
    
    public void SetDayDuration(float duration)
    {
        dayDuration = duration;
    }
    
    public void ForceTransitionToDay()
    {
        if (!isTransitioning)
            StartDayTransition();
    }
    
    public void ForceTransitionToNight()
    {
        if (!isTransitioning)
            StartNightTransition();
    }
    
    #endregion
    
    private void OnDestroy()
    {
        // Clean up DOTween sequences
        DOTween.Kill(this);
        
        // Unsubscribe from events
        if (waveSystem != null)
        {
            waveSystem.OnWaveComplete -= HandleWaveComplete;
        }
    }
    
    #if UNITY_EDITOR
    [ContextMenu("Test Day Transition")]
    private void TestDayTransition()
    {
        if (Application.isPlaying)
            StartDayTransition();
    }
    
    [ContextMenu("Test Night Transition")]
    private void TestNightTransition()
    {
        if (Application.isPlaying)
            StartNightTransition();
    }
    
    [ContextMenu("Skip to Night")]
    private void TestSkipToNight()
    {
        if (Application.isPlaying)
            SkipToNight();
    }
    #endif
}