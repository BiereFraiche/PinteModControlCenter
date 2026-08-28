using System.Windows;
using PinteMod.ControlCenter.Views;

namespace PinteMod.ControlCenter.Services;

public static class PinteModMessageBox
{
    public static MessageBoxResult Show(string messageBoxText) =>
        Show(null, messageBoxText, "PinteMod Control Center", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.None);

    public static MessageBoxResult Show(string messageBoxText, string caption) =>
        Show(null, messageBoxText, caption, MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.None);

    public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon) =>
        Show(null, messageBoxText, caption, button, icon, MessageBoxResult.None);

    public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon, MessageBoxResult defaultResult) =>
        Show(null, messageBoxText, caption, button, icon, defaultResult);

    public static MessageBoxResult Show(Window? owner, string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon, MessageBoxResult defaultResult = MessageBoxResult.None)
    {
        var dialog = new PinteModMessageBoxWindow(messageBoxText, caption, button, icon, defaultResult);
        owner ??= Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                  ?? Application.Current?.MainWindow;
        if (owner is not null && owner != dialog && owner.IsVisible)
        {
            dialog.Owner = owner;
        }
        dialog.ShowDialog();
        return dialog.Result;
    }
}
