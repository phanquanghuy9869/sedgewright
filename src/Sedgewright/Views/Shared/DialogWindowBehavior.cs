using System;
using Avalonia.Controls;
using Avalonia.Input;

namespace Sedgewright.Views.Shared;

internal static class DialogWindowBehavior
{
    private const double ScreenMargin = 24;

    public static void Attach(Window window, Control? dragRegion = null)
    {
        window.Opened += (_, _) => ClampToWorkingArea(window);
        window.KeyDown += (_, e) => HandleKeyDown(window, e);

        if (dragRegion is not null)
        {
            dragRegion.PointerPressed += (_, e) => HandleDragRegionPointerPressed(window, e);
        }
    }

    private static void HandleKeyDown(Window window, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        e.Handled = true;
        window.Close();
    }

    private static void HandleDragRegionPointerPressed(Window window, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(window).Properties.IsLeftButtonPressed is false)
        {
            return;
        }

        if (e.Source is Control sourceControl && sourceControl is Button)
        {
            return;
        }

        window.BeginMoveDrag(e);
    }

    private static void ClampToWorkingArea(Window window)
    {
        var screen = window.Screens.ScreenFromVisual(window);
        if (screen is null)
        {
            return;
        }

        double scaling = window.DesktopScaling;
        if (scaling <= 0)
        {
            scaling = 1.0;
        }

        double availableWidth = Math.Max(0, (screen.WorkingArea.Width / scaling) - ScreenMargin);
        double availableHeight = Math.Max(0, (screen.WorkingArea.Height / scaling) - ScreenMargin);
        if (availableWidth <= 0 || availableHeight <= 0)
        {
            return;
        }

        window.MaxWidth = availableWidth;
        window.MaxHeight = availableHeight;
        window.MinWidth = Math.Min(window.MinWidth, availableWidth);
        window.MinHeight = Math.Min(window.MinHeight, availableHeight);

        if (!double.IsNaN(window.Width))
        {
            window.Width = Math.Min(window.Width, availableWidth);
        }

        if (!double.IsNaN(window.Height))
        {
            window.Height = Math.Min(window.Height, availableHeight);
        }
    }
}
