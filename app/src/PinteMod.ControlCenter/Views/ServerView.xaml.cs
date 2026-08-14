using System.Windows.Controls;

namespace PinteMod.ControlCenter.Views;

public partial class ServerView : UserControl
{
    public ServerView()
    {
        InitializeComponent();
    }

    private async void SetJoinPassword_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not ViewModels.ServerViewModel viewModel)
        {
            return;
        }

        var joinPassword = JoinPasswordBox.Password;
        JoinPasswordBox.Clear();
        await viewModel.SetJoinPasswordAsync(joinPassword);
        joinPassword = string.Empty;
    }
}
