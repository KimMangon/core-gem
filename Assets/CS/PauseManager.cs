using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Unity.Netcode;

public class PauseManager : MonoBehaviour
{
    public static bool IsPaused = false; // 다른 스크립트(PlayerLook, PlayerArm)가 참조하는 플래그
    public GameObject pausePanel;

    void Update()
    {
        if (StageManager.Instance != null && StageManager.Instance.RoomTimerFinished)
            return;
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
            return;

        if (GameSettingsMenu.Instance != null && GameSettingsMenu.Instance.IsOpen)
        {
            if (GameSettingsMenu.Instance.IsWaitingForBinding)
                GameSettingsMenu.Instance.CancelKeyCapture();
            else
                GameSettingsMenu.Instance.Close();
            return;
        }

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            return;

        if (StageManager.Instance != null && !StageManager.Instance.IsGameStarted)
        {
            GameSettingsMenu.Instance?.OpenFromLobby();
            return;
        }

        if (IsPaused) ClosePause();
        else OpenPause();
    }

    public void OpenPause()
    {
        IsPaused = true;
        pausePanel.SetActive(true);
    }

    public void ClosePause()
    {
        IsPaused = false;
        pausePanel.SetActive(false);
    }

    public void RestartGame()
    {
        IsPaused = false;
        
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
