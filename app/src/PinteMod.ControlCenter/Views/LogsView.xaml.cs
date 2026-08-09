using System.Windows.Controls;
using System.Collections.Specialized;
using System.Windows.Threading;
using PinteMod.ControlCenter.ViewModels;

namespace PinteMod.ControlCenter.Views;

public partial class LogsView : UserControl
{
    private LogsViewModel? _viewModel;

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
        }
    }

    private void OnEventsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_viewModel is not { AutoScrollEnabled: true, IsDisplayPaused: false })
        {
            return;
        }

        Dispatcher.BeginInvoke(
            () => EventScroller.ScrollToEnd(),
            DispatcherPriority.Background);
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
