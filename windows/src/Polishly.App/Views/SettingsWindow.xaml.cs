using System.Windows;
using System.Windows.Threading;
using System.ComponentModel;
using Polishly.App.ViewModels;

namespace Polishly.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    public SettingsWindow(SettingsViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.SettingsSaved += (s, e) => Close();
        viewModel.PropertyChanged += ViewModel_PropertyChanged;
        Closed += (s, e) => viewModel.PropertyChanged -= ViewModel_PropertyChanged;
#if HAS_WPF
        Loaded += (s, e) =>
        {
            SyncApiKeyBox(viewModel);

            Dispatcher.BeginInvoke(
                () => SettingsScrollViewer.ScrollToTop(),
                DispatcherPriority.ContextIdle);
        };
#endif
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.ApiKey) && sender is SettingsViewModel viewModel)
        {
            SyncApiKeyBox(viewModel);
        }
    }

    private void SyncApiKeyBox(SettingsViewModel viewModel)
    {
#if HAS_WPF
        if (ApiKeyBox != null && ApiKeyBox.Password != viewModel.ApiKey)
        {
            ApiKeyBox.Password = viewModel.ApiKey;
        }
#endif
    }

#if HAS_WPF
    private void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm && sender is System.Windows.Controls.PasswordBox box)
        {
            vm.ApiKey = box.Password;
        }
    }
#endif

#if !HAS_WPF
    private void InitializeComponent()
    {
    }
#endif

#if HAS_WPF
    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
#endif
}

