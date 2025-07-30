using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;
using System.Collections;

public class CaptainInfluenceSystem : MonoBehaviour
{
    [Header("Influence Configuration")]
    [SerializeField] private float baseInfluenceRadius = 8f;
    [SerializeField] private float influenceRadiusPerTier = 3f;
    [SerializeField] private LayerMask commonerLayer = -1;
    [SerializeField] private float influenceUpdateRate = 0.2f; // Update 5 times per second
    
    [Header("Commoner Stat Boosts")]
    [SerializeField] private float baseDamageBoost = 0.25f; // 25% damage boost at tier 0
    [SerializeField] private float damageBoostPerTier = 0.25f; // Additional 25% per tier
    [SerializeField] private float baseSpeedBoost = 0.15f; // 15% speed boost at tier 0
    [SerializeField] private float speedBoostPerTier = 0.15f; // Additional 15% per tier
    [SerializeField] private float baseDamageReduction = 0.1f; // 10% damage reduction at tier 0
    [SerializeField] private float damageReductionPerTier = 0.1f; // Additional 10% per tier
    [SerializeField] private float baseCooldownReduction = 0.1f; // 10% cooldown reduction at tier 0
    [SerializeField] private float cooldownReductionPerTier = 0.1f; // Additional 10% per tier
    
    [Header("Following System")]
    [SerializeField] private int baseFollowerCount = 0; // Base followers at tier 0
    [SerializeField] private int followersPerTier = 2; // Additional followers per tier
    [SerializeField] private float followDistance = 3f; // Distance followers maintain
    [SerializeField] private float followSpacing = 1.5f; // Space between followers
    [SerializeField] private float followUpdateRate = 0.1f; // How often to update follow positions
    [SerializeField] private float followSmoothing = 5f; // How smoothly followers move to positions
    
    [Header("AoE Visual System")]
    [SerializeField] private Transform aoeContainer; // Parent object containing AoE Circle and AoE Particles
    [SerializeField] private Transform aoeCircle; // The visual circle
    [SerializeField] private ParticleSystem aoeParticles; // Particles emitting from boundary
    [SerializeField] private float circleScaleOffset = 0.5f; // Circle is 0.5 units larger than particle radius
    [SerializeField] private Color aoeColor = new Color(0f, 1f, 0f, 0.3f); // Color for entire AoE system
    [SerializeField] private bool autoFindAoEComponents = true;
    [SerializeField] private bool showInfluenceRange = true;
    
    [Header("Debug")]
    [SerializeField] private bool debugInfluence = false;
    [SerializeField] private bool debugFollowing = false;
    
    // References
    private Villager captain;
    private CaptainCombat captainCombat;
    
    // Influence system
    private List<CommonerCombat> influencedCommoners = new List<CommonerCombat>();
    private List<CommonerCombat> followerCommoners = new List<CommonerCombat>();
    private float currentInfluenceRadius;
    private int currentMaxFollowers;
    private float lastInfluenceUpdate = 0f;
    private float lastFollowUpdate = 0f;
    
    // AoE Visual Components
    private SpriteRenderer aoeCircleRenderer;
    
    // Follow positions
    private List<Vector3> followPositions = new List<Vector3>();
    
    private void Awake()
    {
        captain = GetComponent<Villager>();
        captainCombat = GetComponent<CaptainCombat>();
        
        if (captain == null)
        {
            Debug.LogError($"CaptainInfluenceSystem: No Villager component found on {gameObject.name}!");
        }
        
        if (captain.GetRole() != VillagerRole.Captain)
        {
            Debug.LogWarning($"CaptainInfluenceSystem: Villager {gameObject.name} is not a Captain!");
        }
    }
    
    private void Start()
    {
        SetupAoEVisualSystem();
        UpdateInfluenceStats();
        UpdateInfluenceSystem();
    }
    
    private void Update()
    {
        // Update influence system periodically
        if (Time.time - lastInfluenceUpdate >= influenceUpdateRate)
        {
            UpdateInfluenceSystem();
            lastInfluenceUpdate = Time.time;
        }
        
        // Update follow positions periodically
        if (Time.time - lastFollowUpdate >= followUpdateRate)
        {
            UpdateFollowSystem();
            lastFollowUpdate = Time.time;
        }
    }
    
    private void SetupAoEVisualSystem()
    {
        // Auto-find AoE components if enabled
        if (autoFindAoEComponents)
        {
            FindAoEComponents();
        }
        
        // Initialize AoE visual components
        InitializeAoEComponents();
        
        // Set initial color
        SetAoEColor(aoeColor);
    }
    
    private void FindAoEComponents()
    {
        // Find AoE container
        if (aoeContainer == null)
        {
            Transform found = transform.Find("AoE");
            if (found == null)
            {
                // Try alternative names
                foreach (Transform child in transform)
                {
                    if (child.name.ToLower().Contains("aoe") || child.name.ToLower().Contains("influence"))
                    {
                        found = child;
                        break;
                    }
                }
            }
            aoeContainer = found;
        }
        
        if (aoeContainer != null)
        {
            // Find AoE Circle
            if (aoeCircle == null)
            {
                Transform circleFound = aoeContainer.Find("AoE Circle");
                if (circleFound == null)
                {
                    // Try alternative names
                    foreach (Transform child in aoeContainer)
                    {
                        if (child.name.ToLower().Contains("circle"))
                        {
                            circleFound = child;
                            break;
                        }
                    }
                }
                aoeCircle = circleFound;
            }
            
            // Find AoE Particles
            if (aoeParticles == null)
            {
                ParticleSystem particlesFound = aoeContainer.GetComponentInChildren<ParticleSystem>();
                if (particlesFound == null)
                {
                    Transform particlesTransform = aoeContainer.Find("AoE Particles");
                    if (particlesTransform != null)
                    {
                        particlesFound = particlesTransform.GetComponent<ParticleSystem>();
                    }
                }
                aoeParticles = particlesFound;
            }
        }
        
        if (debugInfluence)
        {
            Debug.Log($"CaptainInfluenceSystem: AoE components found - Container: {aoeContainer != null}, " +
                     $"Circle: {aoeCircle != null}, Particles: {aoeParticles != null}");
        }
    }
    
    private void InitializeAoEComponents()
    {
        // Initialize circle renderer
        if (aoeCircle != null)
        {
            aoeCircleRenderer = aoeCircle.GetComponent<SpriteRenderer>();
            if (aoeCircleRenderer == null)
            {
                Debug.LogWarning($"CaptainInfluenceSystem: AoE Circle {aoeCircle.name} has no SpriteRenderer!");
            }
        }
        
        // Initialize particle system modules
        if (aoeParticles != null)
        {
            var shape = aoeParticles.shape;
            
            // Ensure shape is set to circle
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radiusThickness = 0f; // Emit from edge only
        }
    }
    
    private void UpdateInfluenceStats()
    {
        if (captain == null) return;
        
        VillagerStats stats = captain.GetStats();
        
        // Calculate current influence radius based on tier
        currentInfluenceRadius = baseInfluenceRadius + (stats.tier * influenceRadiusPerTier);
        
        // Calculate current max followers based on tier
        currentMaxFollowers = baseFollowerCount + (stats.tier * followersPerTier);
        
        // Update visual indicator
        UpdateAoEVisualSystem();
        
        if (debugInfluence)
        {
            Debug.Log($"Captain {gameObject.name} (Tier {stats.tier}): Influence radius {currentInfluenceRadius:F1}, Max followers {currentMaxFollowers}");
        }
    }
    
    private void UpdateInfluenceSystem()
    {
        UpdateInfluenceStats();
        
        // Find all commoners in range
        List<CommonerCombat> nearbyCommoners = FindNearbyCommoners();
        
        // Remove influence from commoners no longer in range
        foreach (var commoner in influencedCommoners.ToArray())
        {
            if (!nearbyCommoners.Contains(commoner) || commoner == null)
            {
                RemoveInfluenceFromCommoner(commoner);
            }
        }
        
        // Add influence to new commoners in range
        foreach (var commoner in nearbyCommoners)
        {
            if (!influencedCommoners.Contains(commoner))
            {
                ApplyInfluenceToCommoner(commoner);
            }
        }
        
        // Update follower assignment
        UpdateFollowerAssignment();
        
        if (debugInfluence && influencedCommoners.Count > 0)
        {
            Debug.Log($"Captain {gameObject.name} influencing {influencedCommoners.Count} commoners, {followerCommoners.Count} following");
        }
    }
    
    private List<CommonerCombat> FindNearbyCommoners()
    {
        List<CommonerCombat> nearbyCommoners = new List<CommonerCombat>();
        
        // Find all colliders within influence radius
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, currentInfluenceRadius, commonerLayer);
        
        foreach (var collider in colliders)
        {
            // Skip self
            if (collider.gameObject == gameObject) continue;
            
            // Check if it's a commoner
            Villager villagerComponent = collider.GetComponent<Villager>();
            if (villagerComponent != null && villagerComponent.GetRole() == VillagerRole.Commoner)
            {
                // Only influence loyal commoners (not rebels)
                if (villagerComponent.IsLoyal() || villagerComponent.IsAngry())
                {
                    CommonerCombat commonerCombat = collider.GetComponent<CommonerCombat>();
                    if (commonerCombat != null)
                    {
                        nearbyCommoners.Add(commonerCombat);
                    }
                }
            }
        }
        
        return nearbyCommoners;
    }
    
    private void ApplyInfluenceToCommoner(CommonerCombat commoner)
    {
        if (commoner == null || captain == null) return;
        
        VillagerStats stats = captain.GetStats();
        
        // Calculate boost values based on captain's tier
        float damageBoost = baseDamageBoost + (stats.tier * damageBoostPerTier);
        float speedBoost = baseSpeedBoost + (stats.tier * speedBoostPerTier);
        float damageReduction = baseDamageReduction + (stats.tier * damageReductionPerTier);
        float cooldownReduction = baseCooldownReduction + (stats.tier * cooldownReductionPerTier);
        
        // Apply influence through the commoner's combat system
        commoner.ApplyCaptainInfluence(captainCombat, damageBoost, speedBoost, damageReduction, cooldownReduction);
        
        // Add to influenced list
        influencedCommoners.Add(commoner);
        
        if (debugInfluence)
        {
            Debug.Log($"Captain {gameObject.name} (Tier {stats.tier}) applied influence to {commoner.name}: " +
                     $"Damage +{damageBoost:P0}, Speed +{speedBoost:P0}, Damage Reduction {damageReduction:P0}, Cooldown -{cooldownReduction:P0}");
        }
    }
    
    private void RemoveInfluenceFromCommoner(CommonerCombat commoner)
    {
        if (commoner == null) return;
        
        // Remove influence through the commoner's combat system
        commoner.RemoveCaptainInfluence(captainCombat);
        
        // Remove from influenced list
        influencedCommoners.Remove(commoner);
        
        // Remove from followers if they were following
        RemoveFromFollowers(commoner);
        
        if (debugInfluence)
        {
            Debug.Log($"Captain {gameObject.name} removed influence from {commoner.name}");
        }
    }
    
    private void UpdateFollowerAssignment()
    {
        // Remove dead or invalid followers
        followerCommoners.RemoveAll(f => f == null || !influencedCommoners.Contains(f));
        
        // If we have too many followers, remove the furthest ones
        while (followerCommoners.Count > currentMaxFollowers)
        {
            CommonerCombat furthest = GetFurthestFollower();
            if (furthest != null)
            {
                RemoveFromFollowers(furthest);
            }
            else
            {
                break; // Safety break
            }
        }
        
        // If we need more followers, add closest influenced commoners
        while (followerCommoners.Count < currentMaxFollowers && followerCommoners.Count < influencedCommoners.Count)
        {
            CommonerCombat closest = GetClosestNonFollower();
            if (closest != null)
            {
                AddToFollowers(closest);
            }
            else
            {
                break; // No more available commoners
            }
        }
        
        // Update follow positions
        CalculateFollowPositions();
    }
    
    private CommonerCombat GetFurthestFollower()
    {
        CommonerCombat furthest = null;
        float maxDistance = 0f;
        
        foreach (var follower in followerCommoners)
        {
            if (follower == null) continue;
            
            float distance = Vector3.Distance(transform.position, follower.transform.position);
            if (distance > maxDistance)
            {
                maxDistance = distance;
                furthest = follower;
            }
        }
        
        return furthest;
    }
    
    private CommonerCombat GetClosestNonFollower()
    {
        CommonerCombat closest = null;
        float minDistance = float.MaxValue;
        
        foreach (var commoner in influencedCommoners)
        {
            if (commoner == null || followerCommoners.Contains(commoner)) continue;
            
            float distance = Vector3.Distance(transform.position, commoner.transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                closest = commoner;
            }
        }
        
        return closest;
    }
    
    private void AddToFollowers(CommonerCombat commoner)
    {
        if (commoner == null || followerCommoners.Contains(commoner)) return;
        
        followerCommoners.Add(commoner);
        
        // Notify commoner that they are now following
        VillagerAI ai = commoner.GetComponent<VillagerAI>();
        if (ai != null)
        {
            // Set the commoner to follow this captain
            ai.SetFollowTarget(transform);
        }
        
        if (debugFollowing)
        {
            Debug.Log($"Commoner {commoner.name} is now following Captain {gameObject.name}");
        }
    }
    
    private void RemoveFromFollowers(CommonerCombat commoner)
    {
        if (commoner == null) return;
        
        followerCommoners.Remove(commoner);
        
        // Notify commoner that they are no longer following
        VillagerAI ai = commoner.GetComponent<VillagerAI>();
        if (ai != null)
        {
            ai.ClearFollowTarget();
        }
        
        if (debugFollowing)
        {
            Debug.Log($"Commoner {commoner.name} stopped following Captain {gameObject.name}");
        }
    }
    
    private void CalculateFollowPositions()
    {
        followPositions.Clear();
        
        if (followerCommoners.Count == 0) return;
        
        // Calculate positions in a circle around the captain
        for (int i = 0; i < followerCommoners.Count; i++)
        {
            float angle = (i * 360f / followerCommoners.Count) * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * followDistance;
            Vector3 followPos = transform.position + offset;
            
            followPositions.Add(followPos);
        }
    }
    
    private void UpdateFollowSystem()
    {
        if (followerCommoners.Count == 0 || followPositions.Count != followerCommoners.Count) return;
        
        for (int i = 0; i < followerCommoners.Count; i++)
        {
            if (followerCommoners[i] == null) continue;
            
            VillagerAI ai = followerCommoners[i].GetComponent<VillagerAI>();
            if (ai != null)
            {
                // Update the follow position for this commoner
                ai.SetFollowPosition(followPositions[i]);
            }
        }
    }
    
    private void UpdateAoEVisualSystem()
    {
        if (aoeContainer == null) return;
        
        // Show/hide based on settings
        aoeContainer.gameObject.SetActive(showInfluenceRange);
        
        if (!showInfluenceRange) return;
        
        // Update AoE Circle scale
        if (aoeCircle != null)
        {
            float circleScale = currentInfluenceRadius + circleScaleOffset; // Diameter
            aoeCircle.localScale = Vector3.one * circleScale;
            
            if (debugInfluence)
            {
                Debug.Log($"Updated AoE Circle scale to {circleScale} (radius: {currentInfluenceRadius + circleScaleOffset})");
            }
        }
        
        // Update particle system radius
        if (aoeParticles != null)
        {
            var shape = aoeParticles.shape; // Get a fresh ShapeModule
            shape.radius = currentInfluenceRadius;
            
            if (debugInfluence)
            {
                Debug.Log($"Updated AoE Particles radius to {currentInfluenceRadius}");
            }
        }
    }
    
    // Called when captain's tier changes
    public void OnCaptainTierChanged()
    {
        UpdateInfluenceStats();
        
        // Reapply influence to all commoners with new values
        foreach (var commoner in influencedCommoners.ToArray())
        {
            RemoveInfluenceFromCommoner(commoner);
            ApplyInfluenceToCommoner(commoner);
        }
    }
    
    // Called when captain becomes rebel
    public void OnCaptainBecameRebel()
    {
        // Remove all influence and followers
        foreach (var commoner in influencedCommoners.ToArray())
        {
            RemoveInfluenceFromCommoner(commoner);
        }
        
        // Hide visual effects
        if (aoeContainer != null)
        {
            aoeContainer.gameObject.SetActive(false);
        }
    }
    
    // Public getters for other systems
    public float GetCurrentInfluenceRadius() => currentInfluenceRadius;
    public int GetInfluencedCommonersCount() => influencedCommoners.Count;
    public int GetFollowerCount() => followerCommoners.Count;
    public int GetMaxFollowers() => currentMaxFollowers;
    public List<CommonerCombat> GetInfluencedCommoners() => new List<CommonerCombat>(influencedCommoners);
    public List<CommonerCombat> GetFollowers() => new List<CommonerCombat>(followerCommoners);
    public Color GetAoEColor() => aoeColor;
    public bool IsAoEVisible() => showInfluenceRange;
    public float GetCircleScaleOffset() => circleScaleOffset;
    
    // Public methods for AoE visual control
    public void SetAoEColor(Color newColor)
    {
        aoeColor = newColor;
        
        // Update circle color
        if (aoeCircleRenderer != null)
        {
            aoeCircleRenderer.color = newColor;
        }
        
        // Update particle color
        if (aoeParticles != null)
        {
            var main = aoeParticles.main;
            main.startColor = new ParticleSystem.MinMaxGradient(newColor);
        }
        
        if (debugInfluence)
        {
            Debug.Log($"Captain {gameObject.name} AoE color set to {newColor}");
        }
    }
    
    public void SetAoEVisibility(bool visible)
    {
        showInfluenceRange = visible;
        
        if (aoeContainer != null)
        {
            aoeContainer.gameObject.SetActive(visible);
        }
    }
    
    public void SetCircleScaleOffset(float offset)
    {
        circleScaleOffset = offset;
        UpdateAoEVisualSystem(); // Immediately update visuals
    }
    
    // Configuration methods
    public void SetInfluenceRadius(float baseRadius, float radiusPerTier)
    {
        baseInfluenceRadius = baseRadius;
        influenceRadiusPerTier = radiusPerTier;
        UpdateInfluenceStats();
    }
    
    public void SetFollowerSettings(int baseCount, int countPerTier, float distance, float spacing)
    {
        baseFollowerCount = baseCount;
        followersPerTier = countPerTier;
        followDistance = distance;
        followSpacing = spacing;
        UpdateInfluenceStats();
    }
    
    private void OnDrawGizmosSelected()
    {
        // Draw influence radius
        Gizmos.color = aoeColor;
        if (Application.isPlaying)
        {
            Gizmos.DrawWireSphere(transform.position, currentInfluenceRadius);
        }
        else
        {
            float estimatedRadius = baseInfluenceRadius + (2 * influenceRadiusPerTier); // Estimate at tier 2
            Gizmos.DrawWireSphere(transform.position, estimatedRadius);
        }
        
        // Draw lines to influenced commoners
        if (Application.isPlaying && influencedCommoners.Count > 0)
        {
            Gizmos.color = Color.blue;
            foreach (var commoner in influencedCommoners)
            {
                if (commoner != null)
                {
                    Gizmos.DrawLine(transform.position, commoner.transform.position);
                }
            }
        }
        
        // Draw lines to followers (thicker/different color)
        if (Application.isPlaying && followerCommoners.Count > 0)
        {
            Gizmos.color = Color.green;
            foreach (var follower in followerCommoners)
            {
                if (follower != null)
                {
                    // Draw a thicker line by drawing multiple lines slightly offset
                    Vector3 toFollower = follower.transform.position - transform.position;
                    Vector3 perpendicular = Vector3.Cross(toFollower, Vector3.forward).normalized * 0.1f;
                    
                    Gizmos.DrawLine(transform.position + perpendicular, follower.transform.position + perpendicular);
                    Gizmos.DrawLine(transform.position - perpendicular, follower.transform.position - perpendicular);
                    Gizmos.DrawLine(transform.position, follower.transform.position);
                }
            }
        }
        
        // Draw follow positions
        if (Application.isPlaying && followPositions.Count > 0)
        {
            Gizmos.color = Color.yellow;
            foreach (var pos in followPositions)
            {
                Gizmos.DrawWireSphere(pos, 0.3f);
            }
        }
        
        // Draw follow distance circle
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, followDistance);
    }
    
    private void OnDestroy()
    {
        // Clean up all influences and followers
        foreach (var commoner in influencedCommoners.ToArray())
        {
            RemoveInfluenceFromCommoner(commoner);
        }
    }
}