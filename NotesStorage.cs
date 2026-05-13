using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AgentNotes.Core;

public sealed class NotesStorage
{
    private const string NotesDirName = ".cascade-ide";
    private const string NotesFileName = "agent-notes.md";
    private const string EnvNotesFile = "AGENT_NOTES_FILE";
    private const string RevisionsDirName = ".revisions";
    private const string KnowledgeDirName = "knowledge";
    private const string EnvCanonPath = "AGENT_NOTES_CANON_PATH";
    private const string MemoryArchitectureManifestKey = "l0_manifest";

    private readonly object _sync = new();
    private static readonly Regex SectionRegex = new(
        @"<!--\s*section:(?<id>[A-Za-z0-9._-]+)\s*-->\s*(?<content>.*?)\s*<!--\s*/section:\k<id>\s*-->",
        RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex MemoryArchitectureManifestRegex = new(
        @"(?m)^\s*l0_manifest\s*:\s*(?<path>\S+)\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Hot notes path: <c>AGENT_NOTES_FILE</c> if set; else <c>{AGENT_NOTES_CANON_PATH}/agent-notes.md</c> if canon env set; else <c>workspace_path/.cascade-ide/agent-notes.md</c>.</summary>
    public string GetNotesPath(string workspacePath)
    {
        var globalPath = Environment.GetEnvironmentVariable(EnvNotesFile);
        if (!string.IsNullOrWhiteSpace(globalPath))
            return Path.GetFullPath(globalPath.Trim());

        var canonEnv = Environment.GetEnvironmentVariable(EnvCanonPath);
        if (!string.IsNullOrWhiteSpace(canonEnv))
            return Path.Combine(Path.GetFullPath(canonEnv.Trim()), NotesFileName);

        var root = Path.GetFullPath(workspacePath.Trim());
        if (File.Exists(root))
            root = Path.GetDirectoryName(root) ?? root;

        return Path.Combine(root, NotesDirName, NotesFileName);
    }

    /// <summary>Resolve canon root: tool argument, then AGENT_NOTES_CANON_PATH, else inferred from AGENT_NOTES_FILE (ancestor directory containing knowledge/).</summary>
    public static string ResolveCanonPath(string? canonPath)
    {
        if (!string.IsNullOrWhiteSpace(canonPath))
            return Path.GetFullPath(canonPath.Trim());

        var fromEnvCanon = Environment.GetEnvironmentVariable(EnvCanonPath);
        if (!string.IsNullOrWhiteSpace(fromEnvCanon))
            return Path.GetFullPath(fromEnvCanon.Trim());

        var fromEnvNotes = Environment.GetEnvironmentVariable(EnvNotesFile);
        if (!string.IsNullOrWhiteSpace(fromEnvNotes))
        {
            var inferred = TryInferCanonRootFromAgentNotesFilePath(fromEnvNotes.Trim());
            if (inferred is not null)
                return inferred;
        }

        throw new ArgumentException(
            "canon_path is required when AGENT_NOTES_CANON_PATH is not set and AGENT_NOTES_FILE does not lie under a directory tree that contains knowledge/ (or AGENT_NOTES_FILE is unset).");
    }

    /// <summary>Walks parents from the notes file directory; returns the first directory that contains a <c>knowledge/</c> subfolder (agent-notes repo layout).</summary>
    internal static string? TryInferCanonRootFromAgentNotesFilePath(string agentNotesFilePath)
    {
        var fullPath = Path.GetFullPath(agentNotesFilePath);
        var current = Path.GetDirectoryName(fullPath);
        while (!string.IsNullOrEmpty(current))
        {
            if (Directory.Exists(Path.Combine(current, KnowledgeDirName)))
                return current;

            var parent = Directory.GetParent(current);
            current = parent?.FullName;
        }

        return null;
    }

    /// <summary>Validate relative path under knowledge/: no "..", no leading slash. Returns normalized relative path.</summary>
    private static string ValidateKnowledgeRelativePath(string filePath)
    {
        var normalized = filePath.Replace('\\', '/').TrimStart('/');
        if (normalized.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(normalized))
            throw new ArgumentException("file_path must be a relative path under knowledge/ (no '..', no absolute path).");
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException("file_path is required.");
        return normalized;
    }

    public string GetKnowledgeFilePath(string? canonPath, string filePath)
    {
        var root = ResolveCanonPath(canonPath);
        var relative = ValidateKnowledgeRelativePath(filePath);
        return Path.Combine(root, KnowledgeDirName, relative);
    }

    public string ReadKnowledgeFile(string? canonPath, string filePath, int? firstLine1Based = null, int? maxLineCount = null)
    {
        var fullPath = GetKnowledgeFilePath(canonPath, filePath);
        if (!File.Exists(fullPath)) return "";
        var full = File.ReadAllText(fullPath, Encoding.UTF8);
        if (firstLine1Based is null && maxLineCount is null) return full;
        return SliceTextByLines(full, firstLine1Based ?? 1, maxLineCount);
    }

    /// <summary>Return a substring of <paramref name="text"/> by line numbers. <paramref name="firstLine1Based"/> is 1-based. <paramref name="maxLineCount"/>: null = to EOF, 0 = empty, N = at most N lines.</summary>
    internal static string SliceTextByLines(string text, int firstLine1Based, int? maxLineCount)
    {
        if (maxLineCount is 0) return "";
        var lines = SplitToLines(text);
        var start = Math.Max(0, firstLine1Based - 1);
        if (start >= lines.Length) return "";
        if (maxLineCount is int cap)
        {
            if (cap < 0) return "";
            var n = Math.Min(cap, lines.Length - start);
            if (n <= 0) return "";
            return string.Join("\n", lines, start, n);
        }
        return string.Join("\n", lines, start, lines.Length - start);
    }

    private static string[] SplitToLines(string text) =>
        text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);

    public string ListKnowledgeFiles(string? canonPath, string? subdir)
    {
        var root = ResolveCanonPath(canonPath);
        var knowledgeRoot = Path.Combine(root, KnowledgeDirName);
        var searchDir = string.IsNullOrWhiteSpace(subdir)
            ? knowledgeRoot
            : Path.Combine(knowledgeRoot, ValidateKnowledgeRelativePath(subdir.Trim().Replace('\\', '/')));
        if (!Directory.Exists(searchDir))
            return JsonSerializer.Serialize(new { path = searchDir, files = Array.Empty<object>(), total = 0 }, JsonOptions);
        var baseLen = knowledgeRoot.Length;
        var files = Directory.GetFiles(searchDir, "*", SearchOption.AllDirectories)
            .Where(p => !p.Contains(RevisionsDirName, StringComparison.Ordinal))
            .Select(p =>
            {
                var rel = p.Substring(baseLen).TrimStart(Path.DirectorySeparatorChar).Replace('\\', '/');
                var info = new FileInfo(p);
                return new { path = rel, size_bytes = info.Length, modified_utc = info.LastWriteTimeUtc.ToString("O") };
            })
            .OrderBy(x => x.path, StringComparer.Ordinal)
            .ToArray();
        return JsonSerializer.Serialize(new { path = searchDir, files, total = files.Length }, JsonOptions);
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string WriteKnowledgeFile(string? canonPath, string filePath, string content, bool saveRevision = true)
    {
        var fullPath = GetKnowledgeFilePath(canonPath, filePath);
        if (saveRevision && File.Exists(fullPath))
        {
            var current = File.ReadAllText(fullPath, Encoding.UTF8);
            WriteKnowledgeRevision(canonPath, filePath, current, "write");
        }
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(fullPath, content, Encoding.UTF8);
        return "OK";
    }

    private void WriteKnowledgeRevision(string? canonPath, string filePath, string snapshotContent, string reason)
    {
        var root = ResolveCanonPath(canonPath);
        var revisionsDir = Path.Combine(root, KnowledgeDirName, RevisionsDirName);
        Directory.CreateDirectory(revisionsDir);
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
        var safeName = filePath.Replace('/', '-').Replace('\\', '-');
        var revisionName = $"{timestamp}-{NormalizeReason(reason)}-{safeName}-{ComputeShortHash(snapshotContent)}.md";
        var revisionPath = Path.Combine(revisionsDir, revisionName);
        File.WriteAllText(revisionPath, snapshotContent, Encoding.UTF8);
    }

    public string AppendKnowledgeFile(string? canonPath, string filePath, string content, bool saveRevision = true)
    {
        var fullPath = GetKnowledgeFilePath(canonPath, filePath);
        var existing = File.Exists(fullPath) ? File.ReadAllText(fullPath, Encoding.UTF8) : "";
        if (saveRevision && existing.Length > 0)
            WriteKnowledgeRevision(canonPath, filePath, existing, "append");
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);
        var separator = existing.Length > 0 && !existing.EndsWith('\n') ? "\n" : "";
        File.WriteAllText(fullPath, existing + separator + content, Encoding.UTF8);
        return "OK";
    }

    public string UpsertKnowledgeSection(string? canonPath, string filePath, string sectionId, string content, bool saveRevision = true)
    {
        var fullPath = GetKnowledgeFilePath(canonPath, filePath);
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);
        var existing = File.Exists(fullPath) ? File.ReadAllText(fullPath, Encoding.UTF8) : "";
        if (saveRevision && existing.Length > 0)
            WriteKnowledgeRevision(canonPath, filePath, existing, "upsert");
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
        File.WriteAllText(fullPath, next, Encoding.UTF8);
        return "OK";
    }

    public string DeleteKnowledgeFile(string? canonPath, string filePath)
    {
        var fullPath = GetKnowledgeFilePath(canonPath, filePath);
        if (!File.Exists(fullPath))
            return "NO_CHANGES";
        File.Delete(fullPath);
        return "OK";
    }

    public string DeleteKnowledgeSection(string? canonPath, string filePath, string sectionId)
    {
        var fullPath = GetKnowledgeFilePath(canonPath, filePath);
        if (!File.Exists(fullPath))
            return "NO_CHANGES";
        var existing = File.ReadAllText(fullPath, Encoding.UTF8);
        var startMarker = $"<!-- section:{sectionId} -->";
        var endMarker = $"<!-- /section:{sectionId} -->";
        var start = existing.IndexOf(startMarker, StringComparison.Ordinal);
        var end = start >= 0 ? existing.IndexOf(endMarker, start, StringComparison.Ordinal) : -1;
        if (start < 0 || end < 0)
            return "NO_CHANGES";
        var before = existing[..start].TrimEnd('\r', '\n');
        var after = existing[(end + endMarker.Length)..].TrimStart('\r', '\n');
        var next = JoinBlocks(before, after);
        File.WriteAllText(fullPath, next, Encoding.UTF8);
        return "OK";
    }

    public string Read(string workspacePath)
    {
        var filePath = GetNotesPath(workspacePath);
        return File.Exists(filePath) ? File.ReadAllText(filePath, Encoding.UTF8) : "";
    }

    public string Write(string workspacePath, string content) =>
        SaveWithRevision(GetNotesPath(workspacePath), content, "write");

    public string Append(string workspacePath, string contentToAppend)
    {
        var notesPath = GetNotesPath(workspacePath);
        var existing = File.Exists(notesPath) ? File.ReadAllText(notesPath, Encoding.UTF8) : "";
        var separator = existing.Length > 0 && !existing.EndsWith('\n') ? "\n" : "";
        return SaveWithRevision(notesPath, existing + separator + contentToAppend, "append");
    }

    public string UpsertSection(string workspacePath, string sectionId, string content)
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

    public string ListRevisions(string workspacePath, int limit)
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

    public string Rollback(string workspacePath, string? revisionFile)
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

    public string Search(string workspacePath, string query, int limit)
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

    public string ReadHotContext(string workspacePath, string? activeScope)
    {
        var notes = Read(workspacePath);
        if (string.IsNullOrWhiteSpace(notes))
            return "";

        var sections = ParseSections(notes);
        var scopeAliases = LoadScopeAliasesMerged();
        var resolvedScope = ResolveScope(activeScope, sections, workspacePath, scopeAliases);

        var notesPath = GetNotesPath(workspacePath);
        var manifest = LoadMemoryArchitectureManifest(sections, notesPath);
        var l0 = ResolveL0Ids(sections, notesPath, manifest);
        var priorityIds = (l0 ?? HotContextDefaults.DefaultL0Ids).ToList();
        var scopeId = ResolveScopeSectionId(resolvedScope, sections, scopeAliases);
        priorityIds.Add(scopeId);

        var loaded = new List<string>();
        var blocks = new List<string>();
        foreach (var id in priorityIds.Distinct(StringComparer.Ordinal))
        {
            if (IsHotExcluded(id, manifest?.HotContextSectionExclusions))
                continue;
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

    public string MemoryHealth(string workspacePath, string? activeScope)
    {
        var notesPath = GetNotesPath(workspacePath);
        var notes = Read(workspacePath);
        var sections = ParseSections(notes);
        var scopeAliases = LoadScopeAliasesMerged();
        var resolvedScope = ResolveScope(activeScope, sections, workspacePath, scopeAliases);
        var hotSectionIds = BuildHotSectionIds(resolvedScope, sections, notesPath, scopeAliases);
        var hotSections = hotSectionIds
            .Where(sections.ContainsKey)
            .Select(id => new
            {
                id,
                chars = sections[id].Length,
                lines = CountLines(sections[id])
            })
            .ToArray();

        var hotChars = hotSections.Sum(x => x.chars);
        var hotLines = hotSections.Sum(x => x.lines);
        var manifestForHealth = LoadMemoryArchitectureManifest(sections, notesPath);
        var (warnBudget, critBudget) = ResolveHotBudgetChars(manifestForHealth);

        var missingCoreSections = HotContextDefaults.RequiredCoreSectionIds
            .Where(required => !sections.ContainsKey(required))
            .ToArray();

        var warnings = new List<string>();
        var recommendCompaction = false;
        warnings.AddRange(ValidateMemoryArchitecture(sections, notesPath));

        if (hotChars > critBudget)
        {
            warnings.Add("hot_context_over_critical_budget");
            recommendCompaction = true;
        }
        else if (hotChars > warnBudget)
        {
            warnings.Add("hot_context_over_warning_budget");
            recommendCompaction = true;
        }

        if (missingCoreSections.Length > 0)
            warnings.Add("missing_core_sections");

        var healthLevel = warnings.Contains("hot_context_over_critical_budget", StringComparer.Ordinal)
            ? "critical"
            : warnings.Count > 0
                ? "warning"
                : "good";

        var recommendations = new List<string>();
        if (recommendCompaction)
            recommendations.Add("Run compact_hot_context with apply=true after preview to keep L0/L1 small.");
        if (missingCoreSections.Length > 0)
            recommendations.Add("Restore required core sections via upsert_agent_notes_section.");
        if (recommendations.Count == 0)
            recommendations.Add("Keep current memory shape; no immediate action required.");

        var payload = new
        {
            workspace_path = workspacePath,
            notes_path = notesPath,
            notes_exists = File.Exists(notesPath),
            resolved_scope = resolvedScope,
            total_chars = notes.Length,
            total_lines = CountLines(notes),
            section_count = sections.Count,
            hot_context = new
            {
                section_ids = hotSectionIds,
                loaded_section_count = hotSections.Length,
                chars = hotChars,
                lines = hotLines
            },
            missing_core_sections = missingCoreSections,
            largest_sections = sections
                .Select(kv => new
                {
                    id = kv.Key,
                    chars = kv.Value.Length,
                    lines = CountLines(kv.Value)
                })
                .OrderByDescending(x => x.chars)
                .Take(5)
                .ToArray(),
            warnings,
            recommend_compaction = recommendCompaction,
            health_level = healthLevel,
            recommendations
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    public string RouteContext(
        string workspacePath,
        string query,
        string? activeScope,
        int maxSections,
        int maxChars)
    {
        var notes = Read(workspacePath);
        if (string.IsNullOrWhiteSpace(notes))
            return JsonSerializer.Serialize(new
            {
                query,
                selected = Array.Empty<object>(),
                assembled_context = ""
            }, new JsonSerializerOptions { WriteIndented = true });

        var sections = ParseSections(notes);
        var scopeAliases = LoadScopeAliasesMerged();
        var resolvedScope = ResolveScope(activeScope, sections, workspacePath, scopeAliases);
        var notesPath = GetNotesPath(workspacePath);
        var hotSectionIds = BuildHotSectionIds(resolvedScope, sections, notesPath, scopeAliases);
        var boosted = hotSectionIds
            .Select((id, idx) => (id, bonus: Math.Max(0, 30 - idx * 2)))
            .ToDictionary(x => x.id, x => x.bonus, StringComparer.Ordinal);

        var tokens = TokenizeQuery(query);
        var candidates = new List<(string id, string content, int score, int matchCount)>();
        foreach (var (id, content) in sections)
        {
            var matchCount = CountMatches(content, tokens) + CountMatches(id, tokens);
            var score = matchCount * 4;

            if (content.Contains(query, StringComparison.OrdinalIgnoreCase))
                score += 24;
            if (id.Contains(query, StringComparison.OrdinalIgnoreCase))
                score += 20;
            if (boosted.TryGetValue(id, out var bonus))
                score += bonus;

            if (score <= 0)
                continue;

            candidates.Add((id, content, score, matchCount));
        }

        var selected = candidates
            .OrderByDescending(x => x.score)
            .ThenBy(x => x.id, StringComparer.Ordinal)
            .Take(maxSections)
            .ToArray();

        var assembled = new StringBuilder();
        var emitted = new List<object>();
        var truncated = false;
        foreach (var item in selected)
        {
            var block = $"<!-- section:{item.id} -->\n{item.content}\n<!-- /section:{item.id} -->\n\n";
            if (assembled.Length + block.Length > maxChars)
            {
                truncated = true;
                break;
            }

            assembled.Append(block);
            emitted.Add(new
            {
                id = item.id,
                score = item.score,
                match_count = item.matchCount,
                chars = item.content.Length,
                lines = CountLines(item.content),
                preview = BuildPreview(item.content, 220)
            });
        }

        var payload = new
        {
            query,
            resolved_scope = resolvedScope,
            total_candidates = candidates.Count,
            selected_count = emitted.Count,
            max_sections = maxSections,
            max_chars = maxChars,
            truncated,
            selected = emitted,
            assembled_context = assembled.ToString().TrimEnd('\n')
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    public string ExtractFromArchive(string workspacePath, string query, string? revisionFile, int limit, int contextLines)
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

    public string CompactHotContext(string workspacePath, bool apply)
    {
        var notesPath = GetNotesPath(workspacePath);
        var existing = File.Exists(notesPath) ? File.ReadAllText(notesPath, Encoding.UTF8) : "";
        var compacted = CompactNotes(existing, notesPath);

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

    /// <summary>Parse L0 section IDs from memory-architecture-v1 content (block after "### L0:" until next "###").</summary>
    private static IReadOnlyList<string>? ParseL0FromMemoryArchitecture(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        var lines = content.Replace("\r\n", "\n").Split('\n');
        var inL0 = false;
        var ids = new List<string>();
        foreach (var line in lines)
        {
            var t = line.Trim();
            if (t.StartsWith("### L0:", StringComparison.OrdinalIgnoreCase))
            {
                inL0 = true;
                continue;
            }
            if (inL0)
            {
                if (t.StartsWith("### ", StringComparison.Ordinal))
                    break;
                if (t.StartsWith("- ", StringComparison.Ordinal))
                {
                    var rest = t[2..].Trim();
                    var id = rest.Split([' ', '(', '\t'], 2, StringSplitOptions.None)[0].Trim();
                    if (id.Length > 0 && Regex.IsMatch(id, "^[A-Za-z0-9._-]+$"))
                        ids.Add(id);
                }
            }
        }

        return ids.Count > 0 ? ids : null;
    }

    private static string ResolveCanonRootFromNotesPath(string notesPath)
    {
        var dir = Path.GetDirectoryName(notesPath);
        if (string.IsNullOrWhiteSpace(dir))
            throw new ArgumentException("Invalid notes path.");
        return dir;
    }

    private static string? TryParseManifestRelativePath(string? memoryArchitectureContent)
    {
        if (string.IsNullOrWhiteSpace(memoryArchitectureContent))
            return null;
        var match = MemoryArchitectureManifestRegex.Match(memoryArchitectureContent);
        return match.Success ? match.Groups["path"].Value.Trim() : null;
    }

    private static string? TryResolveManifestFullPath(string notesPath, string? manifestPath)
    {
        if (string.IsNullOrWhiteSpace(manifestPath))
            return null;
        var p = manifestPath.Trim().Trim('"');
        if (p.Length == 0)
            return null;

        var canonRoot = ResolveCanonRootFromNotesPath(notesPath);

        if (p.StartsWith("knowledge/", StringComparison.OrdinalIgnoreCase) || p.StartsWith("knowledge\\", StringComparison.OrdinalIgnoreCase))
            return Path.GetFullPath(Path.Combine(canonRoot, p));

        if (p.StartsWith("./", StringComparison.Ordinal) || p.StartsWith(".\\", StringComparison.Ordinal))
            return Path.GetFullPath(Path.Combine(canonRoot, p));

        return Path.IsPathRooted(p) ? Path.GetFullPath(p) : Path.GetFullPath(Path.Combine(canonRoot, "knowledge", p));
    }

    private static MemoryArchitectureManifestData? TryLoadMemoryArchitectureManifest(string notesPath, string manifestPath)
    {
        var fullPath = TryResolveManifestFullPath(notesPath, manifestPath);
        if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(fullPath, Encoding.UTF8));
            var root = doc.RootElement;

            var l0 = new List<string>();
            if (root.TryGetProperty("l0", out var l0El) && l0El.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in l0El.EnumerateArray())
                {
                    var id = item.ValueKind == JsonValueKind.String ? (item.GetString() ?? "").Trim() : "";
                    if (id.Length == 0)
                        continue;
                    if (Regex.IsMatch(id, "^[A-Za-z0-9._-]+$"))
                        l0.Add(id);
                }
            }

            IReadOnlyList<string>? suffix = null;
            if (root.TryGetProperty("compact_order_suffix", out var suffixEl) && suffixEl.ValueKind == JsonValueKind.Array)
            {
                var list = new List<string>();
                foreach (var item in suffixEl.EnumerateArray())
                {
                    var id = item.ValueKind == JsonValueKind.String ? (item.GetString() ?? "").Trim() : "";
                    if (id.Length == 0)
                        continue;
                    if (Regex.IsMatch(id, "^[A-Za-z0-9._-]+$"))
                        list.Add(id);
                }
                suffix = list.Count > 0 ? list : null;
            }

            int? warnBudget = null;
            int? critBudget = null;
            if (root.TryGetProperty("hot_context_budget_warning_chars", out var wEl) && wEl.ValueKind == JsonValueKind.Number)
                warnBudget = wEl.GetInt32();
            if (root.TryGetProperty("hot_context_budget_critical_chars", out var cEl) && cEl.ValueKind == JsonValueKind.Number)
                critBudget = cEl.GetInt32();

            IReadOnlyList<string>? exclusions = null;
            if (root.TryGetProperty("hot_context_section_exclusions", out var exEl) && exEl.ValueKind == JsonValueKind.Array)
            {
                var list = new List<string>();
                foreach (var item in exEl.EnumerateArray())
                {
                    var id = item.ValueKind == JsonValueKind.String ? (item.GetString() ?? "").Trim() : "";
                    if (id.Length == 0)
                        continue;
                    if (Regex.IsMatch(id, "^[A-Za-z0-9._-]+$"))
                        list.Add(id);
                }
                exclusions = list.Count > 0 ? list : null;
            }

            return new MemoryArchitectureManifestData(l0, suffix, warnBudget, critBudget, exclusions);
        }
        catch
        {
            return null;
        }
    }

    private static MemoryArchitectureManifestData? LoadMemoryArchitectureManifest(IReadOnlyDictionary<string, string> sections, string notesPath)
    {
        var memoryArch = sections.GetValueOrDefault("memory-architecture-v1");
        var manifestPath = TryParseManifestRelativePath(memoryArch);
        if (string.IsNullOrWhiteSpace(manifestPath))
            return null;
        return TryLoadMemoryArchitectureManifest(notesPath, manifestPath);
    }

    private static (int Warning, int Critical) ResolveHotBudgetChars(MemoryArchitectureManifestData? manifest)
    {
        var w = manifest?.HotBudgetWarningChars ?? HotContextDefaults.HotContextBudgetWarningChars;
        var c = manifest?.HotBudgetCriticalChars ?? HotContextDefaults.HotContextBudgetCriticalChars;
        if (w < 1)
            w = HotContextDefaults.HotContextBudgetWarningChars;
        if (c < 1)
            c = HotContextDefaults.HotContextBudgetCriticalChars;
        if (w >= c)
        {
            return (HotContextDefaults.HotContextBudgetWarningChars, HotContextDefaults.HotContextBudgetCriticalChars);
        }

        return (w, c);
    }

    private static bool IsHotExcluded(string sectionId, IReadOnlyList<string>? manifestExclusions)
    {
        if (HotContextDefaults.IsBuiltInHotExclusion(sectionId))
            return true;
        if (manifestExclusions is null)
            return false;
        foreach (var x in manifestExclusions)
        {
            if (sectionId.Equals(x, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static IReadOnlyList<string>? ResolveL0Ids(IReadOnlyDictionary<string, string> sections, string notesPath, MemoryArchitectureManifestData? manifest = null)
    {
        manifest ??= LoadMemoryArchitectureManifest(sections, notesPath);
        if (manifest is { L0.Count: > 0 })
            return manifest.L0;

        var memoryArch = sections.GetValueOrDefault("memory-architecture-v1");
        return ParseL0FromMemoryArchitecture(memoryArch);
    }

    private static IReadOnlyList<string>? ResolveCompactOrderSuffix(IReadOnlyDictionary<string, string> sections, string notesPath)
    {
        return LoadMemoryArchitectureManifest(sections, notesPath)?.CompactOrderSuffix;
    }

    private static string[] BuildHotSectionIds(string resolvedScope, IReadOnlyDictionary<string, string> sections, string notesPath, IReadOnlyDictionary<string, string> scopeAliases)
    {
        var manifest = LoadMemoryArchitectureManifest(sections, notesPath);
        var l0 = ResolveL0Ids(sections, notesPath, manifest);
        var ids = (l0 ?? HotContextDefaults.DefaultL0Ids).ToList();
        ids.Add(ResolveScopeSectionId(resolvedScope, sections, scopeAliases));
        return ids.Where(id => !IsHotExcluded(id, manifest?.HotContextSectionExclusions)).Distinct(StringComparer.Ordinal).ToArray();
    }

    private static int CountLines(string content)
    {
        if (string.IsNullOrEmpty(content))
            return 0;
        return content.Replace("\r\n", "\n").Split('\n').Length;
    }

    private static string[] TokenizeQuery(string query)
    {
        var tokens = Regex.Split(query.ToLowerInvariant(), @"[^a-zа-я0-9._-]+")
            .Where(token => token.Length >= 3)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return tokens.Length > 0 ? tokens : [query.ToLowerInvariant()];
    }

    private static int CountMatches(string text, IReadOnlyList<string> tokens)
    {
        var normalized = text.ToLowerInvariant();
        var count = 0;
        foreach (var token in tokens)
        {
            if (normalized.Contains(token, StringComparison.Ordinal))
                count++;
        }

        return count;
    }

    private static string BuildPreview(string content, int maxChars)
    {
        var normalized = Regex.Replace(content.Replace("\r\n", "\n"), @"\s+", " ").Trim();
        if (normalized.Length <= maxChars)
            return normalized;
        return normalized[..maxChars] + "...";
    }

    /// <summary>Reads <c>knowledge/work/local/scope-alias-map-v1.md</c> under canon (same layer as workspace-scope-map). No hardcoded alias table in code.</summary>
    private static IReadOnlyDictionary<string, string> LoadScopeAliasesMerged()
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var root = ResolveCanonPath(null);
            MergeScopeAliasFile(Path.Combine(root, KnowledgeDirName, "work", "local", "scope-alias-map-v1.md"), dict);
        }
        catch (ArgumentException)
        {
            // Canon cannot be resolved — no alias file (e.g. misconfigured env in edge cases).
        }
        catch (IOException)
        {
        }

        return dict;
    }

    private static void MergeScopeAliasFile(string path, IDictionary<string, string> sink)
    {
        if (!File.Exists(path))
            return;

        var text = File.ReadAllText(path, Encoding.UTF8);
        foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
        {
            var parsed = ParseScopeMapLine(line);
            if (parsed is null || !LooksLikeScopeAliasKey(parsed.Value.Item1))
                continue;

            var alias = parsed.Value.Item1.Trim().ToLowerInvariant();
            var canonical = parsed.Value.Item2.Trim().ToLowerInvariant();
            if (alias.Length != 0 && canonical.Length != 0)
                sink[alias] = canonical;
        }
    }

    /// <summary>Alias keys must be single tokens — not filesystem paths (<c>c:\...</c>). Workspace lines in a mis-placed alias file are ignored.</summary>
    private static bool LooksLikeScopeAliasKey(string key)
    {
        var t = key.Trim();
        if (t.Length == 0)
            return false;
        foreach (var c in t)
        {
            if (char.IsLetterOrDigit(c) || c is '.' or '_' or '-')
                continue;
            return false;
        }

        return true;
    }

    /// <summary>Maps legacy shorthand to canonical ids when defined in merged alias dictionary.</summary>
    private static string NormalizeScope(string scope, IReadOnlyDictionary<string, string> aliases)
    {
        var s = scope.Trim().ToLowerInvariant();
        return aliases.TryGetValue(s, out var mapped) ? mapped : s;
    }

    private static string ResolveScope(string? requestedScope, IReadOnlyDictionary<string, string> sections, string workspacePath, IReadOnlyDictionary<string, string> aliases)
    {
        if (!string.IsNullOrWhiteSpace(requestedScope))
            return NormalizeScope(requestedScope, aliases);

        var mappedScope = TryResolveScopeFromWorkspaceMap(workspacePath, sections);
        if (!string.IsNullOrWhiteSpace(mappedScope))
            return NormalizeScope(mappedScope, aliases);

        if (!sections.TryGetValue("active-scope", out var activeScopeContent))
            return NormalizeScope("door-to-singularity", aliases);

        var match = Regex.Match(activeScopeContent, @"current\s*:\s*(?<scope>[A-Za-z0-9._-]+)", RegexOptions.IgnoreCase);
        var raw = match.Success
            ? match.Groups["scope"].Value.Trim().ToLowerInvariant()
            : "door-to-singularity";
        return NormalizeScope(raw, aliases);
    }

    private static string? TryResolveScopeFromWorkspaceMap(string workspacePath, IReadOnlyDictionary<string, string> sections)
    {
        // Prefer machine-local map under canon (single source); else hot sections (legacy).
        var fromFile = TryLoadWorkspaceScopeMapFromWorkLocal();
        var sectionPrimary = sections.TryGetValue("workspace-scope-map-v1", out var pm) ? pm : null;
        var sectionLegacy = sections.TryGetValue("scope-map-v1", out var lm) ? lm : null;
        var mapContent = !string.IsNullOrWhiteSpace(fromFile)
            ? fromFile
            : !string.IsNullOrWhiteSpace(sectionPrimary)
                ? sectionPrimary
                : !string.IsNullOrWhiteSpace(sectionLegacy)
                    ? sectionLegacy
                    : null;

        if (string.IsNullOrWhiteSpace(mapContent))
            return null;

        var normalizedWorkspace = NormalizePathKey(workspacePath);
        var lines = mapContent.Replace("\r\n", "\n").Split('\n');
        string? bestScope = null;
        var bestKeyLength = -1;
        foreach (var line in lines)
        {
            var parsed = ParseScopeMapLine(line);
            if (parsed is null)
                continue;

            var (workspaceKey, scope) = parsed.Value;
            var normalizedKey = NormalizePathKey(workspaceKey);
            if (!IsPrefixPathMatch(normalizedWorkspace, normalizedKey))
                continue;

            if (normalizedKey.Length <= bestKeyLength)
                continue;

            bestKeyLength = normalizedKey.Length;
            bestScope = scope;
        }

        return bestScope;
    }

    /// <summary>Optional map lines (same format as hot section): <c>knowledge/work/local/workspace-scope-map-v1.md</c> under canon root. Overrides empty/missing hot sections when <see cref="ResolveCanonPath"/> succeeds.</summary>
    private static string? TryLoadWorkspaceScopeMapFromWorkLocal()
    {
        try
        {
            var root = ResolveCanonPath(null);
            var path = Path.Combine(root, "knowledge", "work", "local", "workspace-scope-map-v1.md");
            if (!File.Exists(path))
                return null;
            return File.ReadAllText(path, Encoding.UTF8);
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static (string workspaceKey, string scope)? ParseScopeMapLine(string rawLine)
    {
        var line = rawLine.Trim();
        if (line.Length == 0 || line.StartsWith('#'))
            return null;

        if (line.StartsWith('-'))
            line = line[1..].Trim();

        var arrowParts = line.Split("=>", StringSplitOptions.TrimEntries);
        if (arrowParts.Length == 2)
            return (arrowParts[0], arrowParts[1].ToLowerInvariant());

        var colonParts = line.Split(':', 2, StringSplitOptions.TrimEntries);
        if (colonParts.Length == 2)
            return (colonParts[0], colonParts[1].ToLowerInvariant());

        var eqParts = line.Split('=', 2, StringSplitOptions.TrimEntries);
        if (eqParts.Length == 2)
            return (eqParts[0], eqParts[1].ToLowerInvariant());

        return null;
    }

    private static string NormalizePathKey(string path) =>
        path.Trim().Replace('/', '\\').TrimEnd('\\');

    private static string ResolveDtsDefaultSectionId(IReadOnlyDictionary<string, string> sections)
    {
        if (sections.ContainsKey("scope-door-to-singularity"))
            return "scope-door-to-singularity";
        if (sections.ContainsKey("scope-current-projects"))
            return "scope-current-projects";
        return "scope-door-to-singularity";
    }

    private static string ResolveScopeSectionId(string resolvedScope, IReadOnlyDictionary<string, string> sections, IReadOnlyDictionary<string, string> aliases)
    {
        if (string.IsNullOrWhiteSpace(resolvedScope))
            return ResolveDtsDefaultSectionId(sections);

        var normalizedScope = NormalizeScope(resolvedScope.Trim(), aliases);
        var genericScopeId = $"scope-{normalizedScope}";
        if (sections.ContainsKey(genericScopeId))
            return genericScopeId;

        if (normalizedScope == "door-to-singularity" && sections.ContainsKey("scope-current-projects"))
            return "scope-current-projects";

        return genericScopeId;
    }

    private static bool IsPrefixPathMatch(string workspacePath, string mapKeyPath)
    {
        if (string.Equals(workspacePath, mapKeyPath, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!workspacePath.StartsWith(mapKeyPath, StringComparison.OrdinalIgnoreCase))
            return false;

        return workspacePath.Length > mapKeyPath.Length && workspacePath[mapKeyPath.Length] == '\\';
    }

    private static string CompactNotes(string notes, string notesPath)
    {
        var sections = ParseSections(notes);
        if (sections.Count == 0)
            return NormalizeWhitespace(notes);

        var l0 = ResolveL0Ids(sections, notesPath);
        var startIds = (l0 ?? HotContextDefaults.DefaultL0Ids).ToList();
        var manifestSuffix = ResolveCompactOrderSuffix(sections, notesPath);
        var suffixSeed = manifestSuffix?.ToArray() ?? HotContextDefaults.DefaultCompactOrderSuffix;
        var suffixIds = suffixSeed.Where(id => !startIds.Contains(id, StringComparer.Ordinal));
        var preferredOrder = startIds.Concat(suffixIds).ToArray();

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

    private static IReadOnlyList<string> ValidateMemoryArchitecture(IReadOnlyDictionary<string, string> sections, string notesPath)
    {
        var warnings = new List<string>();
        var memoryArch = sections.GetValueOrDefault("memory-architecture-v1");
        var manifestRel = TryParseManifestRelativePath(memoryArch);
        if (string.IsNullOrWhiteSpace(manifestRel))
            return warnings;

        var fullPath = TryResolveManifestFullPath(notesPath, manifestRel);
        if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
        {
            warnings.Add("memory_arch_manifest_missing");
            return warnings;
        }

        var manifest = TryLoadMemoryArchitectureManifest(notesPath, manifestRel);
        if (manifest is null)
        {
            warnings.Add("memory_arch_manifest_invalid_json");
            return warnings;
        }

        var ids = manifest.L0;
        if (ids.Count == 0)
        {
            warnings.Add("memory_arch_manifest_l0_empty");
            return warnings;
        }

        var missing = ids.Where(id => !sections.ContainsKey(id)).Take(8).ToArray();
        if (missing.Length > 0)
            warnings.Add("memory_arch_manifest_missing_sections:" + string.Join(",", missing));

        return warnings;
    }

    private static string NormalizeWhitespace(string text)
    {
        var normalized = text.Replace("\r\n", "\n");
        normalized = Regex.Replace(normalized, @"\n{3,}", "\n\n");
        return normalized.EndsWith('\n') ? normalized : normalized + "\n";
    }
}
