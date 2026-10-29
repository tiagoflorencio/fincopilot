namespace FinCopilot.Api.Tools;

public sealed record StockQuoteInput(string[] Tickers);

public sealed record StockQuoteResult(
    IReadOnlyList<StockQuote> Quotes,
    IReadOnlyList<string> NotFound,
    IReadOnlyList<string> Unavailable,
    string Source,
    string? Note);

/// <summary>
/// Cotações da B3 (ações, FIIs, ETFs) via brapi.dev.
/// </summary>
public sealed class StockQuoteTool(BrapiClient brapi) : AgentTool<StockQuoteInput>
{
    private const int MaxTickers = 10;

    public override string Name => "get_stock_quote";

    public override string Description =>
        "Busca a cotação atual de ativos da B3 (ações, FIIs e ETFs) pelo ticker, ex.: PETR4, VALE3, HGLG11, BOVA11. " +
        "Retorna preço, variação do dia em % e horário da cotação. Aceita até 10 tickers por chamada; " +
        "prefira uma única chamada com todos os tickers necessários.";

    public override IReadOnlyDictionary<string, object> Properties => new Dictionary<string, object>
    {
        ["tickers"] = new
        {
            type = "array",
            items = new { type = "string" },
            description = "Lista de tickers da B3, em maiúsculas.",
        },
    };

    public override IReadOnlyList<string> Required => ["tickers"];

    protected override async Task<object> ExecuteAsync(StockQuoteInput input, CancellationToken ct)
    {
        var tickers = input.Tickers
            .Select(t => t.Trim().ToUpperInvariant())
            .Where(t => t.Length > 0)
            .Distinct()
            .ToArray();

        if (tickers.Length == 0)
            throw new ToolException("Informe pelo menos um ticker.");
        if (tickers.Length > MaxTickers)
            throw new ToolException($"No máximo {MaxTickers} tickers por chamada.");
        if (tickers.FirstOrDefault(t => !BrapiClient.IsValidTicker(t)) is { } invalid)
            throw new ToolException($"Ticker inválido: '{invalid}'.");

        // Falha de um ticker não derruba os demais: o agente recebe o que deu certo e o motivo do resto.
        var results = await Task.WhenAll(tickers.Select(t => brapi.FetchAsync(t, ct)));
        var unavailable = results.Where(r => r.Status == QuoteStatus.Unauthorized).Select(r => r.Ticker).ToList();

        if (results.All(r => r.Status == QuoteStatus.Failed))
            throw new ToolException("A brapi está indisponível no momento.");

        return new StockQuoteResult(
            results.Where(r => r.Quote is not null).Select(r => r.Quote!).ToList(),
            results.Where(r => r.Status == QuoteStatus.NotFound).Select(r => r.Ticker).ToList(),
            unavailable,
            "brapi.dev (dados podem ter atraso de até 15 minutos)",
            unavailable.Count > 0
                ? "Alguns tickers exigem token da brapi (configuração Brapi:Token); sem ele, só ativos de demonstração como PETR4, VALE3, ITUB4 e MGLU3 são liberados."
                : null);
    }
}
