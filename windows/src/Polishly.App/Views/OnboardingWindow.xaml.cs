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
    }

    private void InitializeComponent()
    {
    }
}
