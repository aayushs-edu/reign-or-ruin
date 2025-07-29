using UnityEngine;
using UnityEngine.EventSystems;

public class VillagerPanelMouseDetector : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public System.Action OnMouseEnterPanel;
    public System.Action OnMouseExitPanel;
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        OnMouseEnterPanel?.Invoke();
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        OnMouseExitPanel?.Invoke();
    }
}