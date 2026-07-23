using Polishly.WindowsIntegration.Security;
using Xunit;

namespace Polishly.Platform.Tests;

public class RedactedLoggerTests
{
    [Theory]
    [InlineData("OpenAI key sk-proj-1234567890abcdef1234567890 in request", "sk-proj-1234567890abcdef1234567890")]
    [InlineData("Anthropic key sk-ant-1234567890abcdef1234567890 in header", "sk-ant-1234567890abcdef1234567890")]
    [InlineData("Groq key gsk_1234567890abcdef1234567890 in header", "gsk_1234567890abcdef1234567890")]
    [InlineData("Cerebras key csk-1234567890abcdef1234567890 in header", "csk-1234567890abcdef1234567890")]
    [InlineData("Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9", "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9")]
    [InlineData("x-api-key: my-super-secret-api-key-value", "my-super-secret-api-key-value")]
    public void Redact_SanitizesApiKeysAndTokens(string input, string rawKeyToEnsureRedacted)
    {
        var logger = new RedactedLogger();
        string redacted = logger.Redact(input);

        Assert.DoesNotContain(rawKeyToEnsureRedacted, redacted);
        Assert.True(redacted.Contains("[REDACTED_API_KEY]") || redacted.Contains("[REDACTED_TOKEN]") || redacted.Contains("[REDACTED_KEY]"));
    }

    [Fact]
    public void RedactedLogger_LogUserSelection_RedactsTextAndZeroLeaks()
    {
        var logger = new RedactedLogger();
        string userSelection = "Confidential user draft document content";

        logger.LogUserSelection(userSelection);

        var logs = logger.GetLogs();
        Assert.Single(logs);
        Assert.DoesNotContain("Confidential user draft", logs[0]);
        Assert.Contains("[REDACTED_SELECTION", logs[0]);
    }

    [Fact]
    public void RedactedLogger_LogClipboardData_RedactsTextAndZeroLeaks()
    {
        var logger = new RedactedLogger();
        string clipboardText = "Secret password or credit card 1234-5678-9012";

        logger.LogClipboardData(clipboardText);

        var logs = logger.GetLogs();
        Assert.Single(logs);
        Assert.DoesNotContain("1234-5678-9012", logs[0]);
        Assert.Contains("[REDACTED_CLIPBOARD", logs[0]);
    }

    [Fact]
    public void RedactedLogger_LogApiKeyUsage_ZeroKeyLeaks()
    {
        var logger = new RedactedLogger();
        string secretApiKey = "sk-proj-999988887777666655554444333322221111";

        logger.LogApiKeyUsage("openai", secretApiKey);

        var logs = logger.GetLogs();
        Assert.Single(logs);
        Assert.DoesNotContain(secretApiKey, logs[0]);
        Assert.Contains("[REDACTED_API_KEY]", logs[0]);
    }

    [Fact]
    public void RedactedLogger_MultipleLogs_Clear_ResetsLogBuffer()
    {
        var logger = new RedactedLogger();
        logger.LogInformation("Info message");
        logger.LogWarning("Warning message");
        logger.LogError("Error message");

        Assert.Equal(3, logger.GetLogs().Count);

        logger.Clear();
        Assert.Empty(logger.GetLogs());
    }

    [Fact]
    public void RedactedLogger_ZeroKeyLeaks_AcrossAllLogTypes()
    {
        var logger = new RedactedLogger();
        string openAiKey = "sk-proj-00001111222233334444555566667777";
        string anthropicKey = "sk-ant-88889999000011112222333344445555";
        string selection = "Top secret document text";
        string clipboard = "Private clipboard data";

        logger.LogInformation($"Connecting with key={openAiKey}");
        logger.LogWarning($"Auth header x-api-key: {anthropicKey}");
        logger.LogUserSelection(selection);
        logger.LogClipboardData(clipboard);

        var logs = logger.GetLogs();

        foreach (var log in logs)
        {
            Assert.DoesNotContain(openAiKey, log);
            Assert.DoesNotContain(anthropicKey, log);
            Assert.DoesNotContain(selection, log);
            Assert.DoesNotContain(clipboard, log);
        }
    }
}
