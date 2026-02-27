using System.Collections.Frozen;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

var storage = new NotesStorage();
var handlers = new ToolHandlers(storage);
var tools = ToolCatalog.Build();

var options = new McpServerOptions
{
    ServerInfo = new Implementation { Name = "AgentNotesMcp", Version = "0.2.0" },
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

            try
            {
                var text = handlers.Handle(name, args);
                var isError = handlers.IsWriteLikeTool(name) && text != "OK" && !text.StartsWith("NO_CHANGES", StringComparison.Ordinal);

                return ValueTask.FromResult(new CallToolResult
                {
                    Content = [new TextContentBlock { Text = text }],
                    IsError = isError
                });
            }
            catch (ArgumentException ex)
            {
                return ValueTask.FromResult(new CallToolResult
                {
                    Content = [new TextContentBlock { Text = $"Error: {ex.Message}" }],
                    IsError = true
                });
            }
            catch (Exception ex)
            {
                return ValueTask.FromResult(new CallToolResult
                {
                    Content = [new TextContentBlock { Text = "Error: " + ex.Message }],
                    IsError = true
                });
            }
        }
    }
};

var transport = new StdioServerTransport("AgentNotesMcp");
await using var server = McpServer.Create(transport, options);
await server.RunAsync();
return 0;
