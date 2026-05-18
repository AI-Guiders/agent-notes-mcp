using System.Collections.Frozen;
using System.Diagnostics;
using System.Text.Json;
using AgentNotes.Core;
using AgentNotesMcp.Status;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

if (AgentNotesBootstrap.IsStatusOnly(args))
    return await AgentNotesStatusOnlyRunner.RunAsync(args);

var startupCode = AgentNotesBootstrap.TryLoadSettings(args, out var localSettings, out var startupError);
if (startupCode != 0)
{
    Console.Error.WriteLine(startupError);
    return startupCode;
}

AgentNotesRuntime.Initialize(localSettings!, AgentNotesBootstrap.LoadedConfigPath);

var storage = new NotesStorage();
var handlers = new ToolHandlers(storage);
var tools = ToolCatalog.Build();

var startedAt = DateTimeOffset.UtcNow;
AgentNotesStatusHost? statusHost = null;
if (AgentNotesStatusHost.TryStartBackground(storage, startedAt, out statusHost, out var statusUrl, out var statusError)
    && statusUrl is not null)
{
    Console.Error.WriteLine($"AgentNotesStatus: {statusUrl}");
    if (localSettings!.Status.PreviewWorkspace is { } preview)
        AgentNotesStatusRuntimeFile.TryWrite(preview, statusUrl, AgentNotesBootstrap.LoadedConfigPath ?? "");
}
else if (localSettings!.Status.Enabled && statusError is not null)
{
    Console.Error.WriteLine($"AgentNotesStatus: failed to start ({statusError}). MCP continues without HTTP status.");
}

var mcpVersion = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "2.1.0";
var options = new McpServerOptions
{
    ServerInfo = new Implementation { Name = "AgentNotesMcp", Version = mcpVersion },
    ProtocolVersion = "2024-11-05",
    Capabilities = new ServerCapabilities
    {
        Tools = new ToolsCapability { ListChanged = false }
    },
    Handlers = new McpServerHandlers
    {
        ListToolsHandler = (_, _) => ValueTask.FromResult(new ListToolsResult { Tools = tools }),
        CallToolHandler = (request, _) =>
        {
            var name = request.Params?.Name ?? "";
            var args = request.Params?.Arguments is IReadOnlyDictionary<string, JsonElement> providedArgs
                ? providedArgs
                : FrozenDictionary<string, JsonElement>.Empty;

            var sw = Stopwatch.StartNew();
            try
            {
                var text = handlers.Handle(name, args);
                var isError = handlers.IsWriteLikeTool(name) && text != "OK" && !text.StartsWith("NO_CHANGES", StringComparison.Ordinal);
                AgentNotesToolCallRingBuffer.Record(name, args, text, isError, sw.ElapsedMilliseconds);

                return ValueTask.FromResult(new CallToolResult
                {
                    Content = [new TextContentBlock { Text = text }],
                    IsError = isError
                });
            }
            catch (ArgumentException ex)
            {
                var message = $"Error: {ex.Message}";
                AgentNotesToolCallRingBuffer.Record(name, args, message, isError: true, sw.ElapsedMilliseconds);
                return ValueTask.FromResult(new CallToolResult
                {
                    Content = [new TextContentBlock { Text = message }],
                    IsError = true
                });
            }
            catch (Exception ex)
            {
                var message = "Error: " + ex.Message;
                AgentNotesToolCallRingBuffer.Record(name, args, message, isError: true, sw.ElapsedMilliseconds);
                return ValueTask.FromResult(new CallToolResult
                {
                    Content = [new TextContentBlock { Text = message }],
                    IsError = true
                });
            }
        }
    }
};

try
{
    var transport = new StdioServerTransport("AgentNotesMcp");
    await using var server = McpServer.Create(transport, options);
    await server.RunAsync();
    return 0;
}
finally
{
    if (statusHost is not null)
        await statusHost.StopAsync();
}
