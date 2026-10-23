using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.SceneManagement;

[Serializable]
public sealed class SavedGameLocaleSelector : IStartupLocaleSelector
{
    public Locale GetStartupLocale(ILocalesProvider availableLocales)
    {
        string code = PlayerPrefs.GetString("GameLanguage", "en");
        return availableLocales.GetLocale(code) ?? availableLocales.GetLocale("en");
    }
}

public static class GameLocalization
{
    public const string Table = "GameUI";
    public static event Action Changed;
    public static bool Ready { get; private set; }
    public static bool Korean => LocalizationSettings.SelectedLocale != null && LocalizationSettings.SelectedLocale.Identifier.Code == "ko";
    public static Font UiFont => Assets != null ? Assets.uiFont : null;
    private static GameLocalizationAssets assets;
    private static GameLocalizationAssets Assets => assets != null ? assets : assets = Resources.Load<GameLocalizationAssets>("Localization/GameLocalizationAssets");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Ready = false;
        Changed = null;
        assets = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Start()
    {
        var go = new GameObject("GameLocalization");
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.AddComponent<GameLocalizationLoader>();
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    internal static IEnumerator Initialize()
    {
        yield return LocalizationSettings.InitializationOperation;
        var selected = LocalizationSettings.SelectedLocale;
        if (selected == null) yield break;
        yield return LocalizationSettings.StringDatabase.GetTableAsync(Table, selected);
        Ready = true;
        Changed?.Invoke();
    }

    public static void SetLanguage(bool korean)
    {
        Locale locale = LocalizationSettings.AvailableLocales.GetLocale(korean ? "ko" : "en");
        if (locale == null) return;
        PlayerPrefs.SetString("GameLanguage", locale.Identifier.Code);
        PlayerPrefs.Save();
        LocalizationSettings.SelectedLocale = locale;
    }

    private static void OnLocaleChanged(Locale locale) => Changed?.Invoke();
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var label in root.GetComponentsInChildren<UnityEngine.UI.Text>(true)) Style(label);
    }

    public static string T(string key, params object[] arguments)
    {
        if (!Ready) return arguments.Length == 0 ? key : string.Format(CultureInfo.InvariantCulture, key, arguments);
        return LocalizationSettings.StringDatabase.GetLocalizedString(Table, key, arguments);
    }

    public static void Style(UnityEngine.UI.Text label)
    {
        if (label == null) return;
        if (UiFont != null) label.font = UiFont;
        // Fit translated labels inside the existing bounds without enlarging them.
        if (label.GetComponentInParent<UnityEngine.UI.Button>() != null)
        {
            label.resizeTextForBestFit = true;
            label.resizeTextMaxSize = label.fontSize;
            label.resizeTextMinSize = Mathf.Min(label.fontSize, 18);
        }
    }

    public static void Bind(UnityEngine.UI.Text label, string key, params object[] arguments)
    {
        Style(label);
        if (label == null || string.IsNullOrEmpty(key)) return;
        var localized = label.GetComponent<LocalizeStringEvent>();
        if (localized == null)
            localized = label.gameObject.AddComponent<LocalizeStringEvent>();
        // Template buttons can clone the component without its runtime listeners.
        var receiver = label.GetComponent<GameLocalizedText>();
        if (receiver == null) receiver = label.gameObject.AddComponent<GameLocalizedText>();
        localized.OnUpdateString.RemoveAllListeners();
        localized.OnUpdateString.AddListener(receiver.Apply);
        localized.StringReference = new LocalizedString(Table, key) { Arguments = arguments };
        label.text = T(key, arguments);
        if (Ready) localized.RefreshString();
    }

    public static void BindKnown(UnityEngine.UI.Text label, string original)
    {
        Style(label);
        // Credits and their entry buttons retain the original attribution text in every locale.
        for (Transform parent = label != null ? label.transform : null; parent != null; parent = parent.parent)
        {
            if (parent.name != "CreditsOverlay" && parent.name != "MainMenuCreditsButton" &&
                parent.name != "ClearCreditsButton") continue;
            ClearText(label);
            label.text = original;
            return;
        }
        if (Assets == null || !Array.Exists(Assets.keys, key => key == original)) return;
        Bind(label, original);
    }

    public static void ClearText(UnityEngine.UI.Text label)
    {
        if (label == null) return;
        var localized = label.GetComponent<LocalizeStringEvent>();
        if (localized != null) localized.StringReference = new LocalizedString();
        label.text = string.Empty;
    }

    public static void BindAuthoredUi(Transform root)
    {
        if (root == null) return;
        foreach (var label in root.GetComponentsInChildren<UnityEngine.UI.Text>(true))
        {
            var input = label.GetComponentInParent<UnityEngine.UI.InputField>();
            if (input != null && input.textComponent == label) { Style(label); continue; }
            BindKnown(label, label.text);
        }
    }
}

public sealed class GameLocalizationLoader : MonoBehaviour
{
    private IEnumerator Start() => GameLocalization.Initialize();
}

public sealed class GameLocalizedText : MonoBehaviour
{
    public void Apply(string value)
    {
        var label = GetComponent<UnityEngine.UI.Text>();
        if (label == null) return;
        label.text = value;
        GameLocalization.Style(label);
        var localized = GetComponent<LocalizeStringEvent>();
        if (localized != null && localized.StringReference.TableEntryReference.Key == "MAIN MENU")
            MenuManager.FitMainMenuLabel(label);
    }
}
