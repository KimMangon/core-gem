using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GameplayMusicPlayer : MonoBehaviour
{
    private const string MusicResourcePath = "GameplayMusic";
    private const string MenuMusicResourcePath = "MenuMusic/AnitrasDance";
    private const float MusicVolume = 0.4f;

    private AudioSource musicSource;
    private AudioClip[] playlist;
    private AudioClip menuClip;
    private MenuManager menuManager;
    private int nextClipIndex;
    private bool gameplayStarted;

    private void Awake()
    {
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = false;
        musicSource.spatialBlend = 0f;
        musicSource.volume = MusicVolume;

        playlist = Resources.LoadAll<AudioClip>(MusicResourcePath);
        menuClip = Resources.Load<AudioClip>(MenuMusicResourcePath);
        menuManager = FindFirstObjectByType<MenuManager>();
        Array.Sort(playlist, (left, right) => string.CompareOrdinal(left.name, right.name));
        if (playlist.Length == 0)
            Debug.LogWarning("Gameplay music clips were not found in Resources/GameplayMusic.");
    }

    private void Update()
    {
        musicSource.volume = MusicVolume * GameSettingsPreferences.BgmVolume;
        bool shouldPlay = StageManager.Instance != null && StageManager.Instance.IsGameStarted &&
            !StageManager.Instance.RoomTimerFinished;
        if (!shouldPlay)
        {
            if (gameplayStarted)
            {
                musicSource.Stop();
                musicSource.clip = null;
                nextClipIndex = 0;
                gameplayStarted = false;
            }
            bool inMainMenu = StageManager.Instance != null && !StageManager.Instance.RoomTimerFinished &&
                menuManager != null && menuManager.hostSettingsPanel != null &&
                !menuManager.hostSettingsPanel.activeSelf;
            if (inMainMenu && menuClip != null)
            {
                if (musicSource.clip != menuClip)
                {
                    musicSource.Stop();
                    musicSource.clip = menuClip;
                    musicSource.loop = true;
                    musicSource.Play();
                }
            }
            else if (musicSource.clip == menuClip)
            {
                musicSource.Stop();
                musicSource.clip = null;
            }
            return;
        }

        if (musicSource.clip == menuClip)
        {
            musicSource.Stop();
            musicSource.clip = null;
            musicSource.loop = false;
        }
        gameplayStarted = true;
        if (playlist.Length == 0 || musicSource.isPlaying)
            return;

        musicSource.clip = playlist[nextClipIndex];
        nextClipIndex = (nextClipIndex + 1) % playlist.Length;
        musicSource.Play();
    }
}
