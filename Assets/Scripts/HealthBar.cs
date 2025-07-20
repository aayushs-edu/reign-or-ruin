using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    [Header("Health Bar UI")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Image fillImage;
    
    [Header("Colors")]
    [SerializeField] private Color fullHealthColor = Color.green;
    [SerializeField] private Color lowHealthColor = Color.red;
    
    public void UpdateHealth(int currentHealth, int maxHealth)
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }
        
        if (fillImage != null && maxHealth > 0)
        {
            float healthPercent = (float)currentHealth / maxHealth;
            fillImage.color = Color.Lerp(lowHealthColor, fullHealthColor, healthPercent);
        }
    }
}