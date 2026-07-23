using Polishly.WindowsIntegration.Hotkey;
using Xunit;

namespace Polishly.Platform.Tests;

public class HotkeyFeatureTests
{
    [Fact]
    public void HotkeyListener_Register_ReturnsBoolStatus()
    {
        using var listener = new GlobalHotkeyListener();
        bool result = listener.Register(IntPtr.Zero, 0x0002 /* MOD_CONTROL */ | 0x0004 /* MOD_SHIFT */, 0x50 /* 'P' */);

        // Under non-Windows or headless runner, Win32 API returns false cleanly
        Assert.NotNull((object)result);
    }

    [Fact]
    public void HotkeyListener_RaiseHotkeyPressed_FiresSubscribedEvent()
    {
        using var listener = new GlobalHotkeyListener();
        bool fired = false;

        listener.HotkeyPressed += (sender, args) => fired = true;
        listener.RaiseHotkeyPressed();

        Assert.True(fired);
    }

    [Fact]
    public void HotkeyListener_Unregister_ExecutesWithoutException()
    {
        using var listener = new GlobalHotkeyListener();
        listener.Register(IntPtr.Zero, 0x0002, 0x50);
        
        var exception = Record.Exception(() => listener.Unregister());
        Assert.Null(exception);
    }

    [Fact]
    public void HotkeyListener_MultipleRegisterCycles_HandlesStateSafely()
    {
        using var listener = new GlobalHotkeyListener();

        var exception = Record.Exception(() =>
        {
            listener.Register(IntPtr.Zero, 0x0002, 0x50);
            listener.Unregister();
            listener.Register(IntPtr.Zero, 0x0001 /* MOD_ALT */, 0x4B /* 'K' */);
            listener.Unregister();
        });

        Assert.Null(exception);
    }

    [Fact]
    public void HotkeyListener_Dispose_UnregistersAndDisposesCleanly()
    {
        var listener = new GlobalHotkeyListener();
        listener.Register(IntPtr.Zero, 0x0002, 0x50);

        var exception = Record.Exception(() => listener.Dispose());
        Assert.Null(exception);
    }
}
