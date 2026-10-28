using System.Text.Json;

namespace FinCopilot.Api.Agent;

/// <summary>
/// Eventos enviados ao frontend via SSE. Cada um vira uma linha "data: {json}".
/// O campo Type é o discriminador lido pelo cliente.
/// </summary>
public abstract record AgentEvent(string Type);

public sealed record ConversationStartedEvent(string ConversationId) : AgentEvent("conversation");

public sealed record IterationEvent(int Iteration) : AgentEvent("iteration");

public sealed record ThinkingEvent(string Text) : AgentEvent("thinking");

public sealed record ToolCallEvent(string Id, string Name, JsonElement Input) : AgentEvent("tool_call");

public sealed record ToolResultEvent(string Id, string Name, object? Output, bool IsError, long DurationMs) : AgentEvent("tool_result");

/// <param name="IsFinal">false = comentário do modelo entre chamadas de ferramenta (vai para o trace).</param>
public sealed record TextEvent(string Text, bool IsFinal) : AgentEvent("text");

public sealed record UsageEvent(
    long InputTokens,
    long OutputTokens,
    long CacheReadTokens,
    long CacheWriteTokens,
    decimal CostUsd,
    long DurationMs,
    int Iterations) : AgentEvent("usage");

public sealed record ErrorEvent(string Message) : AgentEvent("error");

public sealed record DoneEvent(string StopReason) : AgentEvent("done");
