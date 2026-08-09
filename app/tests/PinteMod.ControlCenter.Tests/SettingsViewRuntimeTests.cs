using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.ViewModels;
using PinteMod.ControlCenter.Views;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class SettingsViewRuntimeTests
{
    [TestMethod]
    public void SettingsView_CanBeLaidOutWithItsViewModel()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var application = new Application();
                application.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri(
                        "pack://application:,,,/PinteMod.ControlCenter;component/Themes/PinteModTheme.xaml",
                        UriKind.Absolute),
                });

                var view = new SettingsView
                {
                    DataContext = new SettingsViewModel(),
                };

                view.Measure(new Size(1280, 900));
                view.Arrange(new Rect(0, 0, 1280, 900));
                view.UpdateLayout();
                Dispatcher.CurrentDispatcher.Invoke(
                    () => { },
                    DispatcherPriority.ApplicationIdle);

                var passwordBox = FindLogicalChildren<PasswordBox>(view).Single();
                var surfaceColor = ((SolidColorBrush)application.FindResource("SurfaceAltBrush")).Color;
                var textColor = ((SolidColorBrush)application.FindResource("TextPrimaryBrush")).Color;
                Assert.AreEqual(surfaceColor, ((SolidColorBrush)passwordBox.Background).Color);
                Assert.AreEqual(textColor, ((SolidColorBrush)passwordBox.Foreground).Color);

                var buttons = FindLogicalChildren<Button>(view).ToArray();
                Assert.IsTrue(buttons.Length >= 6);
                Assert.IsFalse(buttons.Any(button =>
                    button.Background is SolidColorBrush background &&
                    button.Foreground is SolidColorBrush foreground &&
                    background.Color == foreground.Color));
                Assert.IsTrue(FindLogicalChildren<TextBlock>(view).Any(text =>
                    text.Text.Contains("Une simple copie des fichiers serveur ne suffit pas.", StringComparison.Ordinal)));

                application.Shutdown();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(15)), "Le test WPF n'a pas terminé dans le délai imparti.");

        Assert.IsNull(failure, failure?.ToString());
    }

    private static IEnumerable<T> FindLogicalChildren<T>(DependencyObject root)
        where T : DependencyObject
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in FindLogicalChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }
}
