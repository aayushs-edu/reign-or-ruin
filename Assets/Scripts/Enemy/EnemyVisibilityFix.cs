using UnityEngine;

public class EnemyVisibilityFix : MonoBehaviour
{
    [Header("Sprite Settings")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 10; // Higher than background
    
    [Header("Debug")]
    [SerializeField] private bool debugSprite = true;
    
    private SpriteRenderer spriteRenderer;
    
    private void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        
        if (spriteRenderer == null)
        {
            Debug.LogError($"EnemyVisibilityFix: No SpriteRenderer found on {gameObject.name}!");
            return;
        }
        
        // Fix common visibility issues
        FixSpriteSettings();
        
        if (debugSprite)
        {
            LogSpriteInfo();
        }
    }
    
    private void FixSpriteSettings()
    {
        // Set sorting layer and order
        spriteRenderer.sortingLayerName = sortingLayerName;
        spriteRenderer.sortingOrder = sortingOrder;
        
        // Ensure sprite is visible
        Color color = spriteRenderer.color;
        color.a = 1f; // Full opacity
        spriteRenderer.color = color;
        
        // Make sure the sprite is enabled
        spriteRenderer.enabled = true;
        gameObject.SetActive(true);
    }
    
    private void LogSpriteInfo()
    {
        Debug.Log($"Enemy {gameObject.name} sprite info:" +
                 $"\n- Position: {transform.position}" +
                 $"\n- Sprite: {(spriteRenderer.sprite != null ? spriteRenderer.sprite.name : "NULL")}" +
                 $"\n- Color: {spriteRenderer.color}" +
                 $"\n- Sorting Layer: {spriteRenderer.sortingLayerName}" +
                 $"\n- Sorting Order: {spriteRenderer.sortingOrder}" +
                 $"\n- Enabled: {spriteRenderer.enabled}");
    }
    
    // Call this if enemies become invisible during gameplay
    public void ForceVisible()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
            Color color = spriteRenderer.color;
            color.a = 1f;
            spriteRenderer.color = color;
        }
    }
}