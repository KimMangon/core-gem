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
        GameLocalization.Changed += InvalidateLanguage;
        livesText = GetComponent<Text>();
        GameLocalization.Style(livesText);
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

    private void InvalidateLanguage() => displayedLives = int.MinValue;
    private void OnDestroy() => GameLocalization.Changed -= InvalidateLanguage;

    private void SetDisplayedLives(int lives)
    {
        if (displayedLives == lives)
            return;

        displayedLives = lives;
        livesText.enabled = lives >= 0;
        livesText.text = GameLocalization.T("Lives: {0}", lives < 0 ? "--" : lives.ToString());
    }
}
