using System.Text.RegularExpressions;

namespace Polishly.WindowsIntegration.Security;

public interface IRedactedLogger
{
    string Redact(string? input);
    void LogInformation(string message);
    void LogWarning(string message);
    void LogError(string message, Exception? ex = null);
    void LogUserSelection(string selectionText);
    void LogClipboardData(string clipboardText);
    void LogApiKeyUsage(string providerId, string apiKey);
    IReadOnlyList<string> GetLogs();
    void Clear();
}

public class RedactedLogger : IRedactedLogger
{
    private readonly List<string> _logs = new();
    private readonly object _lock = new();

    private static readonly Regex OpenAIKeyRegex = new(@"sk-proj-[A-Za-z0-9_-]+|sk-[A-Za-z0-9_-]{15,}", RegexOptions.Compiled);
    private static readonly Regex AnthropicKeyRegex = new(@"sk-ant-[A-Za-z0-9_-]+", RegexOptions.Compiled);
    private static readonly Regex GroqKeyRegex = new(@"gsk_[A-Za-z0-9_-]+", RegexOptions.Compiled);
    private static readonly Regex CerebrasKeyRegex = new(@"csk-[A-Za-z0-9_-]+", RegexOptions.Compiled);
    private static readonly Regex BearerTokenRegex = new(@"(?i)Bearer\s+[A-Za-z0-9._\~+/-]+=*", RegexOptions.Compiled);
    private static readonly Regex HeaderApiKeyRegex = new(@"(?i)x-api-key:\s*[^\s,;]+", RegexOptions.Compiled);
    private static readonly Regex HeaderAuthRegex = new(@"(?i)authorization:\s*[^\s,;]+", RegexOptions.Compiled);
    private static readonly Regex ParamApiKeyRegex = new(@"(?i)(api[_-]?key|secret|password|token)\s*[:=]\s*(?:""[^""]*""|'[^']*'|[^\s,;]+)", RegexOptions.Compiled);
    private static readonly Regex SelectionRegex = new(@"(?i)(selection|userselection|inputtext)\s*[:=]\s*(?:""[^""]*""|'[^']*'|[^\s,;]+)", RegexOptions.Compiled);
    private static readonly Regex ClipboardRegex = new(@"(?i)(clipboard|clipboarddata)\s*[:=]\s*(?:""[^""]*""|'[^']*'|[^\s,;]+)", RegexOptions.Compiled);

    public string Redact(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        string result = input;
        result = OpenAIKeyRegex.Replace(result, "[REDACTED_API_KEY]");
        result = AnthropicKeyRegex.Replace(result, "[REDACTED_API_KEY]");
        result = GroqKeyRegex.Replace(result, "[REDACTED_API_KEY]");
        result = CerebrasKeyRegex.Replace(result, "[REDACTED_API_KEY]");
        result = BearerTokenRegex.Replace(result, "Bearer [REDACTED_TOKEN]");
        result = HeaderApiKeyRegex.Replace(result, "x-api-key: [REDACTED_KEY]");
        result = HeaderAuthRegex.Replace(result, "authorization: [REDACTED_TOKEN]");
        result = ParamApiKeyRegex.Replace(result, "$1=[REDACTED_KEY]");
        result = SelectionRegex.Replace(result, "$1=[REDACTED_SELECTION]");
        result = ClipboardRegex.Replace(result, "$1=[REDACTED_CLIPBOARD]");

        return result;
    }

    public void LogInformation(string message)
    {
        AddLog($"[INFO] {Redact(message)}");
    }

    public void LogWarning(string message)
    {
        AddLog($"[WARN] {Redact(message)}");
    }

    public void LogError(string message, Exception? ex = null)
    {
        string logMsg = $"[ERROR] {Redact(message)}";
        if (ex != null)
        {
            logMsg += $" | Exception: {Redact(ex.Message)}";
        }
        AddLog(logMsg);
    }

    public void LogUserSelection(string selectionText)
    {
        int length = selectionText?.Length ?? 0;
        AddLog($"[INFO] UserSelection: [REDACTED_SELECTION: len={length}]");
    }

    public void LogClipboardData(string clipboardText)
    {
        int length = clipboardText?.Length ?? 0;
        AddLog($"[INFO] ClipboardData: [REDACTED_CLIPBOARD: len={length}]");
    }

    public void LogApiKeyUsage(string providerId, string apiKey)
    {
        string maskedKey;
        if (string.IsNullOrEmpty(apiKey))
        {
            maskedKey = "****";
        }
        else
        {
            maskedKey = "[REDACTED_API_KEY]";
        }
        AddLog($"[INFO] Provider '{providerId}' API Key: {maskedKey}");
    }


    public IReadOnlyList<string> GetLogs()
    {
        lock (_lock)
        {
            return _logs.ToList().AsReadOnly();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _logs.Clear();
        }
    }

    private void AddLog(string entry)
    {
        lock (_lock)
        {
            _logs.Add(entry);
        }
    }
}
