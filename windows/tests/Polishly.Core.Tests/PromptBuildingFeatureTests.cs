using Polishly.Core.Models;
using Polishly.Core.Prompts;
using Xunit;

namespace Polishly.Core.Tests;

public class PromptBuildingFeatureTests
{
    private readonly Polishly.Core.Prompts.PromptBuilder _builder = new();

    [Fact]
    public void BuildPrompt_ImproveMode_IncludesImproveDirectiveAndSystemPrompt()
    {
        var request = new RewriteRequest("Please fix this grammar.", RewriteMode.Improve);
        string prompt = _builder.BuildPrompt(request);

        Assert.Contains(PromptFixture.SystemInstruction, prompt);
        Assert.Contains(PromptFixture.ImproveDirective, prompt);
        Assert.Contains("Please fix this grammar.", prompt);
    }

    [Fact]
    public void BuildPrompt_ConciseMode_IncludesConciseDirective()
    {
        var request = new RewriteRequest("This is a very long and wordy sentence.", RewriteMode.Concise);
        string prompt = _builder.BuildPrompt(request);

        Assert.Contains(PromptFixture.ConciseDirective, prompt);
        Assert.Contains("This is a very long and wordy sentence.", prompt);
    }

    [Fact]
    public void BuildPrompt_FriendlyMode_IncludesFriendlyDirective()
    {
        var request = new RewriteRequest("Send me the report now.", RewriteMode.Friendly);
        string prompt = _builder.BuildPrompt(request);

        Assert.Contains(PromptFixture.FriendlyDirective, prompt);
        Assert.Contains("Send me the report now.", prompt);
    }

    [Fact]
    public void BuildPrompt_ExpandMode_IncludesExpandDirective()
    {
        var request = new RewriteRequest("Short point.", RewriteMode.Expand);
        string prompt = _builder.BuildPrompt(request);

        Assert.Contains(PromptFixture.ExpandDirective, prompt);
        Assert.Contains("Short point.", prompt);
    }

    [Theory]
    [InlineData("Make it sound professional", "Make it sound professional")]
    [InlineData("Translate to German", "Translate to German")]
    public void BuildPrompt_CustomMode_UsesCustomInstruction(string customInst, string expectedSubstring)
    {
        var request = new RewriteRequest("Original text here", RewriteMode.Custom, CustomInstruction: customInst);
        string prompt = _builder.BuildPrompt(request);

        Assert.Contains(expectedSubstring, prompt);
        Assert.Contains("Original text here", prompt);
    }

    [Fact]
    public void BuildPrompt_CustomModeWithNullInstruction_FallsBackToDefaultDirective()
    {
        var request = new RewriteRequest("Original text here", RewriteMode.Custom, CustomInstruction: null);
        string prompt = _builder.BuildPrompt(request);

        Assert.Contains(PromptFixture.ImproveDirective, prompt);
        Assert.Contains("Original text here", prompt);
    }
}
