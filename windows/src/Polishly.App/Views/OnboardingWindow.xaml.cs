using System.Windows;
using Polishly.App.ViewModels;

namespace Polishly.App.Views;

public partial class OnboardingWindow : Window
{
    public OnboardingWindow()
    {
        InitializeComponent();
    }

    public OnboardingWindow(OnboardingViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.OnboardingCompleted += (s, e) => Close();
#if HAS_WPF
        Loaded += (_, _) =>
        {
            OnboardingApiKeyBox.Password = viewModel.Settings.ApiKey;
        };
        viewModel.Settings.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SettingsViewModel.ApiKey) &&
                OnboardingApiKeyBox.Password != viewModel.Settings.ApiKey)
            {
                OnboardingApiKeyBox.Password = viewModel.Settings.ApiKey;
            }
        };
#endif
    }

#if HAS_WPF
    private void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is OnboardingViewModel vm &&
            sender is System.Windows.Controls.PasswordBox passwordBox)
        {
            vm.Settings.ApiKey = passwordBox.Password;
        }
    }
#endif

#if !HAS_WPF
    private void InitializeComponent()
    {
    }
#endif
}
