using Xunit;
using Polishly.App.Services;

namespace Polishly.Platform.Tests;

public class PopupPositionerTests
{
    private readonly ScreenRect _standardWorkArea = new(0, 0, 1920, 1080);

    [Fact]
    public void PopupPositioner_NormalPlacement_BelowSelection()
    {
        var positioner = new PopupPositioner(1.0);
        var selection = new ScreenRect(100, 200, 150, 20);

        var pos = positioner.CalculatePosition(selection, _standardWorkArea, 350, 150);

        // Expect x = selection.Left (100)
        // Expect y = selection.Bottom (220) + margin (4) = 224
        Assert.Equal(100, pos.X);
        Assert.Equal(224, pos.Y);
        Assert.False(positioner.WasFlippedAbove);
    }

    [Theory]
    [InlineData(96, 1.0, 4.0)]
    [InlineData(120, 1.25, 5.0)]
    [InlineData(144, 1.50, 6.0)]
    [InlineData(192, 2.00, 8.0)]
    public void PopupPositioner_DpiAwareness_ScalesMarginCorrectly(int dpi, double expectedScale, double expectedScaledMargin)
    {
        double scale = PopupPositioner.DpiToScale(dpi);
        Assert.Equal(expectedScale, scale);

        var positioner = new PopupPositioner(scale);
        var selection = new ScreenRect(100, 200, 150, 20);

        var pos = positioner.CalculatePosition(selection, _standardWorkArea, 350, 150);

        Assert.Equal(100, pos.X);
        Assert.Equal(200 + 20 + expectedScaledMargin, pos.Y);
    }

    [Fact]
    public void PopupPositioner_SmartFlipAbove_WhenOverflowingWorkAreaBottom()
    {
        var positioner = new PopupPositioner(1.0);
        // Selection near bottom of 1080p screen
        var selection = new ScreenRect(500, 1000, 200, 30);

        var pos = positioner.CalculatePosition(selection, _standardWorkArea, 400, 200);

        // Placing below would yield y = 1034 + 200 = 1234 > 1080 (overflow)
        // Smart flip above yields y = 1000 - 200 - 4 = 796
        Assert.Equal(500, pos.X);
        Assert.Equal(796, pos.Y);
        Assert.True(positioner.WasFlippedAbove);
    }

    [Fact]
    public void PopupPositioner_HorizontalClamping_RightEdgeWorkArea()
    {
        var positioner = new PopupPositioner(1.0);
        // Selection at rightmost boundary (Left = 1800, Width = 100)
        var selection = new ScreenRect(1800, 300, 100, 20);

        var pos = positioner.CalculatePosition(selection, _standardWorkArea, 300, 100);

        // x = 1800 + 300 = 2100 > 1920
        // Clamped x = 1920 - 300 = 1620
        Assert.Equal(1620, pos.X);
        Assert.Equal(324, pos.Y);
    }

    [Fact]
    public void PopupPositioner_DynamicHeightAdjustment_DuringStreaming()
    {
        var positioner = new PopupPositioner(1.0);
        var selection = new ScreenRect(100, 200, 150, 20);

        // Initial small height (100px)
        var pos1 = positioner.CalculatePosition(selection, _standardWorkArea, 350, 100);
        Assert.Equal(224, pos1.Y);

        // Token streaming expands height to 180px
        var pos2 = positioner.RecalculateForStreamingHeight(selection, _standardWorkArea, 350, 180);
        Assert.Equal(224, pos2.Y); // Remains anchored below selection
    }

    [Fact]
    public void PopupPositioner_DynamicHeightAdjustment_FlipsAboveWhenStreamingOverflows()
    {
        var positioner = new PopupPositioner(1.0);
        var selection = new ScreenRect(100, 900, 150, 20);

        // Initial height 100px fits below (y = 924 + 100 = 1024 <= 1080)
        var pos1 = positioner.CalculatePosition(selection, _standardWorkArea, 350, 100);
        Assert.Equal(924, pos1.Y);
        Assert.False(positioner.WasFlippedAbove);

        // Streaming text expands height to 200px (y = 924 + 200 = 1124 > 1080 overflow)
        var pos2 = positioner.RecalculateForStreamingHeight(selection, _standardWorkArea, 350, 200);

        // Smart flip above during streaming
        Assert.Equal(900 - 200 - 4, pos2.Y); // 696
        Assert.True(positioner.WasFlippedAbove);
    }
}
