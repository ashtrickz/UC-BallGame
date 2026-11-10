using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Slime.App.Loading;
using Slime.Foundation;
using Slime.Gameplay.View;
using Slime.Composition;
using Slime.Gameplay.Presentation;
using Slime.Infrastructure.Settings;
using Slime.Infrastructure.Localization;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Slime.Editor
{
    /// <summary>Authoring only: stores keys and font bindings on existing UI, with no runtime string matching.</summary>
    public static class LocalizationUiInstaller
    {
        private const string PopupPath = "Assets/_Game/Prefabs/UI/Popups/SettingsPopup.prefab";
        private const string FontFolder = "Assets/_Game/Art/Fonts/Localization";
        private static Dictionary<string, string> _english;

        [MenuItem("Tools/Ball Game/Localization/Install UI Bindings")]
        public static void Install()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play mode before installing UI bindings.");
            var document = JsonUtility.FromJson<Document>(File.ReadAllText("Assets/_Game/Resources/Localization/fallback.json"));
            _english = document.languages.Single(language => language.code == "en").entries.ToDictionary(entry => entry.key, entry => entry.value);
            InstallFonts(document);
            InstallSettings();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (var name in new[] { "Loading", "Menu", "Game" })
                {
                    var path = "Assets/_Game/Scenes/" + name + ".unity";
                    var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
                    if (scene.IsValid() && scene.isDirty)
                    {
                        GameLog.Warning("Localization: skipped unsaved scene " + path + ". Save it and run Install UI Bindings again.");
                        continue;
                    }
                    if (!scene.IsValid() || !scene.isLoaded)
                        scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    foreach (var root in scene.GetRootGameObjects())
                        BindHierarchy(root);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
            GameLog.Info("Localization: installed serialized UI keys, language buttons and TMP fallback fonts.");
        }

        private static void InstallSettings()
        {
            var root = PrefabUtility.LoadPrefabContents(PopupPath);
            try
            {
                var view = root.GetComponent<SettingsPopupView>();
                var serialized = new SerializedObject(view);
                var panel = root.transform.Find("RootTransform") as RectTransform;
                if (panel == null)
                    throw new InvalidOperationException("Settings popup panel is missing.");
                var choices = serialized.FindProperty("languageChoices");
                if (choices.arraySize == 0)
                {
                    panel.sizeDelta += new Vector2(0, 260);
                    foreach (RectTransform child in panel)
                    {
                        var bottom = child.name == "Reset" || child.name == "Version";
                        child.anchoredPosition += new Vector2(0, bottom ? -115 : 130);
                    }
                    var title = panel.Find("Title") as RectTransform;
                    if (title != null)
                    {
                        title.anchoredPosition += new Vector2(-70, 0);
                        title.sizeDelta = new Vector2(420, 80);
                    }
                    var labelTemplate = panel.Find("MasterLabel").GetComponent<TMP_Text>();
                    var languageLabel = UnityEngine.Object.Instantiate(labelTemplate, panel);
                    languageLabel.name = "LanguageLabel";
                    languageLabel.rectTransform.anchoredPosition = new Vector2(0, -180);
                    languageLabel.rectTransform.sizeDelta = new Vector2(600, 50);
                    languageLabel.alignment = TextAlignmentOptions.Center;
                    BindText(languageLabel, "settings.language");

                    var template = serialized.FindProperty("fpsChoices").GetArrayElementAtIndex(0)
                        .FindPropertyRelative("button").objectReferenceValue as UnityEngine.UI.Button;
                    if (template == null)
                        throw new InvalidOperationException("Settings popup needs an FPS button template.");
                    var codes = new[] { "en", "uk", "es", "zh-CN" };
                    choices.arraySize = codes.Length;
                    for (var index = 0; index < codes.Length; index++)
                    {
                        var code = codes[index];
                        var button = UnityEngine.Object.Instantiate(template, panel);
                        button.name = "Language_" + code;
                        button.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
                        var rect = (RectTransform)button.transform;
                        rect.anchoredPosition = new Vector2(index % 2 == 0 ? -150 : 150, index < 2 ? -245 : -320);
                        rect.sizeDelta = new Vector2(270, 60);
                        var label = button.GetComponentInChildren<TMP_Text>(true);
                        BindText(label, "language." + code);
                        var choice = choices.GetArrayElementAtIndex(index);
                        choice.FindPropertyRelative("code").stringValue = code;
                        choice.FindPropertyRelative("button").objectReferenceValue = button;
                    }
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                BindHierarchy(root);
                var fpsChoices = serialized.FindProperty("fpsChoices");
                for (var index = 0; index < fpsChoices.arraySize; index++)
                {
                    var choice = fpsChoices.GetArrayElementAtIndex(index);
                    var button = choice.FindPropertyRelative("button").objectReferenceValue as UnityEngine.UI.Button;
                    if (button != null)
                        BindText(button.GetComponentInChildren<TMP_Text>(true), "settings.fps", choice.FindPropertyRelative("fps").intValue.ToString());
                }
                PrefabUtility.SaveAsPrefabAsset(root, PopupPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void BindHierarchy(GameObject root)
        {
            var dynamic = new HashSet<TMP_Text>();
            foreach (var view in root.GetComponentsInChildren<GameView>(true))
                dynamic.UnionWith(view.DynamicTextLabels);
            foreach (var view in root.GetComponentsInChildren<ResultView>(true))
                dynamic.UnionWith(view.DynamicTextLabels);
            foreach (var loading in root.GetComponentsInChildren<LoadingScreenView>(true))
                dynamic.UnionWith(loading.DynamicTextLabels);
            foreach (var tutorial in root.GetComponentsInChildren<TutorialOverlay>(true))
                dynamic.Add(tutorial.MessageLabel);
            var byText = _english.Where(pair => !pair.Value.Contains("{0}") && !pair.Key.StartsWith("language."))
                .GroupBy(pair => pair.Value, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().Key, StringComparer.OrdinalIgnoreCase);
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (!text.enableAutoSizing)
                {
                    text.fontSizeMax = text.fontSize;
                    text.fontSizeMin = Mathf.Max(14, text.fontSize * 0.5f);
                    text.enableAutoSizing = true;
                }
                if (dynamic.Contains(text) || text.GetComponent<LocalizedText>() != null)
                    continue;
                if (!string.IsNullOrEmpty(text.text) && byText.TryGetValue(text.text.Trim(), out var key))
                    BindText(text, key);
            }
        }

        private static void BindText(TMP_Text text, string key, params string[] arguments)
        {
            if (text == null)
                return;
            var component = text.GetComponent<LocalizedText>() ?? text.gameObject.AddComponent<LocalizedText>();
            var serialized = new SerializedObject(component);
            serialized.FindProperty("key").stringValue = key;
            var values = serialized.FindProperty("arguments");
            values.arraySize = arguments.Length;
            for (var index = 0; index < arguments.Length; index++)
                values.GetArrayElementAtIndex(index).stringValue = arguments[index];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            text.text = arguments.Length == 0 ? _english[key] : string.Format(_english[key], arguments);
            EditorUtility.SetDirty(text);
            EditorUtility.SetDirty(component);
        }

        private static void InstallFonts(Document document)
        {
            if (TMP_Settings.instance == null)
                throw new InvalidOperationException("TMP Essential Resources are required.");
            var multilingualCharacters = string.Concat(document.languages.Where(language => language.code != "zh-CN")
                .SelectMany(language => language.entries).Where(entry => entry.key != "language.zh-CN").Select(entry => entry.value));
            var multilingual = CreateFont("NotoSans-Regular.ttf", "NotoSans Multilingual SDF", multilingualCharacters);
            var chinese = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LocalizationChineseFontBuilder.FontPath);
            if (chinese == null)
                chinese = CreateFont("NotoSansCJKsc-Regular.otf", "NotoSans Simplified Chinese SDF", string.Empty);
            var path = "Assets/_Game/Resources/Localization/Fonts.asset";
            var fonts = AssetDatabase.LoadAssetAtPath<LocalizationFonts>(path);
            if (fonts == null)
            {
                fonts = ScriptableObject.CreateInstance<LocalizationFonts>();
                AssetDatabase.CreateAsset(fonts, path);
            }
            var serialized = new SerializedObject(fonts);
            serialized.FindProperty("multilingual").objectReferenceValue = multilingual;
            serialized.FindProperty("simplifiedChinese").objectReferenceValue = chinese;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(fonts);
            AssetDatabase.SaveAssetIfDirty(fonts);
            fonts.ConfigureFallbacks();
            foreach (var font in Resources.LoadAll<TMP_FontAsset>("Fonts & Materials").Concat(new[] { multilingual }))
            {
                EditorUtility.SetDirty(font);
                AssetDatabase.SaveAssetIfDirty(font);
            }
            EditorUtility.SetDirty(TMP_Settings.instance);
            AssetDatabase.SaveAssetIfDirty(TMP_Settings.instance);
            LocalizationChineseFontBuilder.Rebuild();
        }

        private static TMP_FontAsset CreateFont(string sourceFile, string name, string characters)
        {
            var path = FontFolder + "/" + name + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (asset == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<Font>(FontFolder + "/" + sourceFile);
                if (source == null)
                    throw new InvalidOperationException("Font source is missing: " + sourceFile);
                asset = TMP_FontAsset.CreateFontAsset(source, 64, 8, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
                asset.name = name;
                asset.isMultiAtlasTexturesEnabled = true;
                AssetDatabase.CreateAsset(asset, path);
                foreach (var atlas in asset.atlasTextures)
                    AssetDatabase.AddObjectToAsset(atlas, asset);
                AssetDatabase.AddObjectToAsset(asset.material, asset);
                asset.material.mainTexture = asset.atlasTexture;
            }
            var glyphs = new string(characters.Where(character => !char.IsControl(character)).Concat("0123456789% ").Distinct().ToArray());
            if (!asset.HasCharacters(glyphs, out uint[] missingGlyphs, false, false))
            {
                FontEngine.InitializeFontEngine();
                var missingText = new string(missingGlyphs.Select(value => (char)value).ToArray());
                if (!asset.TryAddCharacters(missingText, out string missing))
                    throw new InvalidOperationException(name + " is missing glyphs: " + missing);
            }
            EditorUtility.SetDirty(asset);
            EditorUtility.SetDirty(asset.material);
            foreach (var atlas in asset.atlasTextures)
                EditorUtility.SetDirty(atlas);
            AssetDatabase.SaveAssetIfDirty(asset);
            if (!AssetDatabase.LoadAllAssetsAtPath(path).Any(value => value is Material))
                throw new InvalidOperationException("Font material was not saved: " + path);
            return asset;
        }

        [Serializable]
        private sealed class Document
        {
            public Language[] languages;
        }
        [Serializable]
        private sealed class Language
        {
            public string code; public Entry[] entries;
        }
        [Serializable]
        private sealed class Entry
        {
            public string key; public string value;
        }
    }
}
