using System.Diagnostics;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Beta.Messages;
using FinCopilot.Api.Tools;
using Microsoft.Extensions.Options;

namespace FinCopilot.Api.Agent;

public sealed class AgentOptions
{
    public string Model { get; set; } = "claude-opus-5-5";
    public string Effort { get; set; } = "medium";
    public int MaxIterations { get; set; } = 8;
    public int MaxTokens { get; set; } = 16000;
}

/// <summary>
/// Loop agêntico manual: chama o modelo, executa as ferramentas pedidas, devolve os resultados
/// e repete até o modelo responder sem pedir ferramentas (ou até o limite de iterações).
/// Cada passo é emitido como evento para o frontend exibir o trace.
/// </summary>
public sealed class AgentRunner(
    AnthropicClient client,
    IEnumerable<IAgentTool> tools,
    IOptions<AgentOptions> options,
    ILogger<AgentRunner> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AgentOptions _options = options.Value;
    private readonly Dictionary<string, IAgentTool> _tools = tools.ToDictionary(t => t.Name);

    // Definições montadas uma vez e em ordem fixa: qualquer variação invalidaria o cache do prompt.
    private readonly List<BetaToolUnion> _toolDefinitions = tools
        .OrderBy(t => t.Name, StringComparer.Ordinal)
        .Select(t => (BetaToolUnion)new BetaTool
        {
            Name = t.Name,
            Description = t.Description,
            InputSchema = new()
            {
                Properties = t.Properties.ToDictionary(p => p.Key, p => JsonSerializer.SerializeToElement(p.Value)),
                Required = [.. t.Required],
            },
        })
        .ToList();

    public async Task RunAsync(Conversation conversation, string userMessage, Func<AgentEvent, Task> emit, CancellationToken ct)
    {
        var historyStart = conversation.Messages.Count;
        var pricing = ModelPricing.For(_options.Model);
        var stopwatch = Stopwatch.StartNew();
        long input = 0, output = 0, cacheRead = 0, cacheWrite = 0;
        var iteration = 0;
        var stopReason = "unknown";

        conversation.Messages.Add(new BetaUserMessageParam { Content = userMessage });

        try
        {
            while (++iteration <= _options.MaxIterations)
            {
                await emit(new IterationEvent(iteration));

                var response = await client.Beta.Messages.Create(BuildParams(conversation, iteration), ct);

                input += response.Usage.InputTokens;
                output += response.Usage.OutputTokens;
                cacheRead += response.Usage.CacheReadInputTokens ?? 0;
                cacheWrite += response.Usage.CacheCreationInputTokens ?? 0;
                // ApiEnum.ToString() devolve o JSON (com aspas); Raw() devolve o valor ("tool_use").
                stopReason = response.StopReason?.Raw() ?? "unknown";

                if (stopReason == "refusal")
                {
                    // Recusa sem resposta utilizável: descarta só os turnos desta execução (o prefixo fica intacto).
                    conversation.Messages.RemoveRange(historyStart, conversation.Messages.Count - historyStart);
                    await emit(new ErrorEvent("O modelo não pôde responder a essa solicitação. Tente reformular a pergunta."));
                    break;
                }

                var wantsTools = stopReason == "tool_use";
                var (assistantBlocks, toolUses) = await ReadContentAsync(response, isFinal: !wantsTools, emit);

                if (toolUses.Count > 0 && !wantsTools)
                {
                    // Ex.: max_tokens no meio de uma tool call. O bloco pode estar incompleto e
                    // ficaria sem tool_result, então não entra no histórico.
                    conversation.Messages.RemoveRange(historyStart, conversation.Messages.Count - historyStart);
                    await emit(new ErrorEvent("A resposta foi interrompida antes de terminar. Tente novamente."));
                    break;
                }

                conversation.Messages.Add(new BetaAssistantMessageParam { Content = assistantBlocks });

                if (!wantsTools)
                    break;

                // Chamadas independentes rodam em paralelo; todos os resultados voltam numa única mensagem.
                var results = await Task.WhenAll(toolUses.Select(tu => ExecuteToolAsync(tu, emit, ct)));
                conversation.Messages.Add(new BetaUserMessageParam
                {
                    Content = results.Select(r => (BetaContentBlockParam)r).ToList(),
                });
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Falha na execução do agente");
            conversation.Messages.RemoveRange(historyStart, conversation.Messages.Count - historyStart);
            await emit(new ErrorEvent("Erro ao falar com o modelo. Verifique a chave da API e tente novamente."));
            stopReason = "error";
        }

        await emit(new UsageEvent(
            input, output, cacheRead, cacheWrite,
            Math.Round(pricing.Cost(input, output, cacheRead, cacheWrite), 6),
            stopwatch.ElapsedMilliseconds,
            Math.Min(iteration, _options.MaxIterations)));
        await emit(new DoneEvent(stopReason));
    }

    private MessageCreateParams BuildParams(Conversation conversation, int iteration) => new()
    {
        Model = _options.Model,
        MaxTokens = _options.MaxTokens,
        // Breakpoint explícito no fim do prefixo estático (tools + system): garante a reutilização
        // entre conversas diferentes, enquanto o cache automático abaixo cobre o histórico que cresce.
        System = new List<BetaTextBlockParam> { new() { Text = SystemPrompt.Text, CacheControl = new BetaCacheControlEphemeral() } },
        Tools = _toolDefinitions,
        // Na última iteração permitida, o modelo precisa responder com o que já tem.
        ToolChoice = iteration == _options.MaxIterations ? new BetaToolChoiceNone() : new BetaToolChoiceAuto(),
        Messages = [.. conversation.Messages],
        Thinking = new BetaThinkingConfigAdaptive { Display = Display.Summarized },
        OutputConfig = new BetaOutputConfig { Effort = ParseEffort(_options.Effort) },
        // Cache automático da cauda da conversa: reduz custo a partir da 2ª iteração/turno.
        CacheControl = new BetaCacheControlEphemeral(),
        // Se um classificador de segurança recusar, a API tenta o modelo de fallback recomendado.
        Betas = ["server-side-fallback-2026-07-01"],
        Fallbacks = new Default(),
    };

    private static async Task<(List<BetaContentBlockParam> Blocks, List<BetaToolUseBlock> ToolUses)> ReadContentAsync(
        BetaMessage response, bool isFinal, Func<AgentEvent, Task> emit)
    {
        // Não existe .ToParam() no SDK C#: cada bloco da resposta é reconstruído como *Param.
        List<BetaContentBlockParam> blocks = [];
        List<BetaToolUseBlock> toolUses = [];

        foreach (var block in response.Content)
        {
            if (block.TryPickThinking(out var thinking))
            {
                // A assinatura precisa voltar intacta, senão a API rejeita o histórico.
                blocks.Add(new BetaThinkingBlockParam { Thinking = thinking.Thinking, Signature = thinking.Signature });
                if (!string.IsNullOrWhiteSpace(thinking.Thinking))
                    await emit(new ThinkingEvent(thinking.Thinking));
            }
            else if (block.TryPickRedactedThinking(out var redacted))
            {
                blocks.Add(new BetaRedactedThinkingBlockParam { Data = redacted.Data });
            }
            else if (block.TryPickText(out var text))
            {
                blocks.Add(new BetaTextBlockParam { Text = text.Text });
                await emit(new TextEvent(text.Text, isFinal));
            }
            else if (block.TryPickToolUse(out var toolUse))
            {
                blocks.Add(new BetaToolUseBlockParam { ID = toolUse.ID, Name = toolUse.Name, Input = toolUse.Input });
                toolUses.Add(toolUse);
            }
        }

        return (blocks, toolUses);
    }

    private async Task<BetaToolResultBlockParam> ExecuteToolAsync(BetaToolUseBlock toolUse, Func<AgentEvent, Task> emit, CancellationToken ct)
    {
        await emit(new ToolCallEvent(toolUse.ID, toolUse.Name, JsonSerializer.SerializeToElement(toolUse.Input)));
        var stopwatch = Stopwatch.StartNew();

        object? result;
        bool isError;
        try
        {
            if (!_tools.TryGetValue(toolUse.Name, out var tool))
                throw new ToolException($"Ferramenta desconhecida: {toolUse.Name}");

            result = await tool.ExecuteAsync(toolUse.Input, ct);
            isError = false;
        }
        catch (ToolException ex)
        {
            (result, isError) = (new { error = ex.Message }, true);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Falha de rede na ferramenta {Tool}", toolUse.Name);
            (result, isError) = (new { error = "Não foi possível acessar a fonte de dados externa agora." }, true);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Timeout na ferramenta {Tool}", toolUse.Name);
            (result, isError) = (new { error = "A fonte de dados demorou demais para responder." }, true);
        }

        await emit(new ToolResultEvent(toolUse.ID, toolUse.Name, result, isError, stopwatch.ElapsedMilliseconds));

        return new BetaToolResultBlockParam
        {
            ToolUseID = toolUse.ID,
            Content = JsonSerializer.Serialize(result, JsonOptions),
            IsError = isError,
        };
    }

    private static Effort ParseEffort(string effort) => effort.ToLowerInvariant() switch
    {
        "low" => Effort.Low,
        "high" => Effort.High,
        "xhigh" => Effort.Xhigh,
        "max" => Effort.Max,
        _ => Effort.Medium,
    };
}
