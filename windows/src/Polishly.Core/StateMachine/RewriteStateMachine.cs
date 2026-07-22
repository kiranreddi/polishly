namespace Polishly.Core.StateMachine;

public class RewriteStateMachine : IRewriteStateMachine
{
    private readonly object _lock = new();
    private RewriteState _currentState = RewriteState.Idle;

    public RewriteState CurrentState
    {
        get
        {
            lock (_lock)
            {
                return _currentState;
            }
        }
    }

    public event EventHandler<RewriteStateChangedEventArgs>? StateChanged;

    public void Transition(RewriteEvent @event, string? payload = null)
    {
        RewriteState previousState;
        RewriteState newState;

        lock (_lock)
        {
            previousState = _currentState;
            newState = GetNextState(previousState, @event);

            if (previousState == newState && payload == null)
            {
                return;
            }

            _currentState = newState;
        }

        StateChanged?.Invoke(this, new RewriteStateChangedEventArgs(previousState, newState, payload));
    }

    private static RewriteState GetNextState(RewriteState current, RewriteEvent @event)
    {
        return (@event, current) switch
        {
            (RewriteEvent.TriggerHotkey, _) => RewriteState.Capturing,
            (RewriteEvent.CaptureSuccess, RewriteState.Capturing) => RewriteState.StreamReady,
            (RewriteEvent.CaptureFailed, RewriteState.Capturing) => RewriteState.Failed,
            (RewriteEvent.StartStreaming, RewriteState.StreamReady) => RewriteState.Streaming,
            (RewriteEvent.ReceiveToken, RewriteState.Streaming) => RewriteState.Streaming,
            (RewriteEvent.StreamFinished, RewriteState.Streaming) => RewriteState.StreamComplete,
            (RewriteEvent.Accept, RewriteState.StreamComplete) => RewriteState.Replacing,
            (RewriteEvent.Copy, RewriteState.StreamComplete) => RewriteState.Copied,
            (RewriteEvent.Copy, RewriteState.Failed) => RewriteState.Copied,
            (RewriteEvent.Reject, _) => RewriteState.Cancelled,
            (RewriteEvent.Error, _) => RewriteState.Failed,
            _ => current
        };
    }
}
