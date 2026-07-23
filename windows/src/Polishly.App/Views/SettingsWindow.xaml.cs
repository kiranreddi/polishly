using System.Windows;
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
#if HAS_WPF
        Loaded += (s, e) =>
        {
            if (ApiKeyBox != null && !string.IsNullOrEmpty(viewModel.ApiKey))
            {
                ApiKeyBox.Password = viewModel.ApiKey;
            }
        };
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
}

