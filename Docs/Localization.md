# Localization

`Slime.Localization` contains `ILocalizationService`, its implementation and table parsing. The Unity component `LocalizedText` and source loading live in `Slime.Infrastructure.Localization`. The service is registered as a singleton in `ProjectScope` and loads as the first bootstrap step. JSON is available as soon as the service is created; Google Sheets loads before the menu opens. An empty URL turns the network request off. A network error or invalid CSV never blocks the game from starting.

The language comes from `ISettingsService.CurrentLanguage` and is saved in PlayerPrefs by the existing settings. Supported languages are `en`, `uk` (Ukrainian), `es` and `zh-CN` (Simplified Chinese). Change the language with `settings.SetLanguage(...)`, passing an option from `settings.Languages`. The service raises `Changed` when a table loads and when the language changes; volume changes do not raise it.

## Getting text

Plain classes get the service through the constructor:

```csharp
using Slime.Localization;

public sealed class ExampleController
{
    private readonly ILocalizationService _localization;

    public ExampleController(ILocalizationService localization)
        => _localization = localization;

    public string GetTitle() => _localization.Get("settings.title");
}
```

A MonoBehaviour uses `[Inject]`:

```csharp
[VContainer.Inject]
public void Construct(Slime.Localization.ILocalizationService localization)
{
    label.text = localization.Get("menu.levels");
}
```

For static TMP labels, add `Slime.Infrastructure.Localization.LocalizedText`, fill in `key`, and include the object in `LifetimeScope > Auto Inject Game Objects`. In the existing scopes, `RegisterInstance(view)` alone does not inject scene components. For objects created from code, use `resolver.Instantiate(prefab)` or `resolver.InjectGameObject(instance)`. `LocalizedText` listens for changes while it is enabled and refreshes when it is enabled again. For text whose key changes, use `SetKey(key)`.

The static labels of the `Loading`, `Menu` and `Game` scenes and the settings prefab are bound through a serialized `LocalizedText`. The scope injects these components, including inactive labels. `LevelSelectView` also injects the level cells it creates. Nothing maps text to keys at runtime.

Dynamic texts in the HUD, results, tutorial and loading screen get their values by key from their controllers and refresh when the language changes. Parameters use `localization.Format("hud.level", number)`. Player names, the version number, seeds and numeric values stay as data.

The settings have four language buttons. The choice is saved through `ISettingsService` in PlayerPrefs and applied immediately; resetting the settings returns to English. Language names come from the keys `language.en`, `language.uk`, `language.es` and `language.zh-CN`.

Fonts use the standard TMP fallback chain: the original font asset → Noto Sans → Noto Sans CJK SC. Fonts and their licences are in `Assets/_Game/Art/Fonts/Localization`, and the SDF references are in `Resources/Localization/Fonts.asset`. The chain is assigned once at startup, and the editor installer saves it into the font assets and TMP Settings. `TMP_Text.font` is never switched when the language changes. Language names in different scripts are covered by the same chain.

After rebuilding the scenes or the settings prefab, run `Tools > Ball Game > Localization > Install UI Bindings`. This editor operation takes the source strings from the English section of the JSON, assigns keys to the existing labels, adds missing language buttons and saves the font fallback chains. Dynamic fields do not get a static key. Scenes with unsaved changes are skipped with a message in the Console.

## Main source: Google Sheets

The game is connected to [Slime: Homecoming Loc](https://docs.google.com/spreadsheets/d/1-cbHj8pmW9SiYFXXYsr-uMO60QRn94AoTEiKzyJu11E/edit#gid=0), first sheet (`gid=0`). Columns `A:E` are `key,en,uk,es,zh-CN`. The table covers the menu, settings, loading, HUD, results, tutorial and the names of the ten levels. Keep the `{0}` and `{1}` parameters and the line breaks in translations.

`LocalizationConfig.asset` reads the [published CSV of this sheet](https://docs.google.com/spreadsheets/d/e/2PACX-1vQBeR_t9lLkhlILeXVM1YD7UsTzBde8l_jgrRNAnjzSe8H-zCC7mV_2pr4aKD5FGJipGIB4Pgs4-rXZ/pub?gid=0&single=true&output=csv). The shipped translations are in the JSON fallback and in `Docs/LocalizationTemplate.csv`. Four new `result.hint_*` keys were added to both local sources; the external Google Sheet was not changed in that edit. When the sheet lacks the new keys, the service uses the JSON. For a new label, assign `LocalizedText.key` or call the service from its controller.

In the relaid-out `SettingsPopup`, each volume channel uses its own key, FPS passes the argument 30, 60 or 120, and the language buttons use `language.en`, `language.uk`, `language.es` and `language.zh-CN`. `ResultView` has only two dynamic texts: the title and the explanation. `ResultPresenter` translates them through the mode policy, including on a language change. The result buttons use icons and actions and no longer need text fields or caption keys. The win explanation depends on which star criteria were missed. The static Chinese atlas was updated for the new strings.

To connect a different sheet:

1. Create a Google Sheet and import `Docs/LocalizationTemplate.csv` with UTF-8 encoding and a comma separator.
2. The first row is `key,en,uk,es,zh-CN`. The first column is a unique key, and the others are translations. The order of the language columns does not matter. An empty cell means there is no translation. Commas, quotes and line breaks inside cells are supported.
3. Give read access to anyone with the link, or publish the sheet as CSV. Access without sign-in is required, because the game reads translations without a Google account.
4. Open `Assets/_Game/Resources/Localization/LocalizationConfig.asset`. In `Google Sheets Csv Url`, enter the CSV address, for example `https://docs.google.com/spreadsheets/d/SPREADSHEET_ID/gviz/tq?tqx=out:csv&gid=0`, replacing the ID and `gid` with the values of your sheet. The CSV link you get when you publish the sheet also works.

See [accessing Google Sheets data](https://developers.google.com/chart/interactive/docs/spreadsheets) and [the CSV response format](https://developers.google.com/chart/interactive/docs/dev/implementing_data_source).

The service requests the sheet when the game starts. The timeout is set in the config and defaults to 5 seconds. Loading does not change the JSON and does not keep a network cache. To support future offline launches, update the shipped JSON together with the translations in the sheet.

## In-game fallback

`Assets/_Game/Resources/Localization/fallback.json` is included in the build through a reference from the config. Format:

```json
{
  "languages": [
    {
      "code": "en",
      "entries": [
        { "key": "menu.levels", "value": "LEVELS" }
      ]
    }
  ]
}
```

`Get(key)` looks up text in this order:

1. Google Sheets, selected language.
2. JSON, selected language.
3. Google Sheets, default language from the config (`en`).
4. JSON, default language.
5. The key itself, if there is no translation anywhere. An empty or null key returns an empty string.

Duplicate keys or language columns, broken quotes, HTML instead of CSV, and tables without translations are rejected as a whole. The last load error is available in `LocalizationService.LastLoadError`; cancellation through a CancellationToken propagates to the caller.

Checks: `Slime.Tests.EditMode.LocalizationTests` covers the fallback, source priority, Unicode and multiline CSV, damaged data, no network, cancellation, language change and TMP component injection. `LocalizationUiBindingTests` checks keys, buttons and glyphs in the saved scenes and prefab. `LocalizedGameUiTests` checks language switching in the menu, HUD, tutorial and results through the real game.

Manual check: open the settings, select each of the four languages, close the window and check the menu. Then check the HUD, the level 1 tutorial and the defeat screen; changing the language must not restart the attempt. Restart the game to check that the choice is saved. Without internet, the translations come from the JSON.
