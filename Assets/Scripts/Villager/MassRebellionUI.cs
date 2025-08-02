using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;

public class MassRebellionUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject bannerContainer;
    [SerializeField] private RectTransform bannerBackground;
    [SerializeField] private TextMeshProUGUI rebellionText;
    [SerializeField] private CanvasGroup bannerCanvasGroup;
    
    [Header("Animation Settings")]
    [SerializeField] private float targetBannerWidth = 1000f;
    [SerializeField] private float bannerExpandDuration = 1.2f;
    [SerializeField] private AnimationCurve bannerExpandCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private float textRevealDelay = 0.8f;
    [SerializeField] private float textRevealDuration = 0.8f;
    [SerializeField] private AnimationCurve textRevealCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 1));
    [SerializeField] private float bannerDisplayDuration = 4f;
    [SerializeField] private float bannerFadeOutDuration = 1f;
    
    [Header("Visual Settings")]
    [SerializeField] private Color bannerColor = new Color(0.8f, 0.1f, 0.1f, 0.9f);
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private string rebellionMessage = "MASS REBELLION";
    [SerializeField] private bool addTextEffects = true;
    [SerializeField] private float textShakeIntensity = 2f;
    [SerializeField] private float textPulseIntensity = 0.1f;
    
    [Header("Post Processing")]
    [SerializeField] private Volume postProcessingVolume;
    [SerializeField] private float vignetteTargetIntensity = 0.4f;
    [SerializeField] private float vignetteFadeInDuration = 2f;
    [SerializeField] private float vignetteDisplayDuration = 8f;
    [SerializeField] private float vignetteFadeOutDuration = 3f;
    [SerializeField] private bool autoFindPostProcessing = true;
    
    [Header("Audio")]
    [SerializeField] private AudioClip rebellionSound;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private float audioVolume = 0.8f;
    
    [Header("Debug")]
    [SerializeField] private bool debugMode = false;
    
    // Internal state
    private Vignette vignetteEffect;
    private bool isPlaying = false;
    private float originalBannerWidth;
    private Vector3 originalTextScale;
    private Color originalTextColor;
    private Coroutine currentAnimation;
    
    // References
    private VillageManager villageManager;
    
    private void Start()
    {
        villageManager = GetComponent<VillageManager>();
        if (villageManager == null)
        {
            Debug.LogError("MassRebellionUI: No VillageManager component found on the same GameObject!");
        }
        
        InitializeComponents();
        SetupInitialState();
    }
    
    private void InitializeComponents()
    {
        // Auto-find post processing volume if not assigned
        if (autoFindPostProcessing && postProcessingVolume == null)
        {
            postProcessingVolume = FindObjectOfType<Volume>();
            if (postProcessingVolume != null && debugMode)
            {
                Debug.Log($"MassRebellionUI: Auto-found post processing volume: {postProcessingVolume.name}");
            }
        }
        
        // Get vignette effect from post processing
        if (postProcessingVolume != null && postProcessingVolume.profile != null)
        {
            if (postProcessingVolume.profile.TryGet<Vignette>(out vignetteEffect))
            {
                if (debugMode)
                {
                    Debug.Log("MassRebellionUI: Found vignette effect in post processing profile");
                }
            }
            else
            {
                Debug.LogWarning("MassRebellionUI: No Vignette effect found in post processing profile!");
            }
        }
        
        // Setup audio source
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
        
        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.volume = audioVolume;
        }
        
        // Auto-find UI components if not assigned
        if (bannerContainer == null)
        {
            bannerContainer = transform.Find("BannerContainer")?.gameObject;
        }
        
        if (bannerBackground == null)
        {
            bannerBackground = transform.Find("BannerContainer/Background")?.GetComponent<RectTransform>();
        }
        
        if (rebellionText == null)
        {
            rebellionText = GetComponentInChildren<TextMeshProUGUI>();
        }
        
        if (bannerCanvasGroup == null)
        {
            bannerCanvasGroup = bannerContainer?.GetComponent<CanvasGroup>();
            if (bannerCanvasGroup == null && bannerContainer != null)
            {
                bannerCanvasGroup = bannerContainer.AddComponent<CanvasGroup>();
            }
        }
    }
    
    private void SetupInitialState()
    {
        // Store original values
        if (bannerBackground != null)
        {
            originalBannerWidth = bannerBackground.sizeDelta.x;
        }
        
        if (rebellionText != null)
        {
            originalTextScale = rebellionText.transform.localScale;
            originalTextColor = rebellionText.color;
            rebellionText.text = rebellionMessage;
            rebellionText.color = textColor;
        }
        
        // Hide banner initially
        SetBannerVisible(false);
        
        // Disable vignette initially
        if (vignetteEffect != null)
        {
            vignetteEffect.active = false;
            vignetteEffect.intensity.value = 0f;
        }
    }
    
    public void TriggerMassRebellion()
    {
        if (isPlaying)
        {
            if (debugMode)
            {
                Debug.Log("MassRebellionUI: Animation already playing, ignoring trigger");
            }
            return;
        }
        
        if (debugMode)
        {
            Debug.Log("MassRebellionUI: Triggering mass rebellion animation");
        }
        
        if (currentAnimation != null)
        {
            StopCoroutine(currentAnimation);
        }
        
        currentAnimation = StartCoroutine(PlayRebellionAnimation());
    }
    
    private IEnumerator PlayRebellionAnimation()
    {
        isPlaying = true;
        
        // Play audio
        if (audioSource != null && rebellionSound != null)
        {
            audioSource.PlayOneShot(rebellionSound);
        }
        
        // Start both banner and vignette animations simultaneously
        StartCoroutine(AnimateBanner());
        StartCoroutine(AnimateVignette());
        
        // Wait for all animations to complete
        yield return new WaitForSeconds(bannerExpandDuration + textRevealDuration + bannerDisplayDuration + bannerFadeOutDuration);
        
        isPlaying = false;
        currentAnimation = null;
        
        if (debugMode)
        {
            Debug.Log("MassRebellionUI: Animation sequence completed");
        }
    }
    
    private IEnumerator AnimateBanner()
    {
        // Reset banner state
        SetBannerVisible(true);
        
        if (bannerBackground != null)
        {
            Vector2 startSize = new Vector2(0f, bannerBackground.sizeDelta.y);
            Vector2 targetSize = new Vector2(targetBannerWidth, bannerBackground.sizeDelta.y);
            bannerBackground.sizeDelta = startSize;
        }
        
        if (rebellionText != null)
        {
            rebellionText.color = new Color(textColor.r, textColor.g, textColor.b, 0f);
            rebellionText.transform.localScale = originalTextScale * 0.5f;
        }
        
        if (bannerCanvasGroup != null)
        {
            bannerCanvasGroup.alpha = 1f;
        }
        
        // Animate banner expansion
        float elapsed = 0f;
        while (elapsed < bannerExpandDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / bannerExpandDuration;
            float curveValue = bannerExpandCurve.Evaluate(progress);
            
            if (bannerBackground != null)
            {
                float currentWidth = Mathf.Lerp(0f, targetBannerWidth, curveValue);
                Vector2 currentSize = bannerBackground.sizeDelta;
                currentSize.x = currentWidth;
                bannerBackground.sizeDelta = currentSize;
            }
            
            yield return null;
        }
        
        // Wait for text reveal delay
        yield return new WaitForSeconds(textRevealDelay - bannerExpandDuration);
        
        // Animate text reveal
        elapsed = 0f;
        while (elapsed < textRevealDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / textRevealDuration;
            float curveValue = textRevealCurve.Evaluate(progress);
            
            if (rebellionText != null)
            {
                // Fade in text
                Color currentColor = textColor;
                currentColor.a = Mathf.Lerp(0f, textColor.a, curveValue);
                rebellionText.color = currentColor;
                
                // Scale up text
                float currentScale = Mathf.Lerp(0.5f, 1f, curveValue);
                rebellionText.transform.localScale = originalTextScale * currentScale;
                
                // Add text effects
                if (addTextEffects)
                {
                    // Slight shake effect
                    Vector3 shakeOffset = Random.insideUnitSphere * textShakeIntensity * (1f - curveValue);
                    shakeOffset.z = 0f;
                    rebellionText.transform.localPosition = shakeOffset;
                    
                    // Pulse effect
                    float pulseScale = 1f + Mathf.Sin(Time.time * 10f) * textPulseIntensity * curveValue;
                    rebellionText.transform.localScale = originalTextScale * currentScale * pulseScale;
                }
            }
            
            yield return null;
        }
        
        // Display banner for duration
        yield return new WaitForSeconds(bannerDisplayDuration);
        
        // Fade out banner
        elapsed = 0f;
        while (elapsed < bannerFadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / bannerFadeOutDuration;
            
            if (bannerCanvasGroup != null)
            {
                bannerCanvasGroup.alpha = Mathf.Lerp(1f, 0f, progress);
            }
            
            yield return null;
        }
        
        // Hide banner
        SetBannerVisible(false);
        
        // Reset text position and scale
        if (rebellionText != null)
        {
            rebellionText.transform.localPosition = Vector3.zero;
            rebellionText.transform.localScale = originalTextScale;
            rebellionText.color = originalTextColor;
        }
    }
    
    private IEnumerator AnimateVignette()
    {
        if (vignetteEffect == null) yield break;
        
        // Enable vignette effect
        vignetteEffect.active = true;
        vignetteEffect.intensity.value = 0f;
        
        // Fade in vignette
        float elapsed = 0f;
        while (elapsed < vignetteFadeInDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / vignetteFadeInDuration;
            vignetteEffect.intensity.value = Mathf.Lerp(0f, vignetteTargetIntensity, progress);
            yield return null;
        }
        
        // Display vignette
        yield return new WaitForSeconds(vignetteDisplayDuration);
        
        // Fade out vignette
        elapsed = 0f;
        while (elapsed < vignetteFadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / vignetteFadeOutDuration;
            vignetteEffect.intensity.value = Mathf.Lerp(vignetteTargetIntensity, 0f, progress);
            yield return null;
        }
        
        // Disable vignette effect
        vignetteEffect.intensity.value = 0f;
        vignetteEffect.active = false;
    }
    
    private void SetBannerVisible(bool visible)
    {
        if (bannerContainer != null)
        {
            bannerContainer.SetActive(visible);
        }
    }
    
    // Public methods for customization
    public void SetBannerColor(Color color)
    {
        bannerColor = color;
        if (bannerBackground != null)
        {
            Image bgImage = bannerBackground.GetComponent<Image>();
            if (bgImage != null)
            {
                bgImage.color = color;
            }
        }
    }
    
    public void SetTextColor(Color color)
    {
        textColor = color;
        if (rebellionText != null)
        {
            rebellionText.color = color;
        }
    }
    
    public void SetRebellionMessage(string message)
    {
        rebellionMessage = message;
        if (rebellionText != null)
        {
            rebellionText.text = message;
        }
    }
    
    public void SetVignetteIntensity(float intensity)
    {
        vignetteTargetIntensity = intensity;
    }
    
    public void SetAnimationDurations(float bannerDuration, float textDuration, float displayDuration)
    {
        bannerExpandDuration = bannerDuration;
        textRevealDuration = textDuration;
        bannerDisplayDuration = displayDuration;
    }
    
    // Public getters
    public bool IsPlaying() => isPlaying;
    public float GetTotalAnimationDuration() => bannerExpandDuration + textRevealDuration + bannerDisplayDuration + bannerFadeOutDuration;
    
    // Debug methods
    [ContextMenu("Test Mass Rebellion Animation")]
    public void TestAnimation()
    {
        if (Application.isPlaying)
        {
            TriggerMassRebellion();
        }
    }
    
    [ContextMenu("Stop Animation")]
    public void StopAnimation()
    {
        if (currentAnimation != null)
        {
            StopCoroutine(currentAnimation);
            currentAnimation = null;
        }
        
        isPlaying = false;
        SetBannerVisible(false);
        
        if (vignetteEffect != null)
        {
            vignetteEffect.active = false;
            vignetteEffect.intensity.value = 0f;
        }
    }
    
    private void OnDestroy()
    {
        if (currentAnimation != null)
        {
            StopCoroutine(currentAnimation);
        }
    }
}