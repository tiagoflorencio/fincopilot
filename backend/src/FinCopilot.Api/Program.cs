using System.Text.Json;
using Anthropic;
using FinCopilot.Api.Agent;
using FinCopilot.Api.Tools;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection("Agent"));

// A chave vem de ANTHROPIC_API_KEY ou de "Anthropic:ApiKey" (dotnet user-secrets). Nunca no código.
builder.Services.AddSingleton(_ => builder.Configuration["Anthropic:ApiKey"] is { Length: > 0 } apiKey
    ? new AnthropicClient { ApiKey = apiKey }
    : new AnthropicClient());

builder.Services.AddHttpClient<StockQuoteTool>(c => c.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddHttpClient<EconomicIndicatorTool>(c => c.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddTransient<IAgentTool>(sp => sp.GetRequiredService<StockQuoteTool>());
builder.Services.AddTransient<IAgentTool>(sp => sp.GetRequiredService<EconomicIndicatorTool>());
builder.Services.AddTransient<IAgentTool, CompoundInterestTool>();

builder.Services.AddSingleton<ConversationStore>();
builder.Services.AddScoped<AgentRunner>();

var app = builder.Build();

var sseJson = new JsonSerializerOptions(JsonSerializerDefaults.Web);

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/chat", async (ChatRequest request, HttpContext http, ConversationStore store, AgentRunner agent) =>
{
    if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Length > 4000)
    {
        http.Response.StatusCode = StatusCodes.Status400BadRequest;
        await http.Response.WriteAsJsonAsync(new { error = "A mensagem deve ter entre 1 e 4000 caracteres." });
        return;
    }

    var conversation = store.GetOrCreate(request.ConversationId);
    if (!await conversation.Lock.WaitAsync(0))
    {
        http.Response.StatusCode = StatusCodes.Status409Conflict;
        await http.Response.WriteAsJsonAsync(new { error = "Esta conversa ainda está processando a mensagem anterior." });
        return;
    }

    try
    {
        http.Response.ContentType = "text/event-stream";
        http.Response.Headers.CacheControl = "no-cache";
        http.Response.Headers["X-Accel-Buffering"] = "no";

        // Ferramentas rodam em paralelo e emitem eventos; a escrita no stream precisa ser serializada.
        var writeLock = new SemaphoreSlim(1, 1);
        async Task Emit(AgentEvent evt)
        {
            await writeLock.WaitAsync(http.RequestAborted);
            try
            {
                var json = JsonSerializer.Serialize(evt, evt.GetType(), sseJson);
                await http.Response.WriteAsync($"data: {json}\n\n", http.RequestAborted);
                await http.Response.Body.FlushAsync(http.RequestAborted);
            }
            finally
            {
                writeLock.Release();
            }
        }

        await Emit(new ConversationStartedEvent(conversation.Id));
        await agent.RunAsync(conversation, request.Message.Trim(), Emit, http.RequestAborted);
    }
    catch (OperationCanceledException) when (http.RequestAborted.IsCancellationRequested)
    {
        // Cliente fechou a conexão.
    }
    finally
    {
        conversation.Lock.Release();
    }
});

app.Run();

public sealed record ChatRequest(string Message, string? ConversationId);
