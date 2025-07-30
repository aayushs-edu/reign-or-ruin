using UnityEngine;

[CreateAssetMenu(fileName = "Settings", menuName = "Balance of Power/Settings")]
public class Settings : ScriptableObject
{
    [Header("Villager Settings")]
    [Tooltip("Commoner Health")]
    public int commonerHealth = 25;
    [Tooltip("Farmer Health")]
    public int farmerHealth = 50;
    [Tooltip("Builder Health")]
    public int builderHealth = 75;
    [Tooltip("Mage Health")]
    public int mageHealth = 100;
    [Tooltip("Captain Health")]
    public int captainHealth = 150;
    
    [Header("Farmer Food Production")]
    [Tooltip("Base food production per farmer (Tier 0)")]
    public int farmerBaseFoodProduction = 2;
    
    [Tooltip("Food production per farmer at Tier 1 (2 power)")]
    public int farmerTier1FoodProduction = 4;
    
    [Tooltip("Food production per farmer at Tier 2 (4 power)")]
    public int farmerTier2FoodProduction = 8;

    [Header("Discontent Penalties")]
    [Tooltip("Discontent added per point of power shortage")]
    public float powerPenalty = 5f;
    
    [Tooltip("Discontent added per point of food shortage (0-1 range)")]
    public float foodPenalty = 10f;
    
    [Tooltip("Discontent added when hit by player friendly fire")]
    public float friendlyFirePenalty = 25f;
    
    [Tooltip("Discontent added when villager's building is destroyed")]
    public float buildingDestroyedPenalty = 15f;
    
    [Header("Recovery")]
    [Tooltip("Discontent reduced per night when well-fed and full power")]
    public float recoveryRate = 10f;
    
    [Header("Rebellion Thresholds")]
    [Tooltip("Discontent level that triggers rebellion")]
    public float rebellionThreshold = 100f;
    
    [Tooltip("Minimum discontent for cascade rebellion")]
    public float cascadeThreshold = 50f;
    
    [Tooltip("High discontent threshold for mass rebellion")]
    public float massRebellionThreshold = 80f;
    
    [Header("Captain Cascade Rules")]
    [Tooltip("If Captain rebels, all Commoners with this much discontent rebel too")]
    public float captainCascadeThreshold = 50f;
    
    [Header("Mass Rebellion Rules")]
    [Tooltip("Minimum number of rebels to trigger mass rebellion")]
    public int massRebellionMinCount = 3;
    
    [Tooltip("Minimum percentage of villagers rebelling to trigger mass rebellion")]
    [Range(0f, 1f)]
    public float massRebellionMinPercent = 0.4f;
}