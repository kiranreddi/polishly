namespace Polishly.Core;

public enum RewriteState
{
    Idle,
    Capturing,
    Requesting,
    Streaming,
    Diffing,
    Accepted,
    Cancelled,
    Failed
}
