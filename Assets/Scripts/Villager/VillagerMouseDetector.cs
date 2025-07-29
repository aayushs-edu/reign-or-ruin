using UnityEngine;

public class VillagerMouseDetector : MonoBehaviour
{
    public System.Action OnMouseEnterVillager;
    public System.Action OnMouseExitVillager;
    
    private void OnMouseEnter()
    {
        OnMouseEnterVillager?.Invoke();
    }
    
    private void OnMouseExit()
    {
        OnMouseExitVillager?.Invoke();
    }
}