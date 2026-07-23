using System;
using System.Runtime.InteropServices;
using System.Windows;
using Polishly.App.ViewModels;

namespace Polishly.App.Views;

public partial class ReviseInstructionView : Window
{
    private IntPtr _savedTargetWindowHandle = IntPtr.Zero;

    public ReviseInstructionView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    public ReviseInstructionView(ReviseInstructionViewModel viewModel) : this()
    {
        DataContext = viewModel;
        _savedTargetWindowHandle = viewModel.TargetWindowHandle;
        viewModel.InstructionSubmitted += (s, e) => Close();
        viewModel.Cancelled += (s, e) => Close();
    }


#if !HAS_WPF
    private void InitializeComponent()
    {
    }
#endif

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Preserve target window handle if not set
        if (_savedTargetWindowHandle == IntPtr.Zero && OperatingSystem.IsWindows())
        {
            _savedTargetWindowHandle = GetForegroundWindow();
        }

        // Transfer focus to input box
        Activate();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        RestoreFocusToTargetWindow();
    }

    public void RestoreFocusToTargetWindow()
    {
        if (_savedTargetWindowHandle != IntPtr.Zero && OperatingSystem.IsWindows())
        {
            SetForegroundWindow(_savedTargetWindowHandle);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
