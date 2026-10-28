using System.Text.Json;

namespace FinCopilot.Api.Tools;

/// <summary>
/// Ferramenta que o agente pode chamar. A descrição e o schema são enviados ao modelo;
/// a execução acontece no nosso servidor.
/// </summary>
public interface IAgentTool
{
    string Name { get; }
    string Description { get; }

    /// <summary>Propriedades do JSON Schema de entrada (nome -> schema da propriedade).</summary>
    IReadOnlyDictionary<string, object> Properties { get; }
    IReadOnlyList<string> Required { get; }

    Task<object> ExecuteAsync(IReadOnlyDictionary<string, JsonElement> input, CancellationToken ct);
}

/// <summary>Erro de validação/execução que deve voltar ao modelo como tool_result com is_error.</summary>
public sealed class ToolException(string message) : Exception(message);

/// <summary>
/// Base tipada: desserializa a entrada do modelo para um record e delega a execução.
/// </summary>
public abstract class AgentTool<TInput> : IAgentTool
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract IReadOnlyDictionary<string, object> Properties { get; }
    public abstract IReadOnlyList<string> Required { get; }

    public Task<object> ExecuteAsync(IReadOnlyDictionary<string, JsonElement> input, CancellationToken ct)
    {
        TInput? parsed;
        try
        {
            parsed = JsonSerializer.SerializeToElement(input).Deserialize<TInput>(JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new ToolException($"Entrada inválida para {Name}: {ex.Message}");
        }

        if (parsed is null)
            throw new ToolException($"Entrada vazia para {Name}.");

        return ExecuteAsync(parsed, ct);
    }

    protected abstract Task<object> ExecuteAsync(TInput input, CancellationToken ct);
}
