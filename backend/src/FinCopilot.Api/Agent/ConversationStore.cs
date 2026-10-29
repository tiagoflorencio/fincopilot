using System.Collections.Concurrent;
using Anthropic.Models.Beta.Messages;

namespace FinCopilot.Api.Agent;

public sealed class Conversation(string id)
{
    public string Id { get; } = id;

    /// <summary>
    /// Histórico completo enviado à API. Só cresce (append-only): os blocos de thinking
    /// assinados só continuam válidos se o histórico anterior não for editado.
    /// </summary>
    public List<BetaMessageParam> Messages { get; } = [];

    /// <summary>Última carteira já avisada ao modelo, para só repetir o aviso quando ela mudar.</summary>
    public string? PortfolioFingerprint { get; set; }

    /// <summary>Impede duas execuções simultâneas do agente na mesma conversa.</summary>
    public SemaphoreSlim Lock { get; } = new(1, 1);
}

/// <summary>
/// Armazenamento em memória. Suficiente para a fase 1; trocar por Redis/banco depois.
/// </summary>
public sealed class ConversationStore
{
    private readonly ConcurrentDictionary<string, Conversation> _conversations = new();

    public Conversation GetOrCreate(string? id)
    {
        if (id is not null && _conversations.TryGetValue(id, out var existing))
            return existing;

        var created = new Conversation(Guid.NewGuid().ToString("N"));
        _conversations[created.Id] = created;
        return created;
    }
}
