using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class PowerOrbCollectionManager : MonoBehaviour
{
    [Header("Collection Indicator UI")]
    [SerializeField] private TextMeshProUGUI collectionText;
    
    [Header("Animation Settings")]
    [SerializeField] private float popScale = 1.3f;
    [SerializeField] private float popDuration = 0.2f;
    [SerializeField] private float displayDuration = 1f; // How long to keep indicator visible after last collection
    
    [Header("Visual Effects")]
    [SerializeField] private Color baseTextColor = Color.cyan;
    [SerializeField] private Color popTextColor = Color.white;
    [SerializeField] private AnimationCurve fadeOutCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
    [SerializeField] private AnimationCurve popCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 1));
    
    // State tracking
    private int currentPowerTotal = 0;
    private float lastCollectionTime = 0f;
    private bool isIndicatorVisible = false;
    private Coroutine fadeOutCoroutine;
    private Coroutine popAnimationCoroutine;
    
    // Original state for restoration
    private Vector3 originalScale;
    private Color originalColor;
    
    // Singleton
    public static PowerOrbCollectionManager Instance { get; private set; }
    
    private void Awake()
    {
        // Singleton setup
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    
    private void Start()
    {
        InitializeUI();
    }
    
    private void InitializeUI()
    {
        if (collectionText == null)
        {
            Debug.LogError("PowerOrbCollectionManager: No collection text assigned!");
            return;
        }
        
        // Store original values
        originalScale = collectionText.transform.localScale;
        originalColor = collectionText.color;
        
        // Set initial color if different from original
        if (baseTextColor != originalColor)
        {
            collectionText.color = baseTextColor;
            originalColor = baseTextColor;
        }
        
        // Initialize as hidden
        collectionText.gameObject.SetActive(false);
        isIndicatorVisible = false;
    }
    
    private void Update()
    {
        // Check if we should hide the indicator
        if (isIndicatorVisible && Time.time - lastCollectionTime > displayDuration)
        {
            HideIndicator();
        }
    }
    
    public void OnOrbCollected(int powerValue, Vector3 orbPosition)
    {
        // Update total
        currentPowerTotal += powerValue;
        lastCollectionTime = Time.time;
        
        // Show indicator if hidden
        if (!isIndicatorVisible)
        {
            ShowIndicator();
        }
        
        // Update text
        UpdateIndicatorText();
        
        // Trigger pop animation
        TriggerPopAnimation();
        
        // Cancel any existing fade out
        if (fadeOutCoroutine != null)
        {
            StopCoroutine(fadeOutCoroutine);
            fadeOutCoroutine = null;
        }
    }
    
    private void ShowIndicator()
    {
        if (collectionText != null)
        {
            collectionText.gameObject.SetActive(true);
            isIndicatorVisible = true;
            
            // Reset to original state
            collectionText.transform.localScale = originalScale;
            collectionText.color = originalColor;
        }
    }
    
    private void HideIndicator()
    {
        if (isIndicatorVisible)
        {
            fadeOutCoroutine = StartCoroutine(FadeOutIndicator());
        }
    }
    
    private IEnumerator FadeOutIndicator()
    {
        if (collectionText == null) yield break;
        
        float elapsed = 0f;
        float fadeOutTime = 1f;
        
        Color startColor = collectionText.color;
        Vector3 startScale = collectionText.transform.localScale;
        
        while (elapsed < fadeOutTime)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / fadeOutTime;
            float curveValue = fadeOutCurve.Evaluate(progress);
            
            // Fade out text
            Color color = startColor;
            color.a = 1f - curveValue;
            collectionText.color = color;
            
            // Scale down slightly
            float scaleMultiplier = 1f - (curveValue * 0.2f);
            collectionText.transform.localScale = startScale * scaleMultiplier;
            
            yield return null;
        }
        
        // Hide completely
        collectionText.gameObject.SetActive(false);
        isIndicatorVisible = false;
        currentPowerTotal = 0; // Reset for next collection sequence
        fadeOutCoroutine = null;
    }
    
    private void UpdateIndicatorText()
    {
        if (collectionText != null)
        {
            collectionText.text = $"+{currentPowerTotal} Power";
        }
    }
    
    private void TriggerPopAnimation()
    {
        if (popAnimationCoroutine != null)
        {
            StopCoroutine(popAnimationCoroutine);
        }
        
        popAnimationCoroutine = StartCoroutine(PopAnimation());
    }
    
    private IEnumerator PopAnimation()
    {
        if (collectionText == null) yield break;
        
        float elapsed = 0f;
        
        while (elapsed < popDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / popDuration;
            
            // Scale animation using curve
            float scaleProgress = popCurve.Evaluate(progress);
            float currentScale = Mathf.Lerp(popScale, 1f, scaleProgress);
            collectionText.transform.localScale = originalScale * currentScale;
            
            // Color animation (flash to pop color then back)
            Color currentColor = Color.Lerp(popTextColor, originalColor, progress);
            collectionText.color = currentColor;
            
            yield return null;
        }
        
        // Ensure we end at the original values
        collectionText.transform.localScale = originalScale;
        collectionText.color = originalColor;
        
        popAnimationCoroutine = null;
    }
    
    // Public methods for customization
    public void SetDisplayDuration(float duration)
    {
        displayDuration = duration;
    }
    
    public void SetTextColors(Color baseColor, Color popColor)
    {
        baseTextColor = baseColor;
        popTextColor = popColor;
        originalColor = baseColor;
        
        if (collectionText != null)
        {
            collectionText.color = baseColor;
        }
    }
    
    // Force hide indicator (useful for testing or special cases)
    public void ForceHideIndicator()
    {
        if (fadeOutCoroutine != null)
        {
            StopCoroutine(fadeOutCoroutine);
            fadeOutCoroutine = null;
        }
        
        if (collectionText != null)
        {
            collectionText.gameObject.SetActive(false);
        }
        
        isIndicatorVisible = false;
        currentPowerTotal = 0;
    }
    
    // Get current state (useful for debugging)
    public bool IsIndicatorVisible() => isIndicatorVisible;
    public int GetCurrentTotal() => currentPowerTotal;
    public float GetTimeSinceLastCollection() => Time.time - lastCollectionTime;
    
    private void OnDestroy()
    {
        // Clear singleton reference
        if (Instance == this)
        {
            Instance = null;
        }
    }
}