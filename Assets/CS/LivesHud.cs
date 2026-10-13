using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Text))]
public class LivesHud : MonoBehaviour
{
    private Text livesText;
    private GameManager gameManager;
    private int displayedLives = int.MinValue;

    private void Awake()
    {
        livesText = GetComponent<Text>();
        SetDisplayedLives(-1);
    }

    private void Update()
    {
        if (gameManager == null)
            gameManager = FindAnyObjectByType<GameManager>();

        if (gameManager == null)
        {
            SetDisplayedLives(-1);
            return;
        }

        int lives = gameManager.LocalLives >= 0
            ? gameManager.LocalLives
            : gameManager.GetStartingLives();
        SetDisplayedLives(lives);
    }

    private void SetDisplayedLives(int lives)
    {
        if (displayedLives == lives)
            return;

        displayedLives = lives;
        livesText.enabled = lives >= 0;
        livesText.text = lives < 0 ? "Lives: --" : $"Lives: {lives}";
    }
}
