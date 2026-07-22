namespace Polishly.Core.StateMachine;

public enum RewriteState
{
    Idle,
    Capturing,
    StreamReady,
    Streaming,
    StreamComplete,
    Replacing,
    Accepted,
    Copied,
    Failed,
    Cancelled
}

public enum RewriteEvent
{
    TriggerHotkey,
    CaptureSuccess,
    CaptureFailed,
    StartStreaming,
    ReceiveToken,
    StreamFinished,
    Accept,
    Copy,
    Reject,
    Error
}

public interface IRewriteStateMachine
{
    RewriteState CurrentState { get; }
    void Transition(RewriteEvent @event, string? payload = null);
    event EventHandler<RewriteStateChangedEventArgs>? StateChanged;
}

public class RewriteStateChangedEventArgs : EventArgs
{
    public RewriteState PreviousState { get; }
    public RewriteState NewState { get; }
    public string? Payload { get; }

    public RewriteStateChangedEventArgs(RewriteState previousState, RewriteState newState, string? payload = null)
    {
        PreviousState = previousState;
        NewState = newState;
        Payload = payload;
    }
}
