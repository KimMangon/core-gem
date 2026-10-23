using UnityEngine;

/// <summary>Keep the full reference layout visible at any window size or aspect ratio.</summary>
public static class GameUiScaling
{
    public static void Configure(Canvas canvas)
    {
        if (canvas == null || canvas.renderMode == RenderMode.WorldSpace) return;
        var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
        if (scaler == null) scaler = canvas.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
        scaler.referencePixelsPerUnit = 100f;
    }
}
