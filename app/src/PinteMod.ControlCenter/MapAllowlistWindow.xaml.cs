using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace PinteMod.ControlCenter;

public partial class MapAllowlistWindow : Window
{
    private sealed class MapOption(string code, string displayName) : INotifyPropertyChanged
    {
        private bool _isSelected;
        public string Code { get; } = code;
        public string DisplayName { get; } = displayName;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private readonly ObservableCollection<MapOption> _maps =
    [
        new("zm_zod", "Shadows of Evil"),
        new("zm_castle", "Der Eisendrache"),
        new("zm_island", "Zetsubou No Shima"),
        new("zm_stalingrad", "Gorod Krovi"),
        new("zm_genesis", "Revelations"),
        new("zm_factory", "The Giant"),
        new("zm_prototype", "Nacht der Untoten"),
        new("zm_asylum", "Verrückt"),
        new("zm_sumpf", "Shi No Numa"),
        new("zm_theater", "Kino der Toten"),
        new("zm_cosmodrome", "Ascension"),
        new("zm_temple", "Shangri-La"),
        new("zm_moon", "Moon"),
        new("zm_tomb", "Origins")
    ];

    public MapAllowlistWindow()
    {
        InitializeComponent();
        MapsList.ItemsSource = _maps;
    }

    public IReadOnlyList<string> SelectedMapCodes =>
        _maps.Where(map => map.IsSelected).Select(map => map.Code).ToArray();

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var map in _maps) map.IsSelected = true;
    }

    private void SelectNone_Click(object sender, RoutedEventArgs e)
    {
        foreach (var map in _maps) map.IsSelected = false;
    }

    private void Validate_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
