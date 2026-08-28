using System.Windows.Controls;
using System.Collections.Specialized;
using System.Windows.Threading;
using PinteMod.ControlCenter.ViewModels;

namespace PinteMod.ControlCenter.Views;

public partial class LogsView : UserControl
{
    private LogsViewModel? _viewModel;
    private bool _scrollToEndPending;

    public LogsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        DetachViewModel();
        _viewModel = e.NewValue as LogsViewModel;
        if (_viewModel is not null)
        {
            _viewModel.Events.CollectionChanged += OnEventsChanged;
            ScheduleScrollToEnd();
        }
    }

    private void OnEventsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ScheduleScrollToEnd();
    }

    private void ScheduleScrollToEnd()
    {
        if (_scrollToEndPending || _viewModel is not { AutoScrollEnabled: true, IsDisplayPaused: false })
        {
            return;
        }

        _scrollToEndPending = true;
        Dispatcher.BeginInvoke(
            () =>
            {
                _scrollToEndPending = false;
                if (_viewModel is not { AutoScrollEnabled: true, IsDisplayPaused: false })
                {
                    return;
                }

                EventScroller.UpdateLayout();
                EventScroller.ScrollToVerticalOffset(EventScroller.ScrollableHeight);
            },
            DispatcherPriority.ContextIdle);
    }

    private void DetachViewModel()
    {
        if (_viewModel is not null)
        {
            _viewModel.Events.CollectionChanged -= OnEventsChanged;
            _viewModel = null;
        }
    }
}
