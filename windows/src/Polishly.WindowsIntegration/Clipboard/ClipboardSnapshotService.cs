using System.Runtime.InteropServices;
using System.IO;
#if HAS_WPF
using System.Windows;
#endif

namespace Polishly.WindowsIntegration.Clipboard;

/// <summary>
/// Materializes the current clipboard before Polishly temporarily owns it.
/// The snapshot is deliberately all-or-nothing: if any advertised format cannot
/// be read, Polishly does not modify the clipboard.
/// </summary>
internal sealed class ClipboardSnapshotService
{
#if HAS_WPF
    internal sealed record Snapshot(IReadOnlyList<ClipboardItem> Items);
    internal sealed record ClipboardItem(string Format, object Data);

    public Snapshot Capture()
    {
        return Retry(() =>
        {
            IDataObject? source = System.Windows.Clipboard.GetDataObject();
            if (source == null)
            {
                return new Snapshot(Array.Empty<ClipboardItem>());
            }

            var items = new List<ClipboardItem>();
            foreach (string format in source.GetFormats(autoConvert: false).Distinct(StringComparer.Ordinal))
            {
                object? value = source.GetData(format, autoConvert: false);
                if (value == null)
                {
                    throw new InvalidOperationException($"Clipboard format '{format}' could not be materialized.");
                }

                items.Add(new ClipboardItem(format, CloneIfNeeded(value)));
            }

            return new Snapshot(items);
        });
    }

    public void Restore(Snapshot snapshot)
    {
        Retry(() =>
        {
            var restored = new DataObject();
            foreach (var item in snapshot.Items)
            {
                restored.SetData(item.Format, CloneIfNeeded(item.Data), false);
            }

            System.Windows.Clipboard.SetDataObject(restored, copy: true);
            return true;
        });
    }

    public void SetUnicodeText(string text)
    {
        Retry(() =>
        {
            System.Windows.Clipboard.SetText(text, TextDataFormat.UnicodeText);
            return true;
        });
    }

    public string GetUnicodeText()
    {
        return Retry(() => System.Windows.Clipboard.ContainsText(TextDataFormat.UnicodeText)
            ? System.Windows.Clipboard.GetText(TextDataFormat.UnicodeText)
            : string.Empty);
    }

    private static object CloneIfNeeded(object value)
    {
        if (value is MemoryStream stream)
        {
            long originalPosition = stream.CanSeek ? stream.Position : 0;
            if (stream.CanSeek) stream.Position = 0;
            var clone = new MemoryStream();
            stream.CopyTo(clone);
            clone.Position = 0;
            if (stream.CanSeek) stream.Position = originalPosition;
            return clone;
        }

        if (value is byte[] bytes)
        {
            return bytes.ToArray();
        }

        return value;
    }

    private static T Retry<T>(Func<T> action)
    {
        const int attempts = 8;
        for (int attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                return action();
            }
            catch (COMException) when (attempt < attempts)
            {
                Thread.Sleep(15 * attempt);
            }
        }

        return action();
    }
#else
    internal sealed record Snapshot;

    public Snapshot Capture() => new();
    public void Restore(Snapshot snapshot) { }
    public void SetUnicodeText(string text) { }
    public string GetUnicodeText() => string.Empty;
#endif
}
