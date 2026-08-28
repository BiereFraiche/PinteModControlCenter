using System.Collections.Specialized;
using System.Windows.Controls;
using System.Windows.Threading;
using PinteMod.ControlCenter.ViewModels;

namespace PinteMod.ControlCenter.Views;

public partial class PlayerChatView : UserControl
{
    private PlayerChatViewModel? _viewModel;
    private bool _scrollToEndPending;

    public PlayerChatView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        DetachViewModel();
        _viewModel = e.NewValue as PlayerChatViewModel;
        if (_viewModel is not null)
        {
            _viewModel.Messages.CollectionChanged += OnMessagesChanged;
            ScheduleScrollToEnd();
        }
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ScheduleScrollToEnd();
    }

    private void ScheduleScrollToEnd()
    {
        if (_scrollToEndPending || _viewModel is null)
        {
            return;
        }

        _scrollToEndPending = true;
        Dispatcher.BeginInvoke(
            () =>
            {
                _scrollToEndPending = false;
                if (_viewModel is null)
                {
                    return;
                }

                ChatScroller.UpdateLayout();
                ChatScroller.ScrollToVerticalOffset(ChatScroller.ScrollableHeight);
            },
            DispatcherPriority.ContextIdle);
    }

    private void DetachViewModel()
    {
        if (_viewModel is not null)
        {
            _viewModel.Messages.CollectionChanged -= OnMessagesChanged;
            _viewModel = null;
        }
    }
}
