using Xunit;
using Polishly.App.Views;

namespace Polishly.Platform.Tests;

public class NonActivatingWindowFlagsTests
{
    [Fact]
    public void PopupWindow_ExStyleConstants_MatchWin32Specifications()
    {
        Assert.Equal(-20, PopupWindow.GWL_EXSTYLE);
        Assert.Equal(0x08000000L, PopupWindow.WS_EX_NOACTIVATE);
        Assert.Equal(0x00000080L, PopupWindow.WS_EX_TOOLWINDOW);
    }

    [Fact]
    public void PopupWindow_GetNonActivatingExStyleFlags_CombinesNoActivateAndToolWindow()
    {
        long combined = PopupWindow.GetNonActivatingExStyleFlags();
        long expected = 0x08000000L | 0x00000080L;

        Assert.Equal(0x08000080L, combined);
        Assert.Equal(expected, combined);
        Assert.True((combined & PopupWindow.WS_EX_NOACTIVATE) != 0);
        Assert.True((combined & PopupWindow.WS_EX_TOOLWINDOW) != 0);
    }
}
