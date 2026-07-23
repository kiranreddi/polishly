using System.Diagnostics;
using Polishly.Core.Capabilities;
using Polishly.Core.Diff;
using Polishly.Core.Models;
using Polishly.Core.Prompts;
using Polishly.Core.StateMachine;
using Xunit;
using Xunit.Abstractions;

namespace Polishly.Core.Tests;

public class StressAndBoundaryTests
{
    private readonly ITestOutputHelper _output;

    public StressAndBoundaryTests(ITestOutputHelper output)
    {
        _output = output;
    }

    #region WordDiffEngine Stress & Boundary Tests

    [Fact]
    public void WordDiffEngine_ReconstructionInvariant_HoldsForComplexInterleavedText()
    {
        var engine = new Diff.WordDiffEngine();
        string original = "The quick brown fox jumps over the lazy dog near the river bank.";
        string revised = "A fast brown fox leaped over a sleepy hound near the river bank!";

        var diff = engine.ComputeDiff(original, revised);

        string reconstructedOriginal = string.Concat(diff.Where(d => d.Type != DiffType.Addition).Select(d => d.Text));
        string reconstructedRevised = string.Concat(diff.Where(d => d.Type != DiffType.Deletion).Select(d => d.Text));

        _output.WriteLine($"Diff count: {diff.Count}");
        Assert.Equal(original, reconstructedOriginal);
        Assert.Equal(revised, reconstructedRevised);
    }

    [Fact]
    public void WordDiffEngine_RepeatingTokens_ReconstructionInvariant()
    {
        var engine = new Diff.WordDiffEngine();
        string original = "a a a a a";
        string revised = "a a a";

        var diff = engine.ComputeDiff(original, revised);

        string reconstructedOriginal = string.Concat(diff.Where(d => d.Type != DiffType.Addition).Select(d => d.Text));
        string reconstructedRevised = string.Concat(diff.Where(d => d.Type != DiffType.Deletion).Select(d => d.Text));

        Assert.Equal(original, reconstructedOriginal);
        Assert.Equal(revised, reconstructedRevised);
    }

    [Fact]
    public void WordDiffEngine_RootEngine_ReconstructionInvariant_RepeatingTokens()
    {
        var engine = new Polishly.Core.WordDiffEngine();
        string original = "a a a a a";
        string revised = "a a a";

        var diff = engine.ComputeDiff(original, revised);

        string reconstructedOriginal = string.Concat(diff.Where(d => d.Type != DiffSegmentType.Added).Select(d => d.Text));
        string reconstructedRevised = string.Concat(diff.Where(d => d.Type != DiffSegmentType.Deleted).Select(d => d.Text));

        Assert.Equal(original, reconstructedOriginal);
        Assert.Equal(revised, reconstructedRevised);
    }

    [Fact]
    public void WordDiffEngine_Performance_InterleavedLargeText_MeasuresTimeAndMemory()
    {
        var engine = new Diff.WordDiffEngine();
        // Create 2000 tokens with interleaved modifications so LCS cannot be easily trimmed
        var oldWords = Enumerable.Range(1, 1500).Select(i => $"word{i}").ToList();
        var newWords = Enumerable.Range(1, 1500).Select(i => i % 2 == 0 ? $"mod{i}" : $"word{i}").ToList();

        string original = string.Join(" ", oldWords);
        string revised = string.Join(" ", newWords);

        var sw = Stopwatch.StartNew();
        long memBefore = GC.GetTotalMemory(true);
        var diff = engine.ComputeDiff(original, revised);
        sw.Stop();
        long memAfter = GC.GetTotalMemory(false);

        _output.WriteLine($"Diff computed in {sw.ElapsedMilliseconds} ms. Memory delta: {(memAfter - memBefore) / 1024} KB. Segments: {diff.Count}");

        string reconstructedOriginal = string.Concat(diff.Where(d => d.Type != DiffType.Addition).Select(d => d.Text));
        string reconstructedRevised = string.Concat(diff.Where(d => d.Type != DiffType.Deletion).Select(d => d.Text));

        Assert.Equal(original, reconstructedOriginal);
        Assert.Equal(revised, reconstructedRevised);
    }

    [Fact]
    public void WordDiffEngine_RootEngine_Performance_InterleavedText()
    {
        var engine = new Polishly.Core.WordDiffEngine();
        var oldWords = Enumerable.Range(1, 1000).Select(i => $"word{i}").ToList();
        var newWords = Enumerable.Range(1, 1000).Select(i => i % 2 == 0 ? $"mod{i}" : $"word{i}").ToList();

        string original = string.Join(" ", oldWords);
        string revised = string.Join(" ", newWords);

        var sw = Stopwatch.StartNew();
        var diff = engine.ComputeDiff(original, revised);
        sw.Stop();

        _output.WriteLine($"Root WordDiffEngine computed 1000 interleaved tokens in {sw.ElapsedMilliseconds} ms");

        string reconstructedOriginal = string.Concat(diff.Where(d => d.Type != DiffSegmentType.Added).Select(d => d.Text));
        string reconstructedRevised = string.Concat(diff.Where(d => d.Type != DiffSegmentType.Deleted).Select(d => d.Text));

        Assert.Equal(original, reconstructedOriginal);
        Assert.Equal(revised, reconstructedRevised);
    }

    #endregion

    #region RewriteStateMachine Stress & Invariant Tests

    [Fact]
    public void StateMachine_InvalidTriggers_PreserveStateAndDoNotThrow()
    {
        var sm = new Polishly.Core.RewriteStateMachine();
        Assert.Equal(Polishly.Core.RewriteState.Idle, sm.CurrentState);

        // Invalid triggers from Idle
        Assert.False(sm.Fire(Polishly.Core.RewriteTrigger.Accept));
        Assert.Equal(Polishly.Core.RewriteState.Idle, sm.CurrentState);

        Assert.False(sm.Fire(Polishly.Core.RewriteTrigger.CompleteStream));
        Assert.Equal(Polishly.Core.RewriteState.Idle, sm.CurrentState);

        Assert.False(sm.Fire(Polishly.Core.RewriteTrigger.ReceiveToken));
        Assert.Equal(Polishly.Core.RewriteState.Idle, sm.CurrentState);

        Assert.False(sm.Fire(Polishly.Core.RewriteTrigger.CaptureCompleted));
        Assert.Equal(Polishly.Core.RewriteState.Idle, sm.CurrentState);
    }

    [Fact]
    public void StateMachine_SubNamespace_InvalidTriggers_PreserveState()
    {
        var sm = new Polishly.Core.StateMachine.RewriteStateMachine();
        Assert.Equal(Polishly.Core.StateMachine.RewriteState.Idle, sm.CurrentState);

        sm.Transition(RewriteEvent.StartStreaming);
        Assert.Equal(Polishly.Core.StateMachine.RewriteState.Idle, sm.CurrentState);

        sm.Transition(RewriteEvent.Accept);
        Assert.Equal(Polishly.Core.StateMachine.RewriteState.Idle, sm.CurrentState);

        sm.Transition(RewriteEvent.ReceiveToken);
        Assert.Equal(Polishly.Core.StateMachine.RewriteState.Idle, sm.CurrentState);
    }

    [Fact]
    public void StateMachine_ConcurrentRandomTriggers_MaintainsValidState()
    {
        var sm = new Polishly.Core.RewriteStateMachine();
        var triggers = Enum.GetValues<Polishly.Core.RewriteTrigger>();
        var tasks = new List<Task>();
        int errors = 0;

        for (int t = 0; t < 10; t++)
        {
            tasks.Add(Task.Run(() =>
            {
                var rand = new Random(Task.CurrentId ?? 0);
                for (int i = 0; i < 500; i++)
                {
                    try
                    {
                        var trig = triggers[rand.Next(triggers.Length)];
                        sm.Fire(trig, "test payload");
                    }
                    catch
                    {
                        Interlocked.Increment(ref errors);
                    }
                }
            }));
        }

        Task.WaitAll(tasks.ToArray());

        Assert.Equal(0, errors);
        Assert.True(Enum.IsDefined(sm.CurrentState));
    }

    #endregion

    #region PromptBuilder Edge Cases

    [Fact]
    public void PromptBuilder_CustomMode_NullOrEmptyInstruction_OutputVerification()
    {
        var builderSub = new Prompts.PromptBuilder();
        var reqNull = new RewriteRequest("Test input", RewriteMode.Custom, CustomInstruction: null);
        string promptNull = builderSub.BuildPrompt(reqNull);
        Assert.Contains(PromptFixture.ImproveDirective, promptNull);

        var reqEmpty = new RewriteRequest("Test input", RewriteMode.Custom, CustomInstruction: "");
        string promptEmpty = builderSub.BuildPrompt(reqEmpty);
        _output.WriteLine($"Prompt for Custom mode with empty instruction: '{promptEmpty}'");

        var promptRootSys = Polishly.Core.PromptBuilder.BuildSystemPrompt(RewriteMode.Custom, "");
        var promptRootUser = Polishly.Core.PromptBuilder.BuildUserPrompt("Test input", RewriteMode.Custom, "");
        _output.WriteLine($"Root System Prompt Custom empty: '{promptRootSys}'");
        _output.WriteLine($"Root User Prompt Custom empty: '{promptRootUser}'");
    }

    [Fact]
    public void PromptBuilder_UndefinedEnumMode_HandledSafely()
    {
        var invalidMode = (RewriteMode)999;
        var sysPrompt = Polishly.Core.PromptBuilder.BuildSystemPrompt(invalidMode);
        var userPrompt = Polishly.Core.PromptBuilder.BuildUserPrompt("Input text", invalidMode);

        Assert.NotNull(sysPrompt);
        Assert.NotNull(userPrompt);
    }

    #endregion

    #region AppCapabilityRules Path Normalization & Coverage

    [Fact]
    public void AppCapabilityRules_FullPathProcessNames_NormalizationBehavior()
    {
        var rulesRoot = new Polishly.Core.AppCapabilityRules();
        var profilePath = rulesRoot.GetProfile(@"C:\Windows\System32\notepad.exe");

        _output.WriteLine($"Profile for C:\\Windows\\System32\\notepad.exe: ProcessName='{profilePath.ProcessName}', Category='{profilePath.TargetCategory}'");

        var normalizedPath = Polishly.Core.AppCapabilityRules.NormalizeProcessName(@"C:\Windows\System32\notepad.exe");
        _output.WriteLine($"NormalizeProcessName('C:\\Windows\\System32\\notepad.exe') => '{normalizedPath}'");
    }

    [Fact]
    public void AppCapabilityRules_CheckBrowserAndElectronCoverage()
    {
        var rulesRoot = new Polishly.Core.AppCapabilityRules();
        var rulesSub = new Polishly.Core.Capabilities.AppCapabilityRules();

        string[] testApps = new[] { "notepad", "teams", "ms-teams", "slack", "code", "chrome", "msedge", "firefox", "brave", "discord", "onenote" };

        foreach (var app in testApps)
        {
            var rootProf = rulesRoot.GetProfile(app);
            var subProf = rulesSub.GetProfile(app);
            _output.WriteLine($"App '{app}': RootCategory={rootProf.TargetCategory}, SubProcessName={subProf.ProcessName}");
        }
    }

    #endregion
}
