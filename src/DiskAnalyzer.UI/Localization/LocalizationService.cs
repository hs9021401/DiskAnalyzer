using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text.Json;
using System.Windows;

namespace DiskAnalyzer.UI.Localization;

/// <summary>
/// The single UI-language seam. It owns culture selection, fallback, persistence,
/// and publication of translated values to WPF DynamicResource bindings.
/// </summary>
public sealed class LocalizationService : INotifyPropertyChanged
{
    public const string SystemDefaultCulture = "auto";
    public const string EnglishCulture = "en-US";

    private static readonly string[] ResourceKeys =
    [
        "WindowTitle", "MenuOpen", "MenuRevealInExplorer", "MenuCopyFullPath", "MenuCopyAllDetails",
        "MenuOpenCommandPromptHere", "MenuOpenPowerShellHere", "MenuZoomIntoFolder", "MenuToggleHeatmap",
        "MenuDeleteToRecycleBin", "MenuPermanentlyDelete", "MenuProperties", "BrandSubtitle",
        "BrowseButton", "BrowseTooltip", "ScanButton", "ScanTooltip", "CancelButton", "CancelTooltip",
        "ClearSearchTooltip", "AdminBadge", "RestartAdminButton", "RestartAdminTooltip", "HeatmapHideButton",
        "HeatmapShowButton", "HeatmapTooltip", "ExportCsvButton", "ExportCsvTooltip", "CopyInfoButton",
        "CopyInfoTooltip", "LanguageMenu", "LanguageSystemDefault", "TreeViewTab", "TreeNameHeader",
        "TreeParentPercentHeader", "SizeHeader", "AllocatedHeader", "FilesHeader", "FoldersHeader",
        "TotalPercentHeader", "LastModifiedHeader", "AttributesHeader", "FileViewTab", "FileNameHeader",
        "FolderHeader", "ExtensionShortHeader", "DateModifiedHeader", "FileTypesTab", "TotalSizeHeader",
        "PercentOfTotalHeader", "FileCountHeader", "AllocatedSizeHeader", "RootButton", "RootTooltip",
        "UpButton", "UpTooltip", "ActiveFormat", "SelectedFormat", "SelectedSizeFormat", "StatusReady",
        "InvalidPathTitle", "InvalidPathMessage", "ScanningPathStatus", "InitializingStatus",
        "ScanningFolderStatus", "ScanMetricsFormat", "ScanCompletedStatus", "ScanCompletedMetrics",
        "ScanCanceledStatus", "CanceledMetrics", "ScanErrorStatus", "ErrorMetrics", "ScanErrorTitle",
        "ScanErrorMessage", "CancelingStatus", "SelectFolderDialogTitle", "ElevationTitle", "ElevationMessage",
        "ExportCsvFilter", "ExportDialogTitle", "ExportingStatus", "ExportedStatus", "ExportCompleteTitle",
        "ExportCompleteMessage", "ExportErrorStatus", "ExportFailedTitle", "ExportFailedMessage",
        "CopiedPathStatus", "CopiedPathsStatus", "CopiedDetailsStatus", "CopiedDetailsManyStatus",
        "CopyFailedStatus", "OpenFailedTitle", "OpenFileFailedMessage", "OpenFolderFailedMessage",
        "ConfirmDeleteTitle", "ConfirmDeleteSingle", "ConfirmDeleteMany", "MovedToRecycleBinStatus",
        "MovedItemsToRecycleBinStatus", "DeleteFailedTitle", "DeleteFailedMessage", "ConfirmPermanentDeleteTitle",
        "ConfirmPermanentDeleteSingle", "ConfirmPermanentDeleteMany", "PermanentlyDeletedStatus",
        "PermanentlyDeletedItemsStatus", "PermanentDeleteFailedTitle", "PermanentDeleteFailedMessage",
        "ProgressInitializing", "ProgressReadingMft", "ProgressReadingUsn", "ProgressScanningFolder",
        "ProgressBuildingTree", "ProgressCalculatingSizes", "ProgressSorting", "ProgressComplete",
        "ProgressCancelled", "ProgressError", "ProgressScanning", "NoExtensionLabel", "FolderAnalysisTitle", "CustomFolderLabel",
        "ScanningActiveTarget", "NoDriveSelected", "TotalFormat", "UsedFormat", "FreeFormat", "LocalDiskLabel",
        "TreemapSizeFormat", "TreemapAllocatedFormat", "TreemapParentPercentFormat", "TreemapFilesFoldersFormat",
        "TreemapFilesFoldersValueFormat",
        "TreemapModifiedFormat", "TreemapTypeFormat", "FullPathLabel", "BytesLabel", "UnexpectedErrorTitle",
        "UnexpectedErrorMessage", "NoTreemapDataMessage"
    ];

    private static readonly IReadOnlyDictionary<string, string> LanguageNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [SystemDefaultCulture] = "System Default",
            ["en-US"] = "English",
            ["zh-TW"] = "繁體中文",
            ["zh-CN"] = "简体中文",
            ["ja-JP"] = "日本語",
            ["ko-KR"] = "한국어",
            ["es-ES"] = "Español",
            ["fr-FR"] = "Français"
        };

    private static readonly string SettingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DiskAnalyzer",
        "settings.json");

    private readonly ResourceManager _resourceManager = new(
        "DiskAnalyzer.UI.Resources.Strings",
        typeof(LocalizationService).Assembly);
    private readonly ReadOnlyObservableCollection<LanguageOption> _languages;
    private readonly ObservableCollection<LanguageOption> _languageItems = [];
    private string _selectedCultureName;
    private CultureInfo _currentCulture;

    public static LocalizationService Instance { get; } = new();

    public LocalizationService()
    {
        foreach (var language in LanguageNames)
        {
            _languageItems.Add(new LanguageOption(
                language.Key,
                language.Value,
                cultureName => SetLanguage(cultureName)));
        }

        _languages = new ReadOnlyObservableCollection<LanguageOption>(_languageItems);
        _selectedCultureName = LoadLanguagePreference();
        _currentCulture = ResolveCulture(_selectedCultureName);
        UpdateLanguageSelection();
    }

    public ReadOnlyObservableCollection<LanguageOption> Languages => _languages;

    public IReadOnlyList<string> Keys => ResourceKeys;

    public string SelectedCultureName => _selectedCultureName;

    public CultureInfo CurrentCulture => _currentCulture;

    public event EventHandler? LanguageChanged;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Get(string key)
    {
        return GetResource(key, _currentCulture)
            ?? GetResource(key, CultureInfo.GetCultureInfo(EnglishCulture))
            ?? key;
    }

    public string Format(string key, params object?[] args)
    {
        string template = Get(key);
        try
        {
            return string.Format(_currentCulture, template, args);
        }
        catch (FormatException)
        {
            return template;
        }
    }

    public void SetLanguage(string cultureName, bool persist = true)
    {
        if (!LanguageNames.ContainsKey(cultureName))
            cultureName = SystemDefaultCulture;

        _selectedCultureName = cultureName;
        _currentCulture = ResolveCulture(cultureName);
        UpdateLanguageSelection();

        if (persist)
            SaveLanguagePreference(cultureName);

        ApplyTo(Application.Current?.Resources);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentCulture)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedCultureName)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ApplyTo(ResourceDictionary? resources)
    {
        if (resources == null)
            return;

        foreach (string key in ResourceKeys)
        {
            resources[ToResourceKey(key)] = Get(key);
        }
    }

    public static string ToResourceKey(string key) => $"Loc.{key}";

    private string? GetResource(string key, CultureInfo culture)
    {
        try
        {
            return _resourceManager.GetString(key, culture);
        }
        catch (MissingManifestResourceException)
        {
            return null;
        }
    }

    private static CultureInfo ResolveCulture(string cultureName)
    {
        if (!string.Equals(cultureName, SystemDefaultCulture, StringComparison.OrdinalIgnoreCase))
        {
            return CultureInfo.GetCultureInfo(
                LanguageNames.ContainsKey(cultureName) ? cultureName : EnglishCulture);
        }

        CultureInfo systemCulture = CultureInfo.CurrentUICulture;
        if (LanguageNames.ContainsKey(systemCulture.Name))
            return CultureInfo.GetCultureInfo(systemCulture.Name);

        return systemCulture.TwoLetterISOLanguageName switch
        {
            "zh" => CultureInfo.GetCultureInfo("zh-TW"),
            "ja" => CultureInfo.GetCultureInfo("ja-JP"),
            "ko" => CultureInfo.GetCultureInfo("ko-KR"),
            "es" => CultureInfo.GetCultureInfo("es-ES"),
            "fr" => CultureInfo.GetCultureInfo("fr-FR"),
            _ => CultureInfo.GetCultureInfo(EnglishCulture)
        };
    }

    private void UpdateLanguageSelection()
    {
        foreach (LanguageOption language in _languageItems)
        {
            language.DisplayName = language.CultureName.Equals(
                SystemDefaultCulture,
                StringComparison.OrdinalIgnoreCase)
                ? Get("LanguageSystemDefault")
                : LanguageNames[language.CultureName];
            language.IsSelected = string.Equals(
                language.CultureName,
                _selectedCultureName,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string LoadLanguagePreference()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
                return SystemDefaultCulture;

            var settings = JsonSerializer.Deserialize<LanguageSettings>(File.ReadAllText(SettingsFilePath));
            return settings?.Language != null && LanguageNames.ContainsKey(settings.Language)
                ? settings.Language
                : SystemDefaultCulture;
        }
        catch
        {
            return SystemDefaultCulture;
        }
    }

    private static void SaveLanguagePreference(string cultureName)
    {
        try
        {
            string? directory = Path.GetDirectoryName(SettingsFilePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(
                SettingsFilePath,
                JsonSerializer.Serialize(new LanguageSettings { Language = cultureName }));
        }
        catch
        {
            // Language switching must continue even when the settings file cannot be written.
        }
    }

    private sealed class LanguageSettings
    {
        public string? Language { get; set; }
    }
}
