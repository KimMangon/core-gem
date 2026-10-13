using System;
using System.Globalization;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class RoomTimerHud : MonoBehaviour
{
    [SerializeField] private UnityEngine.UI.Text timerText;

    private CanvasGroup canvasGroup;
    private StageManager stageManager;
    private long displayedSecond = -1;
    private bool displayedFinished;
    private bool isVisible = true;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (timerText == null)
            timerText = GetComponentInChildren<UnityEngine.UI.Text>(true);
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        SetVisible(false);
    }

    private void Update()
    {
        NetworkManager network = NetworkManager.Singleton;
        if (network == null || !network.IsListening)
        {
            stageManager = null;
            SetVisible(false);
            return;
        }

        if (stageManager == null || !stageManager.IsSpawned)
            stageManager = FindFirstObjectByType<StageManager>();
        if (stageManager == null || !stageManager.RoomTimerStarted || timerText == null)
        {
            SetVisible(false);
            return;
        }

        SetVisible(true);
        long seconds = (long)Math.Floor(stageManager.RoomElapsedSeconds);
        bool finished = stageManager.RoomTimerFinished;
        if (seconds == displayedSecond && finished == displayedFinished)
            return;

        displayedSecond = seconds;
        displayedFinished = finished;
        long hours = seconds / 3600;
        long minutes = seconds / 60 % 60;
        long remainingSeconds = seconds % 60;
        timerText.text = string.Format(CultureInfo.InvariantCulture,
            "{0} {1:00}:{2:00}:{3:00}",
            finished ? "FINISH TIME" : "PLAY TIME", hours, minutes, remainingSeconds);
    }

    private void SetVisible(bool visible)
    {
        if (isVisible == visible)
            return;

        isVisible = visible;
        canvasGroup.alpha = visible ? 1f : 0f;
        if (!visible)
            displayedSecond = -1;
    }
}
