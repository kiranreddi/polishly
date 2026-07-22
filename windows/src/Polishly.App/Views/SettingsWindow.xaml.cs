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
    }

    private void InitializeComponent()
    {
    }
}
