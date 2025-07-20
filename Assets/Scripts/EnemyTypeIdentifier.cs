using UnityEngine;

public class EnemyTypeIdentifier : MonoBehaviour
{
    [Header("Enemy Type")]
    [SerializeField] private string typeName = "Ghost";
    
    public string GetTypeName() => typeName;
    
    public void SetTypeName(string newTypeName)
    {
        typeName = newTypeName;
    }
}