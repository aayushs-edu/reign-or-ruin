using UnityEngine;

public class StatusIndicatorManager : MonoBehaviour
{
    [Header("Indicator References")]
    [SerializeField] private GameObject lowFoodIndicator;
    [SerializeField] private SpriteRenderer powerTierIndicator;
    [SerializeField] private SpriteRenderer discontentIndicator;

    [Header("Thresholds")]
    [SerializeField] private float foodThreshold = 0.3f; // Threshold for low food indicator

    
    [Header("Power Tier Sprites")]
    [SerializeField] private Sprite noPowerSprite;
    [SerializeField] private Sprite tier1Sprite;
    [SerializeField] private Sprite tier2Sprite;
    
    [Header("Discontent Colors")]
    [SerializeField] private Color loyalColor = Color.green;
    [SerializeField] private Color angryColor = new Color(1f, 0.65f, 0f); // Orange
    [SerializeField] private Color rebelColor = Color.red;
    
    [Header("Debug")]
    [SerializeField] private bool debugMode = false;
    
    // State tracking
    private bool indicatorsVisible = true;
    private bool currentLowFoodState = false;
    private int currentPowerTier = 0;
    private float currentDiscontent = 0f;
    private VillagerState currentState = VillagerState.Loyal;

    // Thresholds
    
    private void Start()
    {
        ValidateReferences();
        InitializeIndicators();
    }
    
    private void ValidateReferences()
    {
        if (lowFoodIndicator == null)
            Debug.LogWarning($"StatusIndicatorManager on {transform.parent.name}: Low Food Indicator not assigned!");
            
        if (powerTierIndicator == null)
            Debug.LogWarning($"StatusIndicatorManager on {transform.parent.name}: Power Tier Indicator not assigned!");
            
        if (discontentIndicator == null)
            Debug.LogWarning($"StatusIndicatorManager on {transform.parent.name}: Discontent Indicator not assigned!");
            
        // Validate power sprites
        if (powerTierIndicator != null)
        {
            if (noPowerSprite == null)
                Debug.LogWarning($"StatusIndicatorManager on {transform.parent.name}: No Power Sprite not assigned!");
            if (tier1Sprite == null)
                Debug.LogWarning($"StatusIndicatorManager on {transform.parent.name}: Tier 1 Sprite not assigned!");
            if (tier2Sprite == null)
                Debug.LogWarning($"StatusIndicatorManager on {transform.parent.name}: Tier 2 Sprite not assigned!");
        }
    }
    
    private void InitializeIndicators()
    {
        // Set initial states
        if (lowFoodIndicator != null)
        {
            lowFoodIndicator.SetActive(false);
        }
        
        if (powerTierIndicator != null)
        {
            // Start with no power sprite
            if (noPowerSprite != null)
            {
                powerTierIndicator.sprite = noPowerSprite;
            }
        }
        
        if (discontentIndicator != null)
        {
            discontentIndicator.color = loyalColor;
        }
    }
    
    public void UpdateFoodStatus(bool isLowFood)
    {
        currentLowFoodState = isLowFood;
        
        if (lowFoodIndicator != null)
        {
            lowFoodIndicator.SetActive(isLowFood && indicatorsVisible);
            
            if (debugMode && isLowFood)
            {
                Debug.Log($"{transform.parent.name}: Low food indicator activated");
            }
        }
    }
    
    public void UpdatePowerTier(int tier)
    {
        currentPowerTier = tier;
        
        if (powerTierIndicator == null) return;
        
        // Update visibility based on tier and indicators visibility
        bool shouldShow = indicatorsVisible;
        powerTierIndicator.gameObject.SetActive(shouldShow);
        
        // Update sprite based on tier
        switch (tier)
        {
            case 0:
                if (noPowerSprite != null)
                    powerTierIndicator.sprite = noPowerSprite;
                break;
            case 1:
                if (tier1Sprite != null)
                    powerTierIndicator.sprite = tier1Sprite;
                break;
            case 2:
                if (tier2Sprite != null)
                    powerTierIndicator.sprite = tier2Sprite;
                break;
            default:
                Debug.LogWarning($"Unknown power tier: {tier}");
                break;
        }
        
        if (debugMode)
        {
            Debug.Log($"{transform.parent.name}: Power tier updated to {tier}");
        }
    }
    
    public void UpdateDiscontentIndicator(float discontent, VillagerState state)
    {
        currentDiscontent = discontent;
        currentState = state;
        
        if (discontentIndicator == null) return;
        
        // Always keep discontent indicator visible (when indicators are visible)
        discontentIndicator.gameObject.SetActive(indicatorsVisible);
        
        // Update color based on state
        Color targetColor = loyalColor;
        
        switch (state)
        {
            case VillagerState.Loyal:
                targetColor = loyalColor;
                break;
            case VillagerState.Angry:
                targetColor = angryColor;
                break;
            case VillagerState.Rebel:
                targetColor = rebelColor;
                break;
        }
        
        discontentIndicator.color = targetColor;
        
        if (debugMode)
        {
            Debug.Log($"{transform.parent.name}: Discontent {discontent:F0}%, State: {state}");
        }
    }
    
    // Update all indicators at once
    public void UpdateAllIndicators(float food, int powerTier, float discontent, VillagerState state)
    {
        UpdateFoodStatus(food < foodThreshold);
        UpdatePowerTier(powerTier);
        UpdateDiscontentIndicator(discontent, state);
    }
    
    // Hide all indicators
    public void HideAllIndicators()
    {
        indicatorsVisible = false;
        
        if (lowFoodIndicator != null)
            lowFoodIndicator.SetActive(false);
            
        if (powerTierIndicator != null)
            powerTierIndicator.gameObject.SetActive(false);
            
        if (discontentIndicator != null)
            discontentIndicator.gameObject.SetActive(false);
    }
    
    // Show all active indicators
    public void ShowAllIndicators()
    {
        indicatorsVisible = true;
        
        // Restore previous states
        if (lowFoodIndicator != null)
        {
            lowFoodIndicator.SetActive(currentLowFoodState);
        }
        
        if (powerTierIndicator != null)
        {
            powerTierIndicator.gameObject.SetActive(currentPowerTier > 0);
        }
        
        if (discontentIndicator != null)
        {
            discontentIndicator.gameObject.SetActive(true);
        }
    }
    
    // Set visibility state for all indicators
    public void SetIndicatorsVisible(bool visible)
    {
        if (visible)
        {
            ShowAllIndicators();
        }
        else
        {
            HideAllIndicators();
        }
    }
    
    // Force refresh all indicators
    public void RefreshIndicators()
    {
        if (!indicatorsVisible) return;
        
        // Re-apply current states
        if (lowFoodIndicator != null)
            lowFoodIndicator.SetActive(currentLowFoodState);
            
        if (powerTierIndicator != null)
            powerTierIndicator.gameObject.SetActive(currentPowerTier > 0);
            
        if (discontentIndicator != null)
            discontentIndicator.gameObject.SetActive(true);
    }
    
    // Get current states (useful for debugging)
    public bool IsLowFood() => currentLowFoodState;
    public int GetPowerTier() => currentPowerTier;
    public float GetDiscontent() => currentDiscontent;
    public float GetFoodThreshold() => foodThreshold;
    public VillagerState GetState() => currentState;
    public bool AreIndicatorsVisible() => indicatorsVisible;
}