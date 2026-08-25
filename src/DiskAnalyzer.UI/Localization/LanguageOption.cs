using System;
using System.ComponentModel;
using DiskAnalyzer.UI.Helpers;

namespace DiskAnalyzer.UI.Localization;

public sealed class LanguageOption : INotifyPropertyChanged
{
    private string _displayName;
    private bool _isSelected;

    internal LanguageOption(string cultureName, string displayName, Action<string> selectLanguage)
    {
        CultureName = cultureName;
        _displayName = displayName;
        SelectCommand = new RelayCommand(() => selectLanguage(CultureName));
    }

    public string CultureName { get; }

    public RelayCommand SelectCommand { get; }

    public string DisplayName
    {
        get => _displayName;
        internal set
        {
            if (string.Equals(_displayName, value, StringComparison.Ordinal))
                return;

            _displayName = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        internal set
        {
            if (_isSelected == value)
                return;

            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
