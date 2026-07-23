#if !HAS_WPF
using System;

namespace System.Windows
{
    public enum Visibility
    {
        Visible = 0,
        Hidden = 1,
        Collapsed = 2
    }

    public enum WindowStyle
    {
        None = 0,
        SingleBorderWindow = 1,
        ThreeDBorderWindow = 2,
        ToolWindow = 3
    }

    public class DependencyObject
    {
    }

    public class RoutedEventArgs : EventArgs
    {
        public object? Source { get; set; }
    }

    public delegate void RoutedEventHandler(object sender, RoutedEventArgs e);

    public class UIElement : DependencyObject
    {
        public Visibility Visibility { get; set; } = Visibility.Visible;
    }

    public class FrameworkElement : UIElement
    {
        public object? DataContext { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double ActualWidth { get; set; } = 300;
        public double ActualHeight { get; set; } = 150;
        public double MinWidth { get; set; }
        public double MaxWidth { get; set; }
        public double MinHeight { get; set; }
        public double MaxHeight { get; set; }

        public event RoutedEventHandler? Loaded;

        protected virtual void OnLoaded(RoutedEventArgs e)
        {
            Loaded?.Invoke(this, e);
        }
    }

    public class Window : FrameworkElement
    {
        public bool Topmost { get; set; }
        public bool ShowInTaskbar { get; set; }
        public WindowStyle WindowStyle { get; set; }
        public bool AllowsTransparency { get; set; }
        public double Left { get; set; }
        public double Top { get; set; }
        public string Title { get; set; } = string.Empty;

        public event EventHandler? SourceInitialized;
        public event EventHandler? Closed;
        public event EventHandler? Deactivated;

        public virtual bool Activate()
        {
            return true;
        }


        public virtual void Show()
        {
            Visibility = Visibility.Visible;
        }

        public virtual void Hide()
        {
            Visibility = Visibility.Collapsed;
        }

        public virtual void Close()
        {
            Closed?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnSourceInitialized(EventArgs e)
        {
            SourceInitialized?.Invoke(this, e);
        }

        protected virtual void OnDeactivated(EventArgs e)
        {
            Deactivated?.Invoke(this, e);
        }
    }
}

namespace System.Windows.Controls
{
    public class UserControl : FrameworkElement
    {
    }
}

namespace System.Windows.Interop
{
    public class WindowInteropHelper
    {
        public IntPtr Handle { get; }

        public WindowInteropHelper(Window window)
        {
            Handle = IntPtr.Zero;
        }
    }
}
#endif
