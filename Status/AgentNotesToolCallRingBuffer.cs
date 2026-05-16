using System.Collections.Concurrent;
using System.Text.Json;

namespace AgentNotesMcp.Status;

internal static class AgentNotesToolCallRingBuffer
{
    internal const int DefaultCapacity = 64;

    private static readonly ConcurrentQueue<ToolCallEntry> Queue = new();

    internal static void Record(
        string toolName,
        IReadOnlyDictionary<string, JsonElement> args,
        string resultText,
        bool isError,
        long durationMs)
    {
        var entry = new ToolCallEntry(
            DateTimeOffset.UtcNow,
            toolName,
            TryGetWorkspacePath(args),
            isError,
            durationMs,
            Preview(resultText));

        Queue.Enqueue(entry);
        while (Queue.Count > DefaultCapacity && Queue.TryDequeue(out _))
        {
        }
    }

    internal static IReadOnlyList<ToolCallEntry> Snapshot()
    {
        var list = Queue.ToArray();
        if (list.Length <= 1)
            return list;

        Array.Reverse(list);
        return list;
    }

    internal static void ClearForTests()
    {
        while (Queue.TryDequeue(out _))
        {
        }
    }

    private static string? TryGetWorkspacePath(IReadOnlyDictionary<string, JsonElement> args)
    {
        if (!args.TryGetValue("workspace_path", out var el))
            return null;

        var path = el.GetString();
        return string.IsNullOrWhiteSpace(path) ? null : path.Trim();
    }

    private static string Preview(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        var oneLine = text.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');
        const int max = 120;
        return oneLine.Length <= max ? oneLine : oneLine[..max] + "…";
    }

    internal sealed record ToolCallEntry(
        DateTimeOffset AtUtc,
        string ToolName,
        string? WorkspacePath,
        bool IsError,
        long DurationMs,
        string ResultPreview);
}
