using Xunit;

namespace Polishly.Core.Tests;

public class RewriteStateMachineTests
{
    [Fact]
    public void FullLifecycle_HappyPath_TransitionsCorrectly()
    {
        var sm = new RewriteStateMachine();
        var eventLog = new List<StateChangedEventArgs>();
        sm.StateChanged += (_, e) => eventLog.Add(e);

        Assert.Equal(RewriteState.Idle, sm.CurrentState);

        Assert.True(sm.CanFire(RewriteTrigger.StartCapture));
        Assert.True(sm.Fire(RewriteTrigger.StartCapture));
        Assert.Equal(RewriteState.Capturing, sm.CurrentState);

        Assert.True(sm.CanFire(RewriteTrigger.CaptureCompleted));
        Assert.True(sm.Fire(RewriteTrigger.CaptureCompleted));
        Assert.Equal(RewriteState.Requesting, sm.CurrentState);

        Assert.True(sm.CanFire(RewriteTrigger.ReceiveToken));
        Assert.True(sm.Fire(RewriteTrigger.ReceiveToken));
        Assert.Equal(RewriteState.Streaming, sm.CurrentState);

        Assert.True(sm.CanFire(RewriteTrigger.ReceiveToken));
        Assert.True(sm.Fire(RewriteTrigger.ReceiveToken));
        Assert.Equal(RewriteState.Streaming, sm.CurrentState);

        Assert.True(sm.CanFire(RewriteTrigger.CompleteStream));
        Assert.True(sm.Fire(RewriteTrigger.CompleteStream));
        Assert.Equal(RewriteState.Diffing, sm.CurrentState);

        Assert.True(sm.CanFire(RewriteTrigger.Accept));
        Assert.True(sm.Fire(RewriteTrigger.Accept));
        Assert.Equal(RewriteState.Accepted, sm.CurrentState);

        sm.Reset();
        Assert.Equal(RewriteState.Idle, sm.CurrentState);

        Assert.Equal(6, eventLog.Count);
        Assert.Equal(RewriteState.Idle, eventLog[0].OldState);
        Assert.Equal(RewriteState.Capturing, eventLog[0].NewState);
        Assert.Equal(RewriteState.Accepted, eventLog[4].NewState);
        Assert.Equal(RewriteState.Idle, eventLog[5].NewState);
    }

    [Theory]
    [InlineData(RewriteState.Capturing)]
    [InlineData(RewriteState.Requesting)]
    [InlineData(RewriteState.Streaming)]
    [InlineData(RewriteState.Diffing)]
    public void Cancel_FromActiveState_TransitionsToCancelled(RewriteState initialState)
    {
        var sm = CreateMachineAtState(initialState);
        Assert.True(sm.CanFire(RewriteTrigger.Cancel));
        Assert.True(sm.Fire(RewriteTrigger.Cancel));
        Assert.Equal(RewriteState.Cancelled, sm.CurrentState);

        sm.Reset();
        Assert.Equal(RewriteState.Idle, sm.CurrentState);
    }

    [Theory]
    [InlineData(RewriteState.Capturing)]
    [InlineData(RewriteState.Requesting)]
    [InlineData(RewriteState.Streaming)]
    [InlineData(RewriteState.Diffing)]
    public void Fail_FromActiveState_CapturesErrorDetails(RewriteState initialState)
    {
        var sm = CreateMachineAtState(initialState);
        const string errorMessage = "Network Connection Timeout";

        Assert.True(sm.CanFire(RewriteTrigger.Fail));
        Assert.True(sm.Fire(RewriteTrigger.Fail, errorMessage));
        Assert.Equal(RewriteState.Failed, sm.CurrentState);
        Assert.Equal(errorMessage, sm.LastError);

        sm.Reset();
        Assert.Equal(RewriteState.Idle, sm.CurrentState);
        Assert.Null(sm.LastError);
    }

    [Fact]
    public void InvalidTransition_FromIdle_ReturnsFalseAndPreservesState()
    {
        var sm = new RewriteStateMachine();
        Assert.False(sm.CanFire(RewriteTrigger.Accept));
        Assert.False(sm.CanFire(RewriteTrigger.CompleteStream));
        Assert.False(sm.CanFire(RewriteTrigger.ReceiveToken));

        var fired = sm.Fire(RewriteTrigger.Accept);
        Assert.False(fired);
        Assert.Equal(RewriteState.Idle, sm.CurrentState);
    }

    [Fact]
    public void ThreadSafety_ConcurrentTransitions_MaintainsConsistentState()
    {
        var sm = new RewriteStateMachine();
        var tasks = new List<Task>();

        for (int i = 0; i < 50; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                sm.Fire(RewriteTrigger.StartCapture);
                sm.Fire(RewriteTrigger.StartRequest);
                sm.Fire(RewriteTrigger.ReceiveToken);
                sm.Fire(RewriteTrigger.CompleteStream);
                sm.Fire(RewriteTrigger.Accept);
                sm.Reset();
            }));
        }

        Task.WaitAll(tasks.ToArray());
        Assert.True(sm.CurrentState is RewriteState.Idle or RewriteState.Capturing or RewriteState.Requesting or RewriteState.Streaming or RewriteState.Diffing or RewriteState.Accepted);
    }

    private static RewriteStateMachine CreateMachineAtState(RewriteState state)
    {
        var sm = new RewriteStateMachine();
        if (state == RewriteState.Idle) return sm;

        sm.Fire(RewriteTrigger.StartCapture);
        if (state == RewriteState.Capturing) return sm;

        sm.Fire(RewriteTrigger.CaptureCompleted);
        if (state == RewriteState.Requesting) return sm;

        sm.Fire(RewriteTrigger.ReceiveToken);
        if (state == RewriteState.Streaming) return sm;

        sm.Fire(RewriteTrigger.CompleteStream);
        if (state == RewriteState.Diffing) return sm;

        return sm;
    }
}
