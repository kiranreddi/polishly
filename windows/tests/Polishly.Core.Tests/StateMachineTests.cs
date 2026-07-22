using System.Collections.Generic;
using Polishly.Core;
using Xunit;

namespace Polishly.Core.Tests;

public class StateMachineTests
{
    [Fact]
    public void StateMachine_InitialState_IsIdle()
    {
        var sm = new RewriteStateMachine();
        Assert.Equal(RewriteState.Idle, sm.CurrentState);
    }

    [Fact]
    public void StateMachine_FullSuccessfulFlow_TransitionsCorrectly()
    {
        var sm = new RewriteStateMachine();
        var stateHistory = new List<RewriteState>();
        sm.StateChanged += (_, e) => stateHistory.Add(e.NewState);

        sm.Fire(RewriteTrigger.StartCapture);
        Assert.Equal(RewriteState.Capturing, sm.CurrentState);

        sm.Fire(RewriteTrigger.CaptureCompleted);
        Assert.Equal(RewriteState.Requesting, sm.CurrentState);

        sm.Fire(RewriteTrigger.ReceiveToken);
        Assert.Equal(RewriteState.Streaming, sm.CurrentState);

        sm.Fire(RewriteTrigger.ReceiveToken);
        Assert.Equal(RewriteState.Streaming, sm.CurrentState);

        sm.Fire(RewriteTrigger.CompleteStream);
        Assert.Equal(RewriteState.Diffing, sm.CurrentState);

        sm.Fire(RewriteTrigger.Accept);
        Assert.Equal(RewriteState.Accepted, sm.CurrentState);

        Assert.Equal(5, stateHistory.Count);
        Assert.Equal(RewriteState.Capturing, stateHistory[0]);
        Assert.Equal(RewriteState.Requesting, stateHistory[1]);
        Assert.Equal(RewriteState.Streaming, stateHistory[2]);
        Assert.Equal(RewriteState.Diffing, stateHistory[3]);
        Assert.Equal(RewriteState.Accepted, stateHistory[4]);
    }

    [Fact]
    public void StateMachine_CaptureFailure_TransitionsToFailedState()
    {
        var sm = new RewriteStateMachine();
        sm.Fire(RewriteTrigger.StartCapture);
        sm.Fire(RewriteTrigger.Fail, "Capture failed");

        Assert.Equal(RewriteState.Failed, sm.CurrentState);
    }

    [Fact]
    public void StateMachine_ResetFromFailed_TransitionsToIdleState()
    {
        var sm = new RewriteStateMachine();
        sm.Fire(RewriteTrigger.StartCapture);
        sm.Fire(RewriteTrigger.Fail, "Error");
        sm.Reset();

        Assert.Equal(RewriteState.Idle, sm.CurrentState);
    }
}

