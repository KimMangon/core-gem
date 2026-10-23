using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

public static class KoreanLocalizationSetup
{
    [Serializable] private sealed class Entry { public string key, en, ko, context; }
    [Serializable] private sealed class Catalog { public Entry[] entries; }

    [MenuItem("Tools/Localization/Set Up Korean and English")]
    public static void Apply()
    {
        Directory.CreateDirectory("Assets/Localization");
        Directory.CreateDirectory("Assets/Resources/Localization");
        AssetDatabase.Refresh();
        var settings = LocalizationEditorSettings.ActiveLocalizationSettings;
        if (settings == null)
        {
            settings = AssetDatabase.FindAssets("t:LocalizationSettings", new[] { "Assets" })
                .Select(g => AssetDatabase.LoadAssetAtPath<LocalizationSettings>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault();
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<LocalizationSettings>();
                AssetDatabase.CreateAsset(settings, "Assets/Localization/LocalizationSettings.asset");
            }
            LocalizationEditorSettings.ActiveLocalizationSettings = settings;
        }
        foreach (string code in new[] { "en", "ko" })
        {
            if (LocalizationEditorSettings.GetLocales().Any(l => l.Identifier.Code == code)) continue;
            var locale = Locale.CreateLocale(code);
            AssetDatabase.CreateAsset(locale, "Assets/Localization/Locale_" + code + ".asset");
            LocalizationEditorSettings.AddLocale(locale);
        }
        settings.GetStartupLocaleSelectors().Clear();
        settings.GetStartupLocaleSelectors().Add(new SavedGameLocaleSelector());
        settings.GetStringDatabase().DefaultTable = GameLocalization.Table;
        var catalog = JsonUtility.FromJson<Catalog>(File.ReadAllText("Assets/Localization/Translations.json"));
        var strings = LocalizationEditorSettings.GetStringTableCollection(GameLocalization.Table) ??
            LocalizationEditorSettings.CreateStringTableCollection(GameLocalization.Table, "Assets/Localization");
        int count = 0;
        foreach (var table in strings.StringTables)
        {
            foreach (var entry in catalog.entries)
            {
                string value = table.LocaleIdentifier.Code == "ko" ? entry.ko : entry.en;
                if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("Empty translation: " + entry.key);
                table.AddEntry(entry.key, value);
                count++;
            }
            EditorUtility.SetDirty(table);
        }
        strings.SetPreloadTableFlag(true);
        EditorUtility.SetDirty(strings.SharedData);
        EditorUtility.SetDirty(strings);
        LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null, strings);

        Font font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fort/netmarbleB.ttf");
        if (font == null) throw new InvalidOperationException("Korean UI font missing.");
        var missing = catalog.entries.SelectMany(e => e.ko).Where(c => !char.IsWhiteSpace(c) && !char.IsControl(c))
            .Distinct().Where(c => !font.HasCharacter(c)).ToArray();
        if (missing.Length != 0) throw new InvalidOperationException("Font missing characters: " + new string(missing));
        var assets = LocalizationEditorSettings.GetAssetTableCollection("GameAssets") ??
            LocalizationEditorSettings.CreateAssetTableCollection("GameAssets", "Assets/Localization");
        foreach (var table in assets.AssetTables)
        {
            assets.AddAssetToTable(table, "UI_FONT", font);
            EditorUtility.SetDirty(table);
        }
        EditorUtility.SetDirty(assets.SharedData);
        EditorUtility.SetDirty(assets);
        LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null, assets);
        string assetPath = "Assets/Resources/Localization/GameLocalizationAssets.asset";
        var config = AssetDatabase.LoadAssetAtPath<GameLocalizationAssets>(assetPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<GameLocalizationAssets>();
            AssetDatabase.CreateAsset(config, assetPath);
        }
        config.uiFont = font;
        config.keys = catalog.entries.Select(e => e.key).ToArray();
        EditorUtility.SetDirty(config);
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();

        foreach (var table in strings.StringTables)
            foreach (var key in strings.SharedData.Entries)
                if (string.IsNullOrEmpty(table.GetEntry(key.Id)?.Value))
                    throw new InvalidOperationException("Missing translation: " + key.Key);
        UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.BuildPlayerContent(out var result);
        if (!string.IsNullOrEmpty(result.Error)) throw new InvalidOperationException(result.Error);
        Debug.Log("Localization complete: " + count + " values, 2 locales, no missing values or Korean glyphs.");
    }
}
