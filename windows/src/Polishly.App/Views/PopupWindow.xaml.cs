using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Polishly.App.ViewModels;
using Polishly.Core.Diff;

#if HAS_WPF
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Documents;
using System.Windows.Media;
#endif

namespace Polishly.App.Views;

public partial class PopupWindow : Window
{
    public const int GWL_EXSTYLE = -20;
    public const long WS_EX_NOACTIVATE = 0x08000000L;
    public const long WS_EX_TOOLWINDOW = 0x00000080L;

    public static long GetNonActivatingExStyleFlags() => WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;

    /// <summary>
    /// Makes the transient window discoverable by UI automation during the
    /// Computer Use stress harness without changing the normal tray UX.
    /// </summary>
    public bool IsComputerUseTestMode { get; set; }

    public PopupWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
    }

    public PopupWindow(PopupViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        RenderDiff();
    }

#if !HAS_WPF
    private void InitializeComponent()
    {
    }
#endif

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PopupViewModel.DiffSegments))
            return;

#if HAS_WPF
        if (Dispatcher.CheckAccess())
            RenderDiff();
        else
            Dispatcher.BeginInvoke(new Action(RenderDiff));
#endif
    }

    private void RenderDiff()
    {
#if HAS_WPF
        if (DiffTextBlock == null || DataContext is not PopupViewModel viewModel)
            return;

        DiffTextBlock.Inlines.Clear();

        var accent = TryFindResource("AccentBrush") as Brush;
        var accentSoft = TryFindResource("AccentSoftBrush") as Brush;
        var danger = TryFindResource("DangerBrush") as Brush;
        var dangerSoft = TryFindResource("DangerSoftBrush") as Brush;
        var primary = TryFindResource("TextPrimaryBrush") as Brush;

        foreach (var segment in viewModel.DiffSegments)
        {
            foreach (var token in SplitDiffText(segment.Text))
            {
                var run = new Run(token) { Foreground = primary };
                if (!string.IsNullOrWhiteSpace(token))
                {
                    switch (segment.Type)
                    {
                        case DiffType.Addition:
                            run.Foreground = accent;
                            run.Background = accentSoft;
                            run.FontWeight = FontWeights.SemiBold;
                            break;
                        case DiffType.Deletion:
                            run.Foreground = danger;
                            run.Background = dangerSoft;
                            run.TextDecorations = TextDecorations.Strikethrough;
                            break;
                    }
                }

                DiffTextBlock.Inlines.Add(run);
            }
        }
#endif
    }

#if HAS_WPF
    private static string[] SplitDiffText(string text)
    {
        return Regex.Split(text, @"(\s+|[,.!?;:()\[\]{}\u2014\u2013-])")
            .Where(token => token.Length > 0)
            .ToArray();
    }
#endif


    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        ApplyNonActivatingStyle();
    }

    public void ApplyNonActivatingStyle()
    {
#if HAS_WPF
        var helper = new WindowInteropHelper(this);
        IntPtr hWnd = helper.Handle;
        if (hWnd != IntPtr.Zero && OperatingSystem.IsWindows())
        {
            long exStyle = GetWindowLongPtrInternal(hWnd, GWL_EXSTYLE);
            exStyle |= WS_EX_NOACTIVATE;
            if (IsComputerUseTestMode)
            {
                exStyle &= ~WS_EX_TOOLWINDOW;
            }
            else
            {
                exStyle |= WS_EX_TOOLWINDOW;
            }
            SetWindowLongPtrInternal(hWnd, GWL_EXSTYLE, (IntPtr)exStyle);
        }
#endif
    }

    internal static long GetWindowLongPtrInternal(IntPtr hWnd, int nIndex)
    {
        if (IntPtr.Size == 8)
            return GetWindowLongPtr64(hWnd, nIndex).ToInt64();
        return GetWindowLong32(hWnd, nIndex);
    }

    internal static IntPtr SetWindowLongPtrInternal(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        if (IntPtr.Size == 8)
            return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
        return (IntPtr)SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32());
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
}
