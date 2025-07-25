// HealthBar.cs - Simple health bar UI component
using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Image fillImage;
    [SerializeField] private Gradient healthGradient;
    [SerializeField] private bool autoHideWhenFull = true;
    
    private void Awake()
    {
        // Auto-find components if not assigned
        if (healthSlider == null)
        {
            healthSlider = GetComponent<Slider>();
            if (healthSlider == null)
            {
                healthSlider = GetComponentInChildren<Slider>();
            }
        }
        
        if (fillImage == null && healthSlider != null)
        {
            fillImage = healthSlider.fillRect?.GetComponent<Image>();
        }
        
        // Set up default gradient if not assigned
        if (healthGradient == null || healthGradient.colorKeys.Length == 0)
        {
            healthGradient = new Gradient();
            GradientColorKey[] colorKeys = new GradientColorKey[3];
            colorKeys[0] = new GradientColorKey(Color.red, 0.0f);
            colorKeys[1] = new GradientColorKey(Color.yellow, 0.5f);
            colorKeys[2] = new GradientColorKey(Color.green, 1.0f);
            
            GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
            alphaKeys[0] = new GradientAlphaKey(1.0f, 0.0f);
            alphaKeys[1] = new GradientAlphaKey(1.0f, 1.0f);
            
            healthGradient.SetKeys(colorKeys, alphaKeys);
        }
    }
    
    public void SetMaxHealth(int maxHealth)
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
        }
    }
    
    public void SetHealth(int health)
    {
        if (healthSlider != null)
        {
            healthSlider.value = health;
            
            // Update color based on health percentage
            if (fillImage != null && healthGradient != null)
            {
                float healthPercent = healthSlider.maxValue > 0 ? healthSlider.value / healthSlider.maxValue : 0f;
                fillImage.color = healthGradient.Evaluate(healthPercent);
            }
            
            // Auto-hide when full
            if (autoHideWhenFull)
            {
                bool isFull = health >= healthSlider.maxValue;
                gameObject.SetActive(!isFull);
            }
        }
    }
    
    public void Show()
    {
        gameObject.SetActive(true);
    }
    
    public void Hide()
    {
        gameObject.SetActive(false);
    }
}