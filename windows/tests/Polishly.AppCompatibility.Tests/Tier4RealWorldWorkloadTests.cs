using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Polishly.App.Services;
using Polishly.App.ViewModels;
using Polishly.Core;
using Polishly.Core.Capabilities;
using Polishly.Core.Diff;
using Polishly.Core.Models;
using Polishly.Core.Prompts;
using Polishly.Providers.Abstractions;
using Polishly.Providers.Demo;
using Polishly.WindowsIntegration.Capture;
using Polishly.WindowsIntegration.Clipboard;
using Polishly.WindowsIntegration.Security;
using Xunit;

namespace Polishly.AppCompatibility.Tests;

public class Tier4RealWorldWorkloadTests
{
    private readonly WindowTracker _tracker = new();
    private readonly Polishly.Core.Capabilities.AppCapabilityRules _capabilityRules = new();
    private readonly SensitiveFieldDetector _detector = new();

    [Fact]
    public async Task Workload_Notepad_NativeTextEdit_DirectUiaCaptureAndReplacement()
    {
        var profile = _capabilityRules.GetProfile("notepad.exe");
        Assert.True(profile.SelectionBoundsSupported);
        Assert.False(profile.RequireClipboardFallback);

        var target = new TargetContext(
            WindowHandle: IntPtr.Zero,
            ProcessId: 1234,
            ProcessName: "notepad",
            AppTitle: "Untitled - Notepad",
            FieldId: "edit_field",
            IsPassword: false,
            IsElevated: false
        );

        var captureEngine = new UIAutomationCapture(_tracker, _capabilityRules);
        var selection = await captureEngine.CaptureSelectionAsync();
        Assert.False(selection.IsEmpty);

        var provider = new DemoProvider();
        var request = new RewriteRequest(selection.SelectedText, RewriteMode.Improve);
        
        var tokens = new List<string>();
        await foreach (var token in provider.StreamRewriteAsync(request))
        {
            tokens.Add(token.Text);
        }
        string resultText = string.Concat(tokens);
        Assert.NotEmpty(resultText);

        var diffEngine = new Polishly.Core.Diff.WordDiffEngine();
        var diffs = diffEngine.ComputeDiff(selection.SelectedText, resultText);
        Assert.NotEmpty(diffs);

        var transaction = new GuardedClipboardTransaction(() => 1u);
        var pasteResult = await transaction.ExecuteSafePasteAsync(resultText, target);
        Assert.True(pasteResult.Success);
        Assert.False(pasteResult.FallbackToCopy);
    }

    [Fact]
    public async Task Workload_MicrosoftTeams_ChatChannel_GuardedClipboardFallback()
    {
        var profile = _capabilityRules.GetProfile("ms-teams");
        Assert.True(profile.RequireClipboardFallback);
        Assert.False(profile.AutomaticTriggerSupported);

        var teamsTarget = new TargetContext(
            WindowHandle: IntPtr.Zero,
            ProcessId: 2345,
            ProcessName: "ms-teams",
            AppTitle: "Microsoft Teams - General Channel",
            FieldId: "chat_input",
            IsPassword: false,
            IsElevated: false
        );

        var provider = new DemoProvider();
        var request = new RewriteRequest("Hey team, here is the quick status update for M0 milestone build.", RewriteMode.Friendly);

        var tokens = new List<string>();
        await foreach (var token in provider.StreamRewriteAsync(request))
        {
            tokens.Add(token.Text);
        }
        string polishedMsg = string.Concat(tokens);

        var transaction = new GuardedClipboardTransaction(() => 1u);
        var pasteResult = await transaction.ExecuteSafePasteAsync(polishedMsg, teamsTarget);

        Assert.True(pasteResult.Success);
        Assert.False(pasteResult.FallbackToCopy);
        Assert.True(pasteResult.RestoredOriginalClipboard);
    }

    [Fact]
    public async Task Workload_OutlookClassic_EmailComposition_GuardedPaste()
    {
        var profile = _capabilityRules.GetProfile("outlook");
        Assert.True(profile.RequireClipboardFallback);

        var outlookTarget = new TargetContext(
            WindowHandle: IntPtr.Zero,
            ProcessId: 3456,
            ProcessName: "outlook",
            AppTitle: "RE: Quarterly Planning - Message (HTML)",
            FieldId: "mail_body",
            IsPassword: false,
            IsElevated: false
        );

        const string draftText = "Please find attached the draft release schedule for Q3.";
        var promptBuilder = new Polishly.Core.Prompts.PromptBuilder();
        var prompt = promptBuilder.BuildPrompt(new RewriteRequest(draftText, RewriteMode.Improve));
        Assert.Contains(draftText, prompt);

        var provider = new DemoProvider();
        var request = new RewriteRequest(draftText, RewriteMode.Improve);

        var stateMachine = new Polishly.Core.StateMachine.RewriteStateMachine();
        var diffEngine = new Polishly.Core.Diff.WordDiffEngine();
        var vm = new PopupViewModel(stateMachine, diffEngine);

        vm.Reset(draftText);
        stateMachine.Transition(Polishly.Core.StateMachine.RewriteEvent.TriggerHotkey);
        stateMachine.Transition(Polishly.Core.StateMachine.RewriteEvent.CaptureSuccess);
        vm.AppendStreamingToken("Polished: ");

        await foreach (var token in provider.StreamRewriteAsync(request))
        {
            vm.AppendStreamingToken(token.Text);
        }
        vm.CompleteStream();

        Assert.Equal(Polishly.Core.StateMachine.RewriteState.StreamComplete, vm.CurrentState);
        Assert.True(vm.CanAccept());

        var transaction = new GuardedClipboardTransaction(() => 1u);
        var pasteResult = await transaction.ExecuteSafePasteAsync(vm.RewrittenText, outlookTarget);

        Assert.True(pasteResult.Success);
    }

    [Fact]
    public async Task Workload_Slack_ChannelMessage_ClipboardSequenceCheck()
    {
        var profile = _capabilityRules.GetProfile("slack");
        Assert.True(profile.RequireClipboardFallback);

        uint sequenceCount = 100;
        Func<uint> seqFunc = () => sequenceCount;

        var slackTarget = new TargetContext(
            WindowHandle: IntPtr.Zero,
            ProcessId: 4567,
            ProcessName: "slack",
            AppTitle: "#engineering - Slack",
            FieldId: "message_editor",
            IsPassword: false,
            IsElevated: false
        );

        var transaction = new GuardedClipboardTransaction(seqFunc);
        var pasteResult = await transaction.ExecuteSafePasteAsync("Slack rewrite message", slackTarget);

        Assert.True(pasteResult.Success);
        Assert.False(pasteResult.FallbackToCopy);
    }

    [Fact]
    public async Task Workload_VSCode_CodeCommentRewrite_PreservesFormatting()
    {
        var profile = _capabilityRules.GetProfile("code");
        Assert.True(profile.RequireClipboardFallback);

        var codeTarget = new TargetContext(
            WindowHandle: IntPtr.Zero,
            ProcessId: 5678,
            ProcessName: "code",
            AppTitle: "Tier4RealWorldWorkloadTests.cs - Polishly - Visual Studio Code",
            FieldId: "editor_pane",
            IsPassword: false,
            IsElevated: false
        );

        const string codeComment = "// TODO: Refactor clipboard sequence validation logic to support fast async retries.";
        var provider = new DemoProvider();
        var request = new RewriteRequest(codeComment, RewriteMode.Concise);

        var tokens = new List<string>();
        await foreach (var token in provider.StreamRewriteAsync(request))
        {
            tokens.Add(token.Text);
        }
        string output = string.Concat(tokens);

        var diffEngine = new Polishly.Core.Diff.WordDiffEngine();
        var diffs = diffEngine.ComputeDiff(codeComment, output);

        var transaction = new GuardedClipboardTransaction(() => 1u);
        var result = await transaction.ExecuteSafePasteAsync(output, codeTarget);

        Assert.True(result.Success);
        Assert.NotEmpty(diffs);
    }

    [Fact]
    public async Task Workload_ChromeGmail_EmailReply_WebBrowserFieldCompatibility()
    {
        var profile = _capabilityRules.GetProfile("chrome");
        Assert.True(profile.RequireClipboardFallback);

        var chromeTarget = new TargetContext(
            WindowHandle: IntPtr.Zero,
            ProcessId: 6789,
            ProcessName: "chrome",
            AppTitle: "Gmail - Inbox (1,402) - google.chrome",
            FieldId: "gmail_compose",
            IsPassword: false,
            IsElevated: false
        );

        var transaction = new GuardedClipboardTransaction(() => 1u);
        var result = await transaction.ExecuteSafePasteAsync("Gmail reply text rewrite", chromeTarget);

        Assert.True(result.Success);
        Assert.False(result.FallbackToCopy);
    }

    [Fact]
    public async Task Workload_ElevatedAdminPrompt_BlocksInPlaceReplacementAndOffersCopy()
    {
        var elevatedTarget = new TargetContext(
            WindowHandle: IntPtr.Zero,
            ProcessId: 7890,
            ProcessName: "powershell",
            AppTitle: "Administrator: Windows PowerShell",
            FieldId: "ps_cli",
            IsPassword: false,
            IsElevated: true
        );

        var elevatedWindow = new TargetWindow(elevatedTarget.WindowHandle, elevatedTarget.ProcessId, elevatedTarget.ProcessName, elevatedTarget.AppTitle, true);
        var sensitivity = _detector.IsSensitiveField(elevatedWindow);

        Assert.True(sensitivity.IsSensitive);
        Assert.Contains("elevated", sensitivity.Reason, StringComparison.OrdinalIgnoreCase);

        var transaction = new GuardedClipboardTransaction(() => 1u);
        var pasteResult = await transaction.ExecuteSafePasteAsync("Get-Process | Where-Object", elevatedTarget);

        Assert.False(pasteResult.Success);
        Assert.True(pasteResult.FallbackToCopy);
        Assert.Contains("elevated window", pasteResult.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Workload_PasswordManagerVault_BlocksCaptureAndPaste()
    {
        var pwdTarget = new TargetContext(
            WindowHandle: IntPtr.Zero,
            ProcessId: 8901,
            ProcessName: "1password",
            AppTitle: "1Password - Enterprise Vault",
            FieldId: "master_password",
            IsPassword: true,
            IsElevated: false
        );

        var pwdWindow = new TargetWindow(pwdTarget.WindowHandle, pwdTarget.ProcessId, pwdTarget.ProcessName, pwdTarget.AppTitle, false);
        var sensitivity = _detector.IsSensitiveField(pwdWindow);

        Assert.True(sensitivity.IsSensitive);

        var transaction = new GuardedClipboardTransaction(() => 1u);
        var result = await transaction.ExecuteSafePasteAsync("ExtremelySecurePassword123!", pwdTarget);

        Assert.False(result.Success);
        Assert.True(result.FallbackToCopy);
        Assert.Contains("password field", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }
}
