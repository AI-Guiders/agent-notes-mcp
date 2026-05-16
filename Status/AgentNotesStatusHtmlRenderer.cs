using System.Net;
using System.Text;

namespace AgentNotesMcp.Status;

internal static class AgentNotesStatusHtmlRenderer
{
    internal static string Render(AgentNotesStatusSnapshot snapshot, string? workspaceQuery)
    {
        var health = snapshot.MemoryHealth;
        var healthClass = health?.HealthLevel switch
        {
            "good" => "pill pill-good",
            "warning" => "pill pill-warn",
            "critical" => "pill pill-crit",
            _ => "pill pill-muted"
        };
        var healthLabel = health?.HealthLevel ?? "no workspace";

        var sb = new StringBuilder(28_000);
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"ru\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"utf-8\" />");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />");
        sb.AppendLine("  <title>Agent Notes MCP — Status</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine(Css);
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <div class=\"wrap\">");

        if (snapshot.BindWarning is not null)
            sb.AppendLine($"    <div class=\"warn-banner\">{E(snapshot.BindWarning)}</div>");

        sb.AppendLine("    <header class=\"hero\">");
        sb.AppendLine("      <div class=\"hero-inner\">");
        sb.AppendLine("        <p class=\"eyebrow\">AgentNotesStatus</p>");
        sb.AppendLine($"        <h1>agent-notes-mcp <span class=\"ver\">v{E(snapshot.McpVersion)}</span></h1>");
        sb.AppendLine("        <p class=\"tagline\">Диагностика процесса MCP: knowledge root, scope, hot-context. Только loopback, read-only.</p>");
        sb.AppendLine("        <div class=\"hero-meta\">");
        sb.AppendLine($"          <span class=\"{healthClass}\">{E(healthLabel)}</span>");
        sb.AppendLine($"          <span class=\"pill pill-muted\">PID {snapshot.ProcessId}</span>");
        sb.AppendLine($"          <span class=\"pill pill-muted\">uptime {FormatUptime(snapshot.UptimeSeconds)}</span>");
        sb.AppendLine("        </div>");
        sb.AppendLine("        <div class=\"hero-actions\">");
        sb.AppendLine("          <a class=\"btn btn-ghost\" href=\"/status.json\">status.json</a>");
        sb.AppendLine("          <a class=\"btn btn-ghost\" href=\"/status.json?verbose=1\">verbose JSON</a>");
        sb.AppendLine($"          <a class=\"btn btn-ghost\" href=\"{E(BuildHotPreviewHref(workspaceQuery ?? snapshot.Workspace.EffectiveWorkspace))}\">hot-preview</a>");
        sb.AppendLine("          <a class=\"btn btn-ghost\" href=\"/tools\">MCP tools</a>");
        sb.AppendLine("          <a class=\"btn btn-primary\" href=\"/health\">health</a>");
        sb.AppendLine("        </div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </header>");

        sb.AppendLine("    <section class=\"card form-card\" style=\"margin-bottom:1rem\">");
        sb.AppendLine("      <h2>Workspace preview</h2>");
        sb.AppendLine("      <form method=\"get\" action=\"/\">");
        sb.AppendLine("        <input type=\"text\" name=\"workspace_path\" placeholder=\"D:/path/to/workspace\" ");
        sb.AppendLine($"               value=\"{E(workspaceQuery ?? snapshot.Workspace.PreviewWorkspace ?? "")}\" />");
        sb.AppendLine("        <button type=\"submit\">Показать memory_health</button>");
        sb.AppendLine("      </form>");
        if (health is null && string.IsNullOrWhiteSpace(snapshot.Workspace.EffectiveWorkspace))
            sb.AppendLine("      <p class=\"empty\" style=\"margin-top:0.75rem\">Укажи workspace или задай <code>[status.preview].workspace</code> в TOML.</p>");
        sb.AppendLine("    </section>");

        AppendToolsStrip(sb, snapshot.Tools);

        sb.AppendLine("    <div class=\"grid\">");
        AppendCard(sb, "Конфигурация", [
            ("Config", $"<code class=\"path\">{E(snapshot.ConfigPath)}</code>"),
            ("Status URL", $"<code class=\"path\">{E(snapshot.StatusUrl)}</code>"),
            ("Started (UTC)", E(snapshot.StartedAtUtc.ToString("yyyy-MM-dd HH:mm:ss")))
        ]);

        AppendCard(sb, "Knowledge", [
            ("Primary root", $"<code class=\"path\">{E(snapshot.Knowledge.PrimaryRoot)}</code>"),
            ("agent-notes.md", $"{YesNo(snapshot.Knowledge.NotesExists)} — <code class=\"path\">{E(snapshot.Knowledge.NotesPath)}</code>"),
            ("Read-only routing", snapshot.Knowledge.ReadOnlyRoutingEnabled ? "включён" : "<span class=\"muted\">выкл (2.0)</span>"),
            ("Read-only roots", FormatReadOnlyRoots(snapshot.Knowledge.ReadOnlyRoots))
        ]);

        AppendCard(sb, "Workspace / scope", [
            ("Effective workspace", snapshot.Workspace.EffectiveWorkspace is null
                ? "<span class=\"empty\">—</span>"
                : $"<code class=\"path\">{E(snapshot.Workspace.EffectiveWorkspace)}</code>"),
            ("Default scope", E(snapshot.Workspace.DefaultScope)),
            ("Resolved scope", health is not null
                ? E(health.ResolvedScope)
                : "<span class=\"empty\">—</span>"),
            ("scope_map", $"{YesNo(snapshot.Workspace.ScopeMapExists)} — <code>{E(snapshot.Workspace.ScopeMapRelative)}</code>"),
            ("scope_aliases", $"{YesNo(snapshot.Workspace.ScopeAliasMapExists)} — <code>{E(snapshot.Workspace.ScopeAliasMapRelative)}</code>")
        ]);

        if (health is not null)
        {
            var hotSummary = $"{health.HotChars:N0} chars · {health.HotLines} lines · {health.SectionCount} sections";
            var warningsHtml = health.Warnings.Count == 0
                ? "<span class=\"empty\">нет</span>"
                : string.Join("<br/>", health.Warnings.Select(w => E(w)));
            var recommendationsHtml = string.Join("<br/>", health.Recommendations.Select(r => E(r)));
            var memoryRows = new (string Label, string Value)[]
            {
                ("Level", $"<span class=\"{healthClass}\">{E(health.HealthLevel)}</span>"),
                ("Hot context", hotSummary),
                ("Hot sections", FormatChips(health.HotSectionIds)),
                ("Warnings", warningsHtml),
                ("Recommendations", recommendationsHtml)
            };
            AppendCard(sb, "Memory health", memoryRows);
        }

        sb.AppendLine("    </div>");

        AppendRecentToolCalls(sb, snapshot.RecentToolCalls);
        sb.AppendLine("    <footer>ADR 013 · AgentNotesStatus · loopback only</footer>");
        sb.AppendLine("  </div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    internal static string RenderToolsPage(IReadOnlyList<AgentNotesStatusSnapshot.ToolSummary> tools)
    {
        var sb = new StringBuilder(32_000);
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"ru\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"utf-8\" />");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />");
        sb.AppendLine("  <title>Agent Notes MCP — Tools</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine(Css);
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <div class=\"wrap\">");
        sb.AppendLine("    <nav class=\"page-nav\">");
        sb.AppendLine("      <a class=\"btn btn-ghost\" href=\"/\">← Status</a>");
        sb.AppendLine("    </nav>");
        sb.AppendLine("    <header class=\"hero\" style=\"margin-bottom:1rem\">");
        sb.AppendLine("      <div class=\"hero-inner\">");
        sb.AppendLine("        <p class=\"eyebrow\">AgentNotesStatus</p>");
        sb.AppendLine($"        <h1>MCP tools <span class=\"ver\">({tools.Count})</span></h1>");
        sb.AppendLine("        <p class=\"tagline\">Каталог tools этого процесса — имена и описания из ToolCatalog.</p>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </header>");
        sb.AppendLine("    <section class=\"card\">");
        sb.AppendLine("      <table class=\"tools-catalog\">");
        sb.AppendLine("        <thead><tr><th>Tool</th><th>Description</th></tr></thead>");
        sb.AppendLine("        <tbody>");
        foreach (var tool in tools.OrderBy(t => t.Name, StringComparer.Ordinal))
        {
            sb.AppendLine("          <tr>");
            sb.AppendLine($"            <td class=\"tool-name\"><a id=\"{E(tool.Name)}\" href=\"#{E(tool.Name)}\"><code>{E(tool.Name)}</code></a></td>");
            sb.AppendLine($"            <td class=\"tool-desc\">{E(tool.Description)}</td>");
            sb.AppendLine("          </tr>");
        }

        sb.AppendLine("        </tbody>");
        sb.AppendLine("      </table>");
        sb.AppendLine("    </section>");
        sb.AppendLine("    <footer>ADR 013 · read-only catalog</footer>");
        sb.AppendLine("  </div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    private static void AppendToolsStrip(StringBuilder sb, IReadOnlyList<AgentNotesStatusSnapshot.ToolSummary> tools)
    {
        sb.AppendLine("    <section class=\"card tools-strip\">");
        sb.AppendLine($"      <h2>MCP tools <span class=\"ver\">({tools.Count})</span></h2>");
        sb.AppendLine("      <p class=\"muted\" style=\"margin:0 0 0.75rem\">Как hot sections — только имена; описания на отдельной странице.</p>");
        sb.AppendLine("      <div class=\"list-chips\">");
        foreach (var tool in tools.OrderBy(t => t.Name, StringComparer.Ordinal))
            sb.AppendLine($"        <a class=\"chip chip-link\" href=\"/tools#{E(tool.Name)}\">{E(tool.Name)}</a>");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <p style=\"margin:0.85rem 0 0\"><a class=\"btn btn-ghost\" href=\"/tools\">Таблица с описаниями →</a></p>");
        sb.AppendLine("    </section>");
    }

    private static void AppendCard(StringBuilder sb, string title, (string Label, string Value)[] rows)
    {
        sb.AppendLine("      <section class=\"card\">");
        sb.AppendLine($"        <h2>{E(title)}</h2>");
        sb.AppendLine("        <dl>");
        foreach (var (label, value) in rows)
        {
            sb.AppendLine("          <div class=\"row\">");
            sb.AppendLine($"            <dt>{E(label)}</dt>");
            sb.AppendLine($"            <dd>{value}</dd>");
            sb.AppendLine("          </div>");
        }

        sb.AppendLine("        </dl>");
        sb.AppendLine("      </section>");
    }

    private static string FormatReadOnlyRoots(IReadOnlyList<AgentNotesStatusSnapshot.ReadOnlyRoot> roots)
    {
        if (roots.Count == 0)
            return "<span class=\"empty\">не настроены</span>";

        return string.Join("<br/>", roots.Select(r =>
            $"<code>{E(r.Id)}</code> {YesNo(r.Exists)} — <code class=\"path\">{E(r.Path)}</code>"));
    }

    private static string BuildHotPreviewHref(string? workspace)
    {
        if (string.IsNullOrWhiteSpace(workspace))
            return "/hot-preview";
        return "/hot-preview?workspace_path=" + Uri.EscapeDataString(workspace);
    }

    private static void AppendRecentToolCalls(StringBuilder sb, IReadOnlyList<AgentNotesStatusSnapshot.RecentToolCall> calls)
    {
        sb.AppendLine("    <section class=\"card\" style=\"margin-top:1rem\">");
        sb.AppendLine($"      <h2>Recent tool calls ({calls.Count})</h2>");
        if (calls.Count == 0)
        {
            sb.AppendLine("      <p class=\"empty\">Пока нет вызовов в этом процессе MCP.</p>");
            sb.AppendLine("    </section>");
            return;
        }

        sb.AppendLine("      <table class=\"calls\">");
        sb.AppendLine("        <thead><tr><th>UTC</th><th>Tool</th><th>ms</th><th>Preview</th></tr></thead>");
        sb.AppendLine("        <tbody>");
        foreach (var call in calls.Take(32))
        {
            var rowClass = call.IsError ? "err" : "";
            sb.AppendLine($"          <tr class=\"{rowClass}\">");
            sb.AppendLine($"            <td>{E(call.AtUtc.ToString("HH:mm:ss"))}</td>");
            sb.AppendLine($"            <td><code>{E(call.ToolName)}</code></td>");
            sb.AppendLine($"            <td>{call.DurationMs}</td>");
            sb.AppendLine($"            <td>{E(call.ResultPreview)}</td>");
            sb.AppendLine("          </tr>");
        }

        sb.AppendLine("        </tbody>");
        sb.AppendLine("      </table>");
        sb.AppendLine("    </section>");
    }

    private static string FormatChips(IReadOnlyList<string> ids)
    {
        if (ids.Count == 0)
            return "<span class=\"empty\">—</span>";

        return "<div class=\"list-chips\">" +
               string.Join("", ids.Select(id => $"<span class=\"chip\">{E(id)}</span>")) +
               "</div>";
    }

    private static string E(string? value) => WebUtility.HtmlEncode(value ?? "");

    private static string YesNo(bool value) =>
        value ? "<span style=\"color:var(--good)\">да</span>" : "<span class=\"empty\">нет</span>";

    private static string FormatUptime(long seconds)
    {
        if (seconds < 60) return $"{seconds}s";
        var m = seconds / 60;
        if (m < 60) return $"{m}m {seconds % 60}s";
        var h = m / 60;
        return $"{h}h {m % 60}m";
    }

    private const string Css = """
        :root {
          --bg: #0a0d12;
          --bg-elevated: #12171f;
          --card: #151b24;
          --border: #273041;
          --text: #e9eef5;
          --muted: #8d99ae;
          --accent: #5ec4e0;
          --accent-dim: #3a8fa8;
          --good: #45b784;
          --warn: #d4a84b;
          --crit: #e07070;
          --mono: "Cascadia Code", "SF Mono", Consolas, monospace;
          --sans: "Segoe UI", system-ui, -apple-system, sans-serif;
        }
        * { box-sizing: border-box; }
        body {
          margin: 0;
          min-height: 100vh;
          font-family: var(--sans);
          color: var(--text);
          background:
            radial-gradient(ellipse 80% 50% at 50% -20%, rgba(94, 196, 224, 0.12), transparent),
            var(--bg);
          line-height: 1.5;
        }
        .wrap { max-width: 1080px; margin: 0 auto; padding: 2rem 1.25rem 3rem; }
        .hero {
          position: relative;
          padding: 1.75rem 1.5rem;
          border: 1px solid var(--border);
          border-radius: 16px;
          background: linear-gradient(145deg, var(--card) 0%, var(--bg-elevated) 100%);
          margin-bottom: 1.5rem;
          overflow: hidden;
        }
        .hero::before {
          content: "";
          position: absolute;
          inset: 0;
          background: radial-gradient(circle at 100% 0%, rgba(94, 196, 224, 0.08), transparent 55%);
          pointer-events: none;
        }
        .hero-inner { position: relative; z-index: 1; }
        .eyebrow {
          margin: 0 0 0.35rem;
          font-size: 0.72rem;
          font-weight: 600;
          letter-spacing: 0.14em;
          text-transform: uppercase;
          color: var(--accent);
        }
        h1 { margin: 0 0 0.5rem; font-size: 1.65rem; font-weight: 650; letter-spacing: -0.02em; }
        h1 .ver { color: var(--muted); font-weight: 500; font-size: 1rem; }
        .tagline { margin: 0 0 1rem; color: var(--muted); font-size: 0.95rem; max-width: 52ch; }
        .hero-meta { display: flex; flex-wrap: wrap; gap: 0.5rem; margin-bottom: 1rem; }
        .pill {
          display: inline-block;
          padding: 0.2rem 0.65rem;
          border-radius: 999px;
          font-size: 0.78rem;
          font-weight: 600;
          border: 1px solid var(--border);
          background: rgba(0,0,0,0.2);
        }
        .pill-good { border-color: rgba(69, 183, 132, 0.5); color: #9ce8c3; }
        .pill-warn { border-color: rgba(212, 168, 75, 0.5); color: #f0d59a; }
        .pill-crit { border-color: rgba(224, 112, 112, 0.5); color: #f5b0b0; }
        .pill-muted { color: var(--muted); }
        .hero-actions { display: flex; flex-wrap: wrap; gap: 0.5rem; }
        .btn {
          display: inline-block;
          padding: 0.45rem 0.9rem;
          border-radius: 8px;
          font-size: 0.85rem;
          font-weight: 600;
          text-decoration: none;
          border: 1px solid var(--border);
        }
        .btn-ghost { color: var(--text); background: rgba(255,255,255,0.03); }
        .btn-ghost:hover { border-color: var(--accent-dim); color: var(--accent); }
        .btn-primary { color: #0a0d12; background: var(--accent); border-color: var(--accent); }
        .grid {
          display: grid;
          grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
          gap: 1rem;
        }
        .card {
          border: 1px solid var(--border);
          border-radius: 12px;
          background: var(--card);
          padding: 1.1rem 1.2rem;
        }
        .card h2 {
          margin: 0 0 0.85rem;
          font-size: 0.82rem;
          font-weight: 650;
          letter-spacing: 0.06em;
          text-transform: uppercase;
          color: var(--muted);
        }
        dl { margin: 0; }
        .row {
          display: grid;
          grid-template-columns: 7.5rem 1fr;
          gap: 0.35rem 0.75rem;
          padding: 0.4rem 0;
          border-bottom: 1px solid rgba(39, 48, 65, 0.6);
          font-size: 0.88rem;
        }
        .row:last-child { border-bottom: none; }
        dt { margin: 0; color: var(--muted); }
        dd { margin: 0; word-break: break-word; }
        code, .path {
          font-family: var(--mono);
          font-size: 0.8rem;
          color: #c5d4e8;
        }
        .warn-banner {
          margin-bottom: 1rem;
          padding: 0.65rem 0.9rem;
          border-radius: 8px;
          border: 1px solid rgba(212, 168, 75, 0.45);
          background: rgba(212, 168, 75, 0.08);
          color: #f0d59a;
          font-size: 0.88rem;
        }
        .form-card form { display: flex; flex-wrap: wrap; gap: 0.5rem; align-items: center; }
        .form-card input[type=text] {
          flex: 1 1 220px;
          min-width: 0;
          padding: 0.5rem 0.65rem;
          border-radius: 8px;
          border: 1px solid var(--border);
          background: var(--bg);
          color: var(--text);
          font-family: var(--mono);
          font-size: 0.82rem;
        }
        .form-card button {
          padding: 0.5rem 1rem;
          border-radius: 8px;
          border: none;
          background: var(--accent);
          color: #0a0d12;
          font-weight: 600;
          cursor: pointer;
        }
        ul.tools { margin: 0; padding: 0; list-style: none; max-height: 280px; overflow: auto; }
        ul.tools li {
          padding: 0.45rem 0;
          border-bottom: 1px solid rgba(39, 48, 65, 0.5);
          font-size: 0.84rem;
        }
        ul.tools li:last-child { border-bottom: none; }
        ul.tools strong { color: var(--accent); font-family: var(--mono); font-size: 0.8rem; }
        ul.tools span { display: block; color: var(--muted); margin-top: 0.15rem; font-size: 0.78rem; }
        .list-chips { display: flex; flex-wrap: wrap; gap: 0.35rem; margin-top: 0.35rem; }
        .chip {
          font-family: var(--mono);
          font-size: 0.72rem;
          padding: 0.15rem 0.45rem;
          border-radius: 6px;
          background: rgba(94, 196, 224, 0.1);
          border: 1px solid rgba(94, 196, 224, 0.25);
          color: #a8dce8;
        }
        a.chip-link {
          text-decoration: none;
          display: inline-block;
        }
        a.chip-link:hover {
          background: rgba(94, 196, 224, 0.22);
          border-color: var(--accent);
          color: #d4f1f8;
        }
        .tools-strip { margin-bottom: 1rem; }
        .page-nav { margin-bottom: 1rem; }
        table.tools-catalog {
          width: 100%;
          border-collapse: collapse;
          font-size: 0.86rem;
        }
        table.tools-catalog th {
          text-align: left;
          color: var(--muted);
          font-weight: 650;
          padding: 0.5rem 0.65rem;
          border-bottom: 1px solid var(--border);
          vertical-align: bottom;
        }
        table.tools-catalog td {
          padding: 0.55rem 0.65rem;
          border-bottom: 1px solid rgba(39, 48, 65, 0.55);
          vertical-align: top;
        }
        table.tools-catalog tr:hover td {
          background: rgba(255, 255, 255, 0.02);
        }
        table.tools-catalog td.tool-name {
          width: 16rem;
          white-space: nowrap;
        }
        table.tools-catalog td.tool-name code {
          color: var(--accent);
        }
        table.tools-catalog td.tool-desc {
          color: var(--text);
          line-height: 1.45;
        }
        .empty, .muted { color: var(--muted); font-size: 0.88rem; }
        footer {
          margin-top: 2rem;
          text-align: center;
          font-size: 0.75rem;
          color: var(--muted);
        }
        table.calls {
          width: 100%;
          border-collapse: collapse;
          font-size: 0.8rem;
        }
        table.calls th {
          text-align: left;
          color: var(--muted);
          font-weight: 600;
          padding: 0.35rem 0.5rem;
          border-bottom: 1px solid var(--border);
        }
        table.calls td {
          padding: 0.35rem 0.5rem;
          border-bottom: 1px solid rgba(39, 48, 65, 0.45);
          vertical-align: top;
        }
        table.calls tr.err td { color: #f5b0b0; }
        table.calls code { font-size: 0.78rem; }
        """;
}
