using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

internal sealed class NotesStorage
{
    private const string NotesDirName = ".cascade-ide";
    private const string NotesFileName = "agent-notes.md";
    private const string EnvNotesFile = "AGENT_NOTES_FILE";
    private const string RevisionsDirName = ".revisions";

    private readonly object _sync = new();
    private static readonly Regex SectionRegex = new(
        @"<!--\s*section:(?<id>[A-Za-z0-9._-]+)\s*-->\s*(?<content>.*?)\s*<!--\s*/section:\k<id>\s*-->",
        RegexOptions.Singleline | RegexOptions.Compiled);

    internal string GetNotesPath(string workspacePath)
    {
        var globalPath = Environment.GetEnvironmentVariable(EnvNotesFile);
        if (!string.IsNullOrWhiteSpace(globalPath))
            return Path.GetFullPath(globalPath.Trim());

        var root = Path.GetFullPath(workspacePath.Trim());
        if (File.Exists(root))
            root = Path.GetDirectoryName(root) ?? root;

        return Path.Combine(root, NotesDirName, NotesFileName);
    }

    internal string Read(string workspacePath)
    {
        var filePath = GetNotesPath(workspacePath);
        return File.Exists(filePath) ? File.ReadAllText(filePath, Encoding.UTF8) : "";
    }

    internal string Write(string workspacePath, string content) =>
        SaveWithRevision(GetNotesPath(workspacePath), content, "write");

    internal string Append(string workspacePath, string contentToAppend)
    {
        var notesPath = GetNotesPath(workspacePath);
        var existing = File.Exists(notesPath) ? File.ReadAllText(notesPath, Encoding.UTF8) : "";
        var separator = existing.Length > 0 && !existing.EndsWith('\n') ? "\n" : "";
        return SaveWithRevision(notesPath, existing + separator + contentToAppend, "append");
    }

    internal string UpsertSection(string workspacePath, string sectionId, string content)
    {
        var notesPath = GetNotesPath(workspacePath);
        var existing = File.Exists(notesPath) ? File.ReadAllText(notesPath, Encoding.UTF8) : "";

        var startMarker = $"<!-- section:{sectionId} -->";
        var endMarker = $"<!-- /section:{sectionId} -->";
        var sectionBlock = $"{startMarker}\n{content}\n{endMarker}";

        var start = existing.IndexOf(startMarker, StringComparison.Ordinal);
        var end = start >= 0 ? existing.IndexOf(endMarker, start, StringComparison.Ordinal) : -1;

        string next;
        if (start >= 0 && end >= 0)
        {
            var before = existing[..start].TrimEnd('\r', '\n');
            var after = existing[(end + endMarker.Length)..].TrimStart('\r', '\n');
            next = JoinBlocks(before, sectionBlock, after);
        }
        else
        {
            next = JoinBlocks(existing, sectionBlock);
        }

        return SaveWithRevision(notesPath, next, $"upsert-{sectionId}");
    }

    internal string ListRevisions(string workspacePath, int limit)
    {
        var notesPath = GetNotesPath(workspacePath);
        var revisionsDir = GetRevisionsDir(notesPath);
        if (!Directory.Exists(revisionsDir))
            return "[]";

        var revisions = Directory.GetFiles(revisionsDir, "*.md")
            .OrderByDescending(Path.GetFileName)
            .Take(limit)
            .Select(path =>
            {
                var info = new FileInfo(path);
                return new
                {
                    file = Path.GetFileName(path),
                    size_bytes = info.Length,
                    modified_utc = info.LastWriteTimeUtc.ToString("O")
                };
            })
            .ToArray();

        return JsonSerializer.Serialize(revisions, new JsonSerializerOptions { WriteIndented = true });
    }

    internal string Rollback(string workspacePath, string? revisionFile)
    {
        var notesPath = GetNotesPath(workspacePath);
        var revisionsDir = GetRevisionsDir(notesPath);
        if (!Directory.Exists(revisionsDir))
            throw new ArgumentException("No revisions found.");

        var resolvedRevisionFile = revisionFile
            ?? Directory.GetFiles(revisionsDir, "*.md")
                .Select(Path.GetFileName)
                .OrderByDescending(name => name)
                .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(resolvedRevisionFile))
            throw new ArgumentException("No revisions found.");

        var revisionPath = Path.Combine(revisionsDir, resolvedRevisionFile);
        if (!File.Exists(revisionPath))
            throw new ArgumentException("revision_file not found.");

        var target = File.ReadAllText(revisionPath, Encoding.UTF8);
        var result = SaveWithRevision(notesPath, target, $"rollback-{Path.GetFileNameWithoutExtension(resolvedRevisionFile)}");
        return result == "NO_CHANGES" ? $"NO_CHANGES ({resolvedRevisionFile})" : $"OK ({resolvedRevisionFile})";
    }

    internal string Search(string workspacePath, string query, int limit)
    {
        var notes = Read(workspacePath);
        var lines = notes.Replace("\r\n", "\n").Split('\n');
        var totalMatches = 0;
        var returned = new List<object>();

        for (var i = 0; i < lines.Length; i++)
        {
            if (!lines[i].Contains(query, StringComparison.OrdinalIgnoreCase))
                continue;

            totalMatches++;
            if (returned.Count >= limit)
                continue;

            returned.Add(new
            {
                line = i + 1,
                text = lines[i]
            });
        }

        var payload = new
        {
            query,
            total_matches = totalMatches,
            returned_matches = returned.Count,
            matches = returned
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    internal string ReadHotContext(string workspacePath, string? activeScope)
    {
        var notes = Read(workspacePath);
        if (string.IsNullOrWhiteSpace(notes))
            return "";

        var sections = ParseSections(notes);
        var resolvedScope = ResolveScope(activeScope, sections);

        var priorityIds = new[]
        {
            "active-scope",
            "current-task",
            "core-software-context",
            "language-style-ru",
            "execution-gate-v1",
            "response-finalizer-v1",
            "hot-context-writing-contract",
            "ontology-router-v1"
        }.ToList();

        var scopeId = resolvedScope switch
        {
            "portal" => "scope-portal",
            "mixed" => "scope-mixed",
            _ => "scope-current-projects"
        };
        priorityIds.Add(scopeId);

        var loaded = new List<string>();
        var blocks = new List<string>();
        foreach (var id in priorityIds.Distinct(StringComparer.Ordinal))
        {
            if (!sections.TryGetValue(id, out var content))
                continue;

            loaded.Add(id);
            blocks.Add($"<!-- section:{id} -->\n{content}\n<!-- /section:{id} -->");
        }

        var payload = new
        {
            active_scope = resolvedScope,
            loaded_sections = loaded,
            content = JoinBlocks(blocks.ToArray()).TrimEnd('\n')
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    internal string ExtractFromArchive(string workspacePath, string query, string? revisionFile, int limit, int contextLines)
    {
        var notesPath = GetNotesPath(workspacePath);
        var revisionsDir = GetRevisionsDir(notesPath);
        if (!Directory.Exists(revisionsDir))
            throw new ArgumentException("No revisions found.");

        var resolvedRevisionFile = revisionFile
            ?? Directory.GetFiles(revisionsDir, "*.md")
                .Select(Path.GetFileName)
                .OrderByDescending(name => name)
                .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(resolvedRevisionFile))
            throw new ArgumentException("No revisions found.");

        var revisionPath = Path.Combine(revisionsDir, resolvedRevisionFile);
        if (!File.Exists(revisionPath))
            throw new ArgumentException("revision_file not found.");

        var text = File.ReadAllText(revisionPath, Encoding.UTF8);
        var lines = text.Replace("\r\n", "\n").Split('\n');

        var totalMatches = 0;
        var matches = new List<object>();
        for (var i = 0; i < lines.Length; i++)
        {
            if (!lines[i].Contains(query, StringComparison.OrdinalIgnoreCase))
                continue;

            totalMatches++;
            if (matches.Count >= limit)
                continue;

            var start = Math.Max(0, i - contextLines);
            var end = Math.Min(lines.Length - 1, i + contextLines);
            var window = new List<object>();
            for (var j = start; j <= end; j++)
            {
                window.Add(new
                {
                    line = j + 1,
                    text = lines[j]
                });
            }

            matches.Add(new
            {
                line = i + 1,
                text = lines[i],
                context = window
            });
        }

        var payload = new
        {
            revision_file = resolvedRevisionFile,
            query,
            total_matches = totalMatches,
            returned_matches = matches.Count,
            matches
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    internal string CompactHotContext(string workspacePath, bool apply)
    {
        var notesPath = GetNotesPath(workspacePath);
        var existing = File.Exists(notesPath) ? File.ReadAllText(notesPath, Encoding.UTF8) : "";
        var compacted = CompactNotes(existing);

        if (!apply)
        {
            var payload = new
            {
                changed = !string.Equals(existing, compacted, StringComparison.Ordinal),
                content = compacted.TrimEnd('\n')
            };

            return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        }

        return SaveWithRevision(notesPath, compacted, "compact-hot-context");
    }

    private string SaveWithRevision(string notesPath, string newContent, string reason)
    {
        lock (_sync)
        {
            var hasCurrent = File.Exists(notesPath);
            var currentContent = hasCurrent ? File.ReadAllText(notesPath, Encoding.UTF8) : "";

            if (currentContent == newContent)
                return "NO_CHANGES";

            if (hasCurrent)
                WriteRevisionSnapshot(notesPath, currentContent, reason);

            AtomicWriteAllText(notesPath, newContent);
            return "OK";
        }
    }

    private static string GetRevisionsDir(string notesPath)
    {
        var dir = Path.GetDirectoryName(notesPath);
        if (string.IsNullOrWhiteSpace(dir))
            throw new ArgumentException("Invalid notes path.");
        return Path.Combine(dir, RevisionsDirName);
    }

    private static void AtomicWriteAllText(string path, string content)
    {
        var dir = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(dir))
            throw new ArgumentException("Invalid target path.");

        Directory.CreateDirectory(dir);
        var tempPath = Path.Combine(dir, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        File.WriteAllText(tempPath, content, Encoding.UTF8);
        File.Move(tempPath, path, true);
    }

    private static void WriteRevisionSnapshot(string notesPath, string snapshotContent, string reason)
    {
        var revisionsDir = GetRevisionsDir(notesPath);
        Directory.CreateDirectory(revisionsDir);

        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
        var revisionName = $"{timestamp}-{NormalizeReason(reason)}-{ComputeShortHash(snapshotContent)}.md";
        var revisionPath = Path.Combine(revisionsDir, revisionName);
        File.WriteAllText(revisionPath, snapshotContent, Encoding.UTF8);
    }

    private static string NormalizeReason(string reason)
    {
        var buffer = new StringBuilder(reason.Length);
        foreach (var ch in reason.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(ch) || ch is '.' or '_' or '-')
                buffer.Append(ch);
            else if (buffer.Length == 0 || buffer[^1] != '-')
                buffer.Append('-');
        }

        return buffer.ToString().Trim('-') is { Length: > 0 } normalized
            ? normalized
            : "update";
    }

    private static string ComputeShortHash(string content)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash.AsSpan(0, 4)).ToLowerInvariant();
    }

    private static string JoinBlocks(params string[] blocks)
    {
        var nonEmpty = blocks
            .Select(block => block.Trim('\r', '\n'))
            .Where(block => block.Length > 0)
            .ToArray();

        if (nonEmpty.Length == 0)
            return "";

        return string.Join("\n\n", nonEmpty) + "\n";
    }

    private static Dictionary<string, string> ParseSections(string notes)
    {
        var sections = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in SectionRegex.Matches(notes))
        {
            var id = match.Groups["id"].Value;
            var content = match.Groups["content"].Value.Trim('\r', '\n');
            sections[id] = content;
        }

        return sections;
    }

    private static string ResolveScope(string? requestedScope, IReadOnlyDictionary<string, string> sections)
    {
        if (!string.IsNullOrWhiteSpace(requestedScope))
            return requestedScope.Trim().ToLowerInvariant();

        if (!sections.TryGetValue("active-scope", out var activeScopeContent))
            return "current-projects";

        var match = Regex.Match(activeScopeContent, @"current\s*:\s*(?<scope>[A-Za-z0-9._-]+)", RegexOptions.IgnoreCase);
        return match.Success
            ? match.Groups["scope"].Value.Trim().ToLowerInvariant()
            : "current-projects";
    }

    private static string CompactNotes(string notes)
    {
        var sections = ParseSections(notes);
        if (sections.Count == 0)
            return NormalizeWhitespace(notes);

        var preferredOrder = new[]
        {
            "core-software-context",
            "language-style-ru",
            "active-scope",
            "current-task",
            "scope-current-projects",
            "scope-portal",
            "scope-mixed",
            "execution-gate-v1",
            "response-finalizer-v1",
            "hot-context-writing-contract",
            "ontology-router-v1",
            "memory-architecture-v1",
            "memory-load-policy-v1",
            "memory-compaction-loop-v1",
            "archive-index-v1"
        };

        var blocks = new List<string>();
        foreach (var id in preferredOrder)
        {
            if (!sections.TryGetValue(id, out var content))
                continue;

            blocks.Add($"<!-- section:{id} -->\n{content}\n<!-- /section:{id} -->");
            sections.Remove(id);
        }

        foreach (var id in sections.Keys.OrderBy(x => x, StringComparer.Ordinal))
        {
            blocks.Add($"<!-- section:{id} -->\n{sections[id]}\n<!-- /section:{id} -->");
        }

        return JoinBlocks(blocks.ToArray());
    }

    private static string NormalizeWhitespace(string text)
    {
        var normalized = text.Replace("\r\n", "\n");
        normalized = Regex.Replace(normalized, @"\n{3,}", "\n\n");
        return normalized.EndsWith('\n') ? normalized : normalized + "\n";
    }
}
