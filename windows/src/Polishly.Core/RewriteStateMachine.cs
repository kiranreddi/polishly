namespace Polishly.Core;

public class RewriteStateMachine
{
    private readonly object _lock = new();
    private RewriteState _currentState = RewriteState.Idle;
    private string? _lastError;

    public event EventHandler<StateChangedEventArgs>? StateChanged;

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

    public string? LastError
    {
        get
        {
            lock (_lock)
            {
                return _lastError;
            }
        }
    }

    public bool CanFire(RewriteTrigger trigger)
    {
        lock (_lock)
        {
            return TryGetNextState(_currentState, trigger, out _);
        }
    }

    public bool Fire(RewriteTrigger trigger, string? errorDetails = null)
    {
        RewriteState oldState;
        RewriteState newState;

        lock (_lock)
        {
            if (!TryGetNextState(_currentState, trigger, out newState))
            {
                return false;
            }

            oldState = _currentState;
            _currentState = newState;

            if (trigger == RewriteTrigger.Fail)
            {
                _lastError = errorDetails ?? "An unspecified error occurred.";
            }
            else if (trigger == RewriteTrigger.Reset)
            {
                _lastError = null;
            }
        }

        if (oldState != newState)
        {
            StateChanged?.Invoke(this, new StateChangedEventArgs(oldState, newState, trigger, errorDetails));
        }
        return true;
    }

    public void Reset()
    {
        Fire(RewriteTrigger.Reset);
    }

    private static bool TryGetNextState(RewriteState current, RewriteTrigger trigger, out RewriteState next)
    {
        switch (trigger)
        {
            case RewriteTrigger.Reset:
                next = RewriteState.Idle;
                return true;

            case RewriteTrigger.Cancel:
                if (current is RewriteState.Capturing or RewriteState.Requesting or RewriteState.Streaming or RewriteState.Diffing or RewriteState.Idle)
                {
                    next = RewriteState.Cancelled;
                    return true;
                }
                break;

            case RewriteTrigger.Fail:
                if (current is RewriteState.Capturing or RewriteState.Requesting or RewriteState.Streaming or RewriteState.Diffing or RewriteState.Idle)
                {
                    next = RewriteState.Failed;
                    return true;
                }
                break;

            case RewriteTrigger.StartCapture:
                if (current == RewriteState.Idle)
                {
                    next = RewriteState.Capturing;
                    return true;
                }
                break;

            case RewriteTrigger.CaptureCompleted:
                if (current == RewriteState.Capturing)
                {
                    next = RewriteState.Requesting;
                    return true;
                }
                break;

            case RewriteTrigger.StartRequest:
                if (current is RewriteState.Capturing or RewriteState.Idle)
                {
                    next = RewriteState.Requesting;
                    return true;
                }
                break;

            case RewriteTrigger.ReceiveToken:
                if (current is RewriteState.Requesting or RewriteState.Streaming)
                {
                    next = RewriteState.Streaming;
                    return true;
                }
                break;

            case RewriteTrigger.CompleteStream:
                if (current is RewriteState.Requesting or RewriteState.Streaming)
                {
                    next = RewriteState.Diffing;
                    return true;
                }
                break;

            case RewriteTrigger.Accept:
                if (current == RewriteState.Diffing)
                {
                    next = RewriteState.Accepted;
                    return true;
                }
                break;
        }

        next = current;
        return false;
    }
}
