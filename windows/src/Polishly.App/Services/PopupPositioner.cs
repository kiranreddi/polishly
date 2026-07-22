using System;

namespace Polishly.App.Services;

public struct ScreenRect
{
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }

    public double Right => Left + Width;
    public double Bottom => Top + Height;

    public ScreenRect(double left, double top, double width, double height)
    {
        Left = left;
        Top = top;
        Width = width;
        Height = height;
    }
}

public struct ScreenPoint
{
    public double X { get; set; }
    public double Y { get; set; }

    public ScreenPoint(double x, double y)
    {
        X = x;
        Y = y;
    }
}

public class PopupPositioner
{
    public const double DefaultMargin = 4.0;

    public double DpiScale { get; set; } = 1.0;
    public bool WasFlippedAbove { get; private set; }

    public PopupPositioner(double dpiScale = 1.0)
    {
        DpiScale = dpiScale <= 0 ? 1.0 : dpiScale;
    }

    public static double DpiToScale(int dpi)
    {
        if (dpi <= 0) return 1.0;
        return dpi / 96.0;
    }

    public ScreenPoint CalculatePosition(
        ScreenRect selectionRect,
        ScreenRect workArea,
        double popupWidth,
        double popupHeight,
        double margin = DefaultMargin)
    {
        double scaledMargin = margin * DpiScale;

        // Horizontal alignment: anchor to selection Left
        double x = selectionRect.Left;

        // Horizontal clamping within monitor work area
        if (x + popupWidth > workArea.Right)
        {
            x = workArea.Right - popupWidth;
        }
        if (x < workArea.Left)
        {
            x = workArea.Left;
        }

        // Vertical placement: default below selection
        double yBelow = selectionRect.Bottom + scaledMargin;
        double y;

        if (yBelow + popupHeight <= workArea.Bottom)
        {
            // Fits below selection
            y = yBelow;
            WasFlippedAbove = false;
        }
        else
        {
            // Try flipping above selection
            double yAbove = selectionRect.Top - popupHeight - scaledMargin;
            if (yAbove >= workArea.Top)
            {
                y = yAbove;
                WasFlippedAbove = true;
            }
            else
            {
                // If neither fits completely, choose position with maximum visible area or clamp
                if (workArea.Bottom - yBelow >= yAbove - workArea.Top)
                {
                    y = Math.Min(yBelow, Math.Max(workArea.Top, workArea.Bottom - popupHeight));
                    WasFlippedAbove = false;
                }
                else
                {
                    y = Math.Max(workArea.Top, yAbove);
                    WasFlippedAbove = true;
                }
            }
        }

        return new ScreenPoint(x, y);
    }

    public ScreenPoint RecalculateForStreamingHeight(
        ScreenRect selectionRect,
        ScreenRect workArea,
        double popupWidth,
        double newHeight,
        double margin = DefaultMargin)
    {
        double scaledMargin = margin * DpiScale;
        double x = selectionRect.Left;

        if (x + popupWidth > workArea.Right)
        {
            x = workArea.Right - popupWidth;
        }
        if (x < workArea.Left)
        {
            x = workArea.Left;
        }

        double y;
        if (WasFlippedAbove)
        {
            // Keep bottom edge anchor above selection
            y = selectionRect.Top - newHeight - scaledMargin;
            if (y < workArea.Top)
            {
                y = workArea.Top;
            }
        }
        else
        {
            // Position below selection
            y = selectionRect.Bottom + scaledMargin;
            if (y + newHeight > workArea.Bottom)
            {
                // Dynamic smart flip if streaming causes overflow
                double yAbove = selectionRect.Top - newHeight - scaledMargin;
                if (yAbove >= workArea.Top)
                {
                    y = yAbove;
                    WasFlippedAbove = true;
                }
                else
                {
                    y = Math.Max(workArea.Top, workArea.Bottom - newHeight);
                }
            }
        }

        return new ScreenPoint(x, y);
    }
}
