namespace Polishly.Core;

public enum RewriteTrigger
{
    StartCapture,
    CaptureCompleted,
    StartRequest,
    ReceiveToken,
    CompleteStream,
    Accept,
    Cancel,
    Fail,
    Reset
}
