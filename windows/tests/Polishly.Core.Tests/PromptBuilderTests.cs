using Polishly.Core.Models;
using Xunit;

namespace Polishly.Core.Tests;

public class PromptBuilderTests
{
    [Theory]
    [InlineData(RewriteMode.Improve, "improve")]
    [InlineData(RewriteMode.Concise, "concise")]
    [InlineData(RewriteMode.Friendly, "friendly")]
    [InlineData(RewriteMode.Expand, "expand")]
    [InlineData(RewriteMode.Custom, "custom")]
    public void BuildSystemPrompt_AllModes_ContainsExpectedKeywords(RewriteMode mode, string expectedSubstring)
    {
        var prompt = PromptBuilder.BuildSystemPrompt(mode);
        Assert.NotNull(prompt);
        Assert.NotEmpty(prompt);
        Assert.Contains(expectedSubstring, prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ONLY the rewritten text", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_WithCustomInstruction_AppendsInstruction()
    {
        const string customInstr = "Use formal academic tone";
        var prompt = PromptBuilder.BuildSystemPrompt(RewriteMode.Improve, customInstr);

        Assert.Contains(customInstr, prompt);
    }

    [Fact]
    public void BuildUserPrompt_StandardMode_IncludesModeAndText()
    {
        const string selectedText = "The quick brown fox jumps over the lazy dog.";
        var prompt = PromptBuilder.BuildUserPrompt(selectedText, RewriteMode.Improve);

        Assert.Contains("Improve", prompt);
        Assert.Contains(selectedText, prompt);
    }

    [Fact]
    public void BuildUserPrompt_CustomMode_IncludesCustomInstruction()
    {
        const string selectedText = "Here is some raw draft text.";
        const string instruction = "Make it sound like Shakespeare";

        var prompt = PromptBuilder.BuildUserPrompt(selectedText, RewriteMode.Custom, instruction);

        Assert.Contains(instruction, prompt);
        Assert.Contains(selectedText, prompt);
    }

    [Fact]
    public void BuildUserPrompt_NullSelectedText_HandlesGracefully()
    {
        var prompt = PromptBuilder.BuildUserPrompt(null!, RewriteMode.Concise);

        Assert.NotNull(prompt);
        Assert.Contains("Concise", prompt);
    }
}
