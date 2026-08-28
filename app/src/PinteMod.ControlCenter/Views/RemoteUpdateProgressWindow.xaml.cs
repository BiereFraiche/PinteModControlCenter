using System.Windows;
using PinteMod.ControlCenter.Services;

namespace PinteMod.ControlCenter.Views;

public partial class RemoteUpdateProgressWindow : Window
{
    public RemoteUpdateProgressWindow() => InitializeComponent();

    public void Report(RemoteAgentUpdateProgress update)
    {
        Progress.Value = Math.Clamp(update.Percent, 0, 100);
        PercentText.Text = $"{Math.Clamp(update.Percent, 0, 100)} %";
        StepText.Text = update.Message;
    }
}
