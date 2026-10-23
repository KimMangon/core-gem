using UnityEngine;
using UnityEngine.EventSystems;

public sealed class CustomizationPreviewDrag : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    public Transform Target { get; set; }
    public void OnBeginDrag(PointerEventData eventData) { }
    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || Target == null) return;
        var canvas = GetComponentInParent<Canvas>();
        float scale = canvas != null ? Mathf.Max(.01f, canvas.scaleFactor) : 1f;
        Target.Rotate(0f, -eventData.delta.x / scale * .4f, 0f, Space.Self);
    }
}
