using UnityEngine;
using UnityEngine.EventSystems;

public sealed class ColorPaletteDrag : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    public System.Action<Vector2> Changed;
    public void OnPointerDown(PointerEventData data) => Pick(data);
    public void OnDrag(PointerEventData data) => Pick(data);
    private void Pick(PointerEventData data)
    {
        if(data.button!=PointerEventData.InputButton.Left)return;
        var rect=(RectTransform)transform;
        if(RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,data.position,data.pressEventCamera,out var point))
            Changed?.Invoke(new Vector2(Mathf.Clamp01((point.x-rect.rect.xMin)/rect.rect.width),Mathf.Clamp01((point.y-rect.rect.yMin)/rect.rect.height)));
    }
}
