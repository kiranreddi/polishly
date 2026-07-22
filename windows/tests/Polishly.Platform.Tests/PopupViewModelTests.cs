using System;
using Xunit;
using Polishly.App.ViewModels;
using Polishly.Core.Diff;
using Polishly.Core.StateMachine;

namespace Polishly.Platform.Tests;

public class PopupViewModelTests
{
    [Fact]
    public void PopupViewModel_Initialization_DefaultValuesAreSet()
    {
        var stateMachine = new RewriteStateMachine();
        var diffEngine = new WordDiffEngine();
        var vm = new PopupViewModel(stateMachine, diffEngine);

        Assert.Equal(RewriteState.Idle, vm.CurrentState);
        Assert.Equal(string.Empty, vm.OriginalText);
        Assert.Equal(string.Empty, vm.RewrittenText);
        Assert.Empty(vm.DiffSegments);
        Assert.False(vm.HasError);
        Assert.False(vm.IsVisible);
    }

    [Fact]
    public void PopupViewModel_AppendStreamingToken_UpdatesTextAndDiff()
    {
        var stateMachine = new RewriteStateMachine();
        var diffEngine = new WordDiffEngine();
        var vm = new PopupViewModel(stateMachine, diffEngine);

        vm.Reset("hello world");
        stateMachine.Transition(RewriteEvent.TriggerHotkey); // Capturing
        stateMachine.Transition(RewriteEvent.CaptureSuccess); // StreamReady

        vm.AppendStreamingToken("hello ");
        vm.AppendStreamingToken("there ");
        vm.AppendStreamingToken("world");

        Assert.Equal("hello there world", vm.RewrittenText);
        Assert.NotEmpty(vm.DiffSegments);
        Assert.Contains(vm.DiffSegments, seg => seg.Type == DiffType.Addition && seg.Text.Contains("there"));
    }

    [Fact]
    public void PopupViewModel_StateTransitions_UpdatesStateDescriptionAndCommands()
    {
        var stateMachine = new RewriteStateMachine();
        var diffEngine = new WordDiffEngine();
        var vm = new PopupViewModel(stateMachine, diffEngine);

        vm.Reset("test");
        stateMachine.Transition(RewriteEvent.TriggerHotkey);
        stateMachine.Transition(RewriteEvent.CaptureSuccess);
        vm.AppendStreamingToken("test rewritten");
        stateMachine.Transition(RewriteEvent.StreamFinished);

        Assert.Equal(RewriteState.StreamComplete, vm.CurrentState);
        Assert.Equal("Review changes", vm.StateDescription);
        Assert.True(vm.CanAccept());
        Assert.True(vm.CanCopy());
    }

    [Fact]
    public void PopupViewModel_AcceptCommand_TransitionsStateAndRaisesEvent()
    {
        var stateMachine = new RewriteStateMachine();
        var diffEngine = new WordDiffEngine();
        var vm = new PopupViewModel(stateMachine, diffEngine);

        string? pastedText = null;
        vm.RequestPaste += (s, text) => pastedText = text;

        vm.Reset("apple");
        stateMachine.Transition(RewriteEvent.TriggerHotkey);
        stateMachine.Transition(RewriteEvent.CaptureSuccess);
        stateMachine.Transition(RewriteEvent.StartStreaming);
        vm.AppendStreamingToken("banana");
        stateMachine.Transition(RewriteEvent.StreamFinished);

        vm.Accept();

        Assert.Equal(RewriteState.Replacing, stateMachine.CurrentState);
        Assert.Equal("banana", pastedText);
        Assert.False(vm.IsVisible);
    }

    [Fact]
    public void PopupViewModel_CopyCommand_TransitionsStateAndRaisesEvent()
    {
        var stateMachine = new RewriteStateMachine();
        var diffEngine = new WordDiffEngine();
        var vm = new PopupViewModel(stateMachine, diffEngine);

        string? copiedText = null;
        vm.RequestCopy += (s, text) => copiedText = text;

        vm.Reset("cat");
        stateMachine.Transition(RewriteEvent.TriggerHotkey);
        stateMachine.Transition(RewriteEvent.CaptureSuccess);
        stateMachine.Transition(RewriteEvent.StartStreaming);
        vm.AppendStreamingToken("dog");
        stateMachine.Transition(RewriteEvent.StreamFinished);

        vm.Copy();

        Assert.Equal(RewriteState.Copied, stateMachine.CurrentState);
        Assert.Equal("dog", copiedText);
    }

    [Fact]
    public void PopupViewModel_DismissAndEscape_TransitionsToCancelled()
    {
        var stateMachine = new RewriteStateMachine();
        var diffEngine = new WordDiffEngine();
        var vm = new PopupViewModel(stateMachine, diffEngine);

        vm.IsVisible = true;
        vm.HandleEscape();

        Assert.Equal(RewriteState.Cancelled, stateMachine.CurrentState);
        Assert.False(vm.IsVisible);
    }

    [Fact]
    public void PopupViewModel_ClickOutside_DismissesPopup()
    {
        var stateMachine = new RewriteStateMachine();
        var diffEngine = new WordDiffEngine();
        var vm = new PopupViewModel(stateMachine, diffEngine);

        vm.IsVisible = true;
        vm.HandleClickOutside();

        Assert.Equal(RewriteState.Cancelled, stateMachine.CurrentState);
        Assert.False(vm.IsVisible);
    }
}
