namespace Polishly.Core;

public class StateChangedEventArgs : EventArgs
{
    public RewriteState OldState { get; }
    public RewriteState NewState { get; }
    public RewriteTrigger Trigger { get; }
    public string? ErrorDetails { get; }

    public StateChangedEventArgs(RewriteState oldState, RewriteState newState, RewriteTrigger trigger, string? errorDetails = null)
    {
        OldState = oldState;
        NewState = newState;
        Trigger = trigger;
        ErrorDetails = errorDetails;
    }
}
