// BuildingHealth.cs
using UnityEngine;

public class BuildingHealth : Health
{
    [Header("Building Specific")]
    [SerializeField] private int[] damageSprites; // Indices for damage states
    [SerializeField] private Sprite[] buildingSprites; // Different damage state sprites
    [SerializeField] private bool isRepairable = true;
    [SerializeField] private int repairCostPerHP = 2;
    
    private int originalMaxHealth;
    
    protected override void Awake()
    {
        base.Awake();
        originalMaxHealth = maxHealth;
        showHealthBar = true; // Buildings should always show health
    }
    
    protected override void OnDamageTaken()
    {
        base.OnDamageTaken();
        UpdateBuildingSprite();
    }
    
    protected override void OnHealReceived()
    {
        base.OnHealReceived();
        UpdateBuildingSprite();
    }
    
    private void UpdateBuildingSprite()
    {
        if (buildingSprites == null || buildingSprites.Length == 0) return;
        
        float healthPercent = GetHealthPercentage();
        int spriteIndex = Mathf.FloorToInt((1f - healthPercent) * (buildingSprites.Length - 1));
        spriteIndex = Mathf.Clamp(spriteIndex, 0, buildingSprites.Length - 1);
        
        if (spriteRenderer != null && buildingSprites[spriteIndex] != null)
        {
            spriteRenderer.sprite = buildingSprites[spriteIndex];
        }
    }
    
    public bool CanRepair() => isRepairable && currentHealth < maxHealth;
    
    public int GetRepairCost()
    {
        int missingHealth = maxHealth - currentHealth;
        return missingHealth * repairCostPerHP;
    }
    
    public void Repair(int amount)
    {
        if (!isRepairable) return;
        Heal(amount);
    }
    
    protected override void Die()
    {
        // Buildings might leave rubble instead of disappearing
        if (deathEffectPrefab != null)
        {
            // The death effect could be a rubble prefab that stays
            GameObject rubble = Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);
            // Don't auto-destroy rubble
        }
        
        base.Die();
    }
}