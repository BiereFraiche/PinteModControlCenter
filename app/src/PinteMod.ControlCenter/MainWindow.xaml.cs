using System.Windows;
using System.Windows.Controls;
using PinteMod.ControlCenter.ViewModels;

namespace PinteMod.ControlCenter;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void LanguageSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is ControlCenterWorkspaceViewModel workspace &&
            LanguageSelector.SelectedValue is string languageCode)
        {
            await workspace.SetUiLanguageAsync(languageCode);
        }
    }

}
