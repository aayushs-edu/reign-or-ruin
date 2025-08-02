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
    public System.Action OnDayStarted;
    public System.Action OnNightStarted;
    public System.Action<bool> OnDayNightTransition; // true = day, false = night
    public System.Action<int> OnWaveStarted;
    
    private void Start()
    {
        InitializeManager();
        // Start with a transition to night
        StartNightTransition();
    }
    
    private void InitializeManager()
    {
        StoreOriginalPositions();
        SetupEventListeners();
        SetupSkipButton();
        // Do not call SetupDayImmediate or SetupNightImmediate here; handled in Start()
        if (debugTransitions)
            Debug.Log("DayNightCycleManager initialized");
        if (waveSystem == null)
            Debug.LogError("DayNightCycleManager: No EnemyWaveSystem found!");
    }
    
    private void SetupEventListeners()
    {
        if (waveSystem != null)
        {
            waveSystem.OnWaveComplete += HandleWaveComplete;
        }
        
        if (villageManager != null)
        {
            villageManager.OnDayNightCycle += HandleVillageManagerCycle;
        }
    }
    
    private void SetupSkipButton()
    {
        if (skipButton != null)
        {
            skipButton.onClick.AddListener(SkipToNight);
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
        
        currentWave = waveNumber + 1;
        StartDayTransition();
    }
    
    private void HandleVillageManagerCycle(bool isNight)
    {
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
        
        // 1. Simultaneously start lighting transition and wave section exit
        TransitionLighting(true);
        
        if (waveSection != null)
        {
            Vector3 exitPos = waveSectionOriginalPos + Vector3.up * uiSettings.waveTextOffsetY;
            waveSection.DOMove(exitPos, uiSettings.waveTextExitDuration)
                      .SetEase(uiSettings.waveTextEase)
                      .OnComplete(() => waveSection.gameObject.SetActive(false));
        }
        
        yield return new WaitForSeconds(uiSettings.waveTextExitDuration * 0.5f);
        
        // 2. Top area slides down from above with proper start position
        if (topArea != null)
        {
            Vector3 startPos = topAreaOriginalPos + Vector3.up * uiSettings.topAreaOffsetY;
            topArea.position = startPos;
            topArea.gameObject.SetActive(true);
            
            topArea.DOMove(topAreaOriginalPos, uiSettings.topAreaEnterDuration)
                   .SetEase(uiSettings.topAreaEase);
        }
        
        yield return new WaitForSeconds(0.3f);
        
        // 3. Main panel slides up from below with proper start position  
        if (mainPanel != null)
        {
            Vector3 startPos = mainPanelOriginalPos + Vector3.down * Mathf.Abs(uiSettings.mainPanelOffsetY);
            mainPanel.position = startPos;
            mainPanel.gameObject.SetActive(true);
            
            mainPanel.DOMove(mainPanelOriginalPos, uiSettings.mainPanelEnterDuration)
                     .SetEase(uiSettings.mainPanelEase);
        }
        
        // 4. Setup village UI with updated data
        PopulateVillageUI();
        
        // 5. Start day timer
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
        
        if (dayTimerCoroutine != null)
        {
            StopCoroutine(dayTimerCoroutine);
            dayTimerCoroutine = null;
        }
        
        // 1. Setup wave section at center position using RectTransform
        if (waveSection != null)
        {
            UpdateWaveSectionText();
            RectTransform waveRect = waveSection as RectTransform;
            if (waveRect != null)
            {
                waveRect.anchoredPosition = new Vector2(0f, -130f);
            }
            waveSection.gameObject.SetActive(true);
            Canvas waveCanvas = waveSection.GetComponent<Canvas>();
            Canvas mainCanvas = mainPanel != null ? mainPanel.GetComponent<Canvas>() : null;
            if (waveCanvas != null && mainCanvas != null)
            {
                waveCanvas.sortingOrder = mainCanvas.sortingOrder - 1;
            }
        }
        
        // 2. Simultaneously start lighting transition and UI exit
        TransitionLighting(false);
        
        // Main panel slides down
        if (mainPanel != null)
        {
            Vector3 exitPos = mainPanelOriginalPos + Vector3.down * Mathf.Abs(uiSettings.mainPanelOffsetY);
            mainPanel.DOMove(exitPos, uiSettings.mainPanelExitDuration)
                     .SetEase(uiSettings.mainPanelEase)
                     .OnComplete(() => mainPanel.gameObject.SetActive(false));
        }
        
        // Top area slides up
        if (topArea != null)
        {
            Vector3 exitPos = topAreaOriginalPos + Vector3.up * uiSettings.topAreaOffsetY;
            topArea.DOMove(exitPos, uiSettings.mainPanelExitDuration)
                   .SetEase(uiSettings.topAreaEase)
                   .OnComplete(() => topArea.gameObject.SetActive(false));
        }
        
        yield return new WaitForSeconds(uiSettings.mainPanelExitDuration);
        
        // 3. Wave section displays in center
        yield return new WaitForSeconds(uiSettings.waveTextCenterDisplayTime);
        
        // 4. Wave section moves to top left (original position)
        if (waveSection != null)
        {
            waveSection.DOMove(waveSectionOriginalPos, uiSettings.waveTextEnterDuration)
                      .SetEase(uiSettings.waveTextEase);
        }
        
        yield return new WaitForSeconds(uiSettings.waveTextEnterDuration);
        
        // 5. Start wave after positioning is complete
        StartCoroutine(StartWaveAfterTransition());
        
        isTransitioning = false;
        OnNightStarted?.Invoke();
        OnDayNightTransition?.Invoke(false);
        
        if (debugTransitions)
            Debug.Log("Night transition completed");
    }
    
    private IEnumerator StartWaveAfterTransition()
    {
        yield return new WaitForSeconds(0.2f);
        
        if (waveSystem != null)
        {
            waveSystem.enabled = true;
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
    
    private void SetupDayImmediate()
    {
        isDay = true;
        
        if (globalLight != null)
        {
            globalLight.color = lightingSettings.dayColor;
            globalLight.intensity = lightingSettings.dayIntensity;
        }
        
        if (waveSection != null)
            waveSection.gameObject.SetActive(false);
        if (topArea != null)
        {
            topArea.gameObject.SetActive(true);
            topArea.position = topAreaOriginalPos;
        }
        if (mainPanel != null)
        {
            mainPanel.gameObject.SetActive(true);
            mainPanel.position = mainPanelOriginalPos;
        }
        
        PopulateVillageUI();
        dayTimeRemaining = dayDuration;
        dayTimerCoroutine = StartCoroutine(DayTimer());
    }
    
    #endregion
    
    #region Lighting Control
    
    private void TransitionLighting(bool toDay)
    {
        if (globalLight == null) return;
        
        Color targetColor = toDay ? lightingSettings.dayColor : lightingSettings.nightColor;
        float targetIntensity = toDay ? lightingSettings.dayIntensity : lightingSettings.nightIntensity;
        
        DOTween.To(() => globalLight.color,
                   x => globalLight.color = x,
                   targetColor,
                   lightingSettings.lightTransitionDuration)
               .SetEase(lightingSettings.lightTransitionEase);
        
        DOTween.To(() => globalLight.intensity, 
                   x => globalLight.intensity = x, 
                   targetIntensity, 
                   lightingSettings.lightTransitionDuration)
               .SetEase(lightingSettings.lightTransitionEase);
    }
    
    #endregion
    
    #region UI Population and Updates
    
    private void PopulateVillageUI()
    {
        if (VillagePowerAllocationUI.Instance != null)
        {
            VillagePowerAllocationUI.Instance.RefreshAllUI();
        }
        
        UpdateCountdownDisplay();
        
        if (debugTransitions)
            Debug.Log("Village UI populated with current data");
    }
    
    private void UpdateCountdownDisplay()
    {
        if (countdownText != null && isDay)
        {
            int minutes = Mathf.FloorToInt(dayTimeRemaining / 60);
            int seconds = Mathf.FloorToInt(dayTimeRemaining % 60);
            countdownText.text = $"{minutes:00}:{seconds:00}";
        }
    }
    
    private void UpdateWaveSectionText()
    {
        if (nightText != null)
            nightText.text = $"Night {currentWave}";
        
        if (waveSystem != null)
        {
            // Get the composition for the upcoming wave
            var enemyCounts = waveSystem.GetNextWaveComposition();
            
            if (ghostText != null)
            {
                int ghostCount = 0;
                // Check for various possible ghost names
                if (enemyCounts.ContainsKey("Ghost"))
                    ghostCount += enemyCounts["Ghost"];
                if (enemyCounts.ContainsKey("ghost"))
                    ghostCount += enemyCounts["ghost"];
                
                ghostText.text = $"X {ghostCount}";
            }
            
            if (ghostMageText != null)
            {
                int ghostMageCount = 0;
                // Check for various possible ghost mage names
                if (enemyCounts.ContainsKey("Ghost Mage"))
                    ghostMageCount += enemyCounts["Ghost Mage"];
                if (enemyCounts.ContainsKey("GhostMage"))
                    ghostMageCount += enemyCounts["GhostMage"];
                if (enemyCounts.ContainsKey("ghost mage"))
                    ghostMageCount += enemyCounts["ghost mage"];
                
                ghostMageText.text = $"X {ghostMageCount}";
            }
        }
    }
    
    #endregion
    
    #region Day Timer
    
    private IEnumerator DayTimer()
    {
        while (dayTimeRemaining > 0 && isDay)
        {
            dayTimeRemaining -= Time.deltaTime;
            UpdateCountdownDisplay();
            yield return null;
        }
        
        if (isDay)
        {
            ProcessEndOfDay();
            StartNightTransition();
        }
    }
    
    private void ProcessEndOfDay()
    {
        if (debugTransitions)
            Debug.Log("Processing end of day - applying power allocations");
        
        ApplyPowerAllocations();
        
        if (villageManager != null)
        {
            villageManager.ProcessNightCycle();
        }
    }
    
    private void ApplyPowerAllocations()
    {
        if (VillagePowerAllocationUI.Instance != null)
        {
            VillagePowerAllocationUI.Instance.ApplyAllPowerAllocations();
        }
    }
    
    #endregion
    
    #region Utility Methods
    
    private Vector3 GetScreenCenterPosition()
    {
        if (mainPanel != null)
        {
            Canvas canvas = mainPanel.GetComponent<Canvas>();
            if (canvas != null)
            {
                return Vector3.zero; // Center for UI Canvas
            }
        }
        return Vector3.zero;
    }

    public void SetDayDuration(float duration)
    {
        dayDuration = duration;
    }
    
    public int GetCurrentWave() => currentWave;
    public bool IsDay() => isDay;
    public bool IsTransitioning() => isTransitioning;
    public float GetDayTimeRemaining() => dayTimeRemaining;
    
    #endregion
}