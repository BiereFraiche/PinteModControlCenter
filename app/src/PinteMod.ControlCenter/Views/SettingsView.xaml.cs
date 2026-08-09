using System.Windows;
using System.Windows.Controls;
using PinteMod.ControlCenter.ViewModels;

namespace PinteMod.ControlCenter.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private async void SaveRconSecret_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel)
        {
            return;
        }

        var secret = RconPasswordBox.Password;
        RconPasswordBox.Clear();
        await viewModel.SaveRconSecretAsync(secret);
    }
}
