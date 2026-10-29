using System.Text.Json;

namespace FinCopilot.Api.Tools;

/// <summary>Tipos de ativo aceitos. Espelhado em frontend/src/types/portfolio.ts.</summary>
public static class AssetKinds
{
    public const string Stock = "stock";
    public const string Fii = "fii";
    public const string Etf = "etf";
    public const string FixedIncome = "fixed_income";

    /// <summary>Ordem fixa da alocação: a cor de cada tipo no gráfico depende dela.</summary>
    public static readonly string[] All = [Stock, Fii, Etf, FixedIncome];

    public static bool IsListed(string kind) => kind is Stock or Fii or Etf;
}

/// <summary>
/// Ativo da carteira enviada pelo frontend. Ativos listados usam Ticker/Quantity/AveragePrice;
/// renda fixa usa Name/Amount (valor atual informado pelo usuário).
/// </summary>
public sealed record PortfolioAsset(
    string Kind,
    string? Ticker = null,
    decimal? Quantity = null,
    decimal? AveragePrice = null,
    string? Name = null,
    decimal? Amount = null);

public sealed record Portfolio(IReadOnlyList<PortfolioAsset>? Assets)
{
    public const int MaxAssets = 20;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Revalida no servidor o que o frontend já validou: o cliente não é confiável.
    /// Mesmos limites de frontend/src/lib/portfolio.ts. Devolve a primeira mensagem de erro ou null.
    /// </summary>
    public string? Validate()
    {
        if (Assets is null)
            return "A carteira precisa ter a lista de ativos.";
        if (Assets.Count > MaxAssets)
            return $"A carteira aceita no máximo {MaxAssets} ativos.";

        var tickers = new HashSet<string>();
        foreach (var a in Assets)
        {
            if (a is null || !AssetKinds.All.Contains(a.Kind))
                return "Tipo de ativo inválido.";

            if (AssetKinds.IsListed(a.Kind))
            {
                if (a.Ticker is null || !BrapiClient.IsValidTicker(a.Ticker))
                    return $"Ticker inválido: '{a.Ticker}'.";
                if (!tickers.Add(a.Ticker))
                    return $"Ticker repetido: {a.Ticker}.";
                if (a.Quantity is not { } q || q < 1 || q > 10_000_000 || q % 1 != 0)
                    return $"Quantidade inválida para {a.Ticker}.";
                if (a.AveragePrice is not { } p || p < 0.01m || p > 1_000_000m)
                    return $"Preço médio inválido para {a.Ticker}.";
            }
            else
            {
                if (a.Name is not { } name || name.Trim().Length is < 2 or > 60)
                    return "O nome do investimento de renda fixa deve ter de 2 a 60 caracteres.";
                if (a.Amount is not { } v || v < 0.01m || v > 100_000_000m)
                    return $"Valor inválido para {a.Name}.";
            }
        }

        return null;
    }

    /// <summary>
    /// Aviso curto acrescentado à mensagem do usuário quando a carteira muda. Vai no histórico
    /// (depois do prefixo em cache), nunca no system prompt, e não leva números: a ferramenta
    /// analyze_portfolio é quem lê os valores.
    /// </summary>
    public static string? ContextNote(string? previousFingerprint, Portfolio? current, out string? fingerprint)
    {
        var hasAssets = current?.Assets is { Count: > 0 };
        fingerprint = hasAssets ? JsonSerializer.Serialize(current, JsonOptions) : null;

        if (fingerprint == previousFingerprint)
            return null;
        if (!hasAssets)
            return "[Aviso do app: o usuário removeu a carteira cadastrada.]";

        var count = current!.Assets!.Count;
        var updated = previousFingerprint is null ? "" : " (alterada desde a última mensagem)";
        return $"[Aviso do app: o usuário tem uma carteira cadastrada com {count} {(count == 1 ? "ativo" : "ativos")}{updated}. " +
               "Para perguntas sobre ela, use analyze_portfolio.]";
    }
}

/// <summary>
/// Carteira da requisição atual. Scoped: o endpoint preenche e a analyze_portfolio lê,
/// então o modelo não precisa transcrever os números como argumento da ferramenta.
/// </summary>
public sealed class PortfolioContext
{
    public Portfolio? Current { get; set; }
}
