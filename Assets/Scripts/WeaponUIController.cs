using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class WeaponUIController : MonoBehaviour
{
    [Header("Weapon Charge UI")]
    [SerializeField] private Slider weaponChargeBar;
    [SerializeField] private TextMeshProUGUI weaponChargeText;
    [SerializeField] private Color normalChargeColor = Color.white;
    [SerializeField] private Color lowChargeColor = Color.red;
    [SerializeField] private Color highChargeColor = Color.cyan;
    
    private ThrowableWeaponSystem weaponSystem;
    
    private void Start()
    {
        InitializeComponents();
        SetupEventListeners();
    }
    
    private void InitializeComponents()
    {
        weaponSystem = FindObjectOfType<ThrowableWeaponSystem>();
        
        if (weaponSystem == null)
        {
            Debug.LogError("WeaponUIController: ThrowableWeaponSystem not found!");
        }
    }
    
    private void SetupEventListeners()
    {
        if (weaponSystem != null)
        {
            weaponSystem.OnChargeChanged += UpdateChargeDisplay;
        }
    }
    
    private void UpdateChargeDisplay(float chargeLevel)
    {
        // Update charge bar
        if (weaponChargeBar != null)
        {
            weaponChargeBar.value = chargeLevel;
            
            // Color based on charge level
            Image fillImage = weaponChargeBar.fillRect?.GetComponent<Image>();
            if (fillImage != null)
            {
                if (chargeLevel < 0.2f)
                {
                    fillImage.color = lowChargeColor;
                }
                else if (chargeLevel > 0.8f)
                {
                    fillImage.color = highChargeColor;
                }
                else
                {
                    fillImage.color = Color.Lerp(normalChargeColor, highChargeColor, chargeLevel);
                }
            }
        }
        
        // Update charge text
        if (weaponChargeText != null)
        {
            weaponChargeText.text = $"Weapon Charge: {chargeLevel:P0}";
            
            // Change text color based on charge
            if (chargeLevel < 0.1f)
            {
                weaponChargeText.color = lowChargeColor;
            }
            else if (chargeLevel > 0.8f)
            {
                weaponChargeText.color = highChargeColor;
            }
            else
            {
                weaponChargeText.color = normalChargeColor;
            }
        }
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from events
        if (weaponSystem != null)
        {
            weaponSystem.OnChargeChanged -= UpdateChargeDisplay;
        }
    }
}