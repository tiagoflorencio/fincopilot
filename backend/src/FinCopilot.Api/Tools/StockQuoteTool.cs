using System.Net;
using System.Text.RegularExpressions;

namespace FinCopilot.Api.Tools;

public sealed record StockQuoteInput(string[] Tickers);

public sealed record StockQuote(
    string Symbol,
    string? Name,
    decimal? Price,
    decimal? ChangePercent,
    string? Currency,
    DateTimeOffset? UpdatedAt);

public sealed record StockQuoteResult(
    IReadOnlyList<StockQuote> Quotes,
    IReadOnlyList<string> NotFound,
    IReadOnlyList<string> Unavailable,
    string Source,
    string? Note);

/// <summary>
/// Cotações da B3 (ações, FIIs, ETFs) via brapi.dev.
/// </summary>
public sealed partial class StockQuoteTool(HttpClient http, IConfiguration config) : AgentTool<StockQuoteInput>
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
        if (tickers.FirstOrDefault(t => !TickerPattern().IsMatch(t)) is { } invalid)
            throw new ToolException($"Ticker inválido: '{invalid}'.");

        // O plano gratuito da brapi aceita um ticker por requisição, então buscamos em paralelo.
        var results = await Task.WhenAll(tickers.Select(t => FetchAsync(t, ct)));
        var unavailable = results.Where(r => r.Status == FetchStatus.Unauthorized).Select(r => r.Ticker).ToList();

        if (results.All(r => r.Status == FetchStatus.Failed))
            throw new ToolException("A brapi está indisponível no momento.");

        return new StockQuoteResult(
            results.Where(r => r.Quote is not null).Select(r => r.Quote!).ToList(),
            results.Where(r => r.Status == FetchStatus.NotFound).Select(r => r.Ticker).ToList(),
            unavailable,
            "brapi.dev (dados podem ter atraso de até 15 minutos)",
            unavailable.Count > 0
                ? "Alguns tickers exigem token da brapi (configuração Brapi:Token); sem ele, só ativos de demonstração como PETR4, VALE3, ITUB4 e MGLU3 são liberados."
                : null);
    }

    private enum FetchStatus { Ok, NotFound, Unauthorized, Failed }

    private sealed record FetchResult(string Ticker, FetchStatus Status, StockQuote? Quote = null);

    // Falha de um ticker não derruba os demais: o agente recebe o que deu certo e o motivo do resto.
    private async Task<FetchResult> FetchAsync(string ticker, CancellationToken ct)
    {
        var url = $"https://brapi.dev/api/quote/{ticker}";
        if (config["Brapi:Token"] is { Length: > 0 } token)
            url += $"?token={Uri.EscapeDataString(token)}";

        try
        {
            using var response = await http.GetAsync(url, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return new(ticker, FetchStatus.NotFound);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.PaymentRequired or HttpStatusCode.Forbidden)
                return new(ticker, FetchStatus.Unauthorized);
            if (!response.IsSuccessStatusCode)
                return new(ticker, FetchStatus.Failed);

            var body = await response.Content.ReadFromJsonAsync<BrapiResponse>(ct);
            if (body?.Results?.FirstOrDefault() is not { } r)
                return new(ticker, FetchStatus.NotFound);

            return new(ticker, FetchStatus.Ok, new StockQuote(
                r.Symbol ?? ticker,
                r.LongName ?? r.ShortName,
                r.RegularMarketPrice,
                r.RegularMarketChangePercent is { } c ? Math.Round(c, 2) : null,
                r.Currency,
                r.RegularMarketTime));
        }
        catch (HttpRequestException)
        {
            return new(ticker, FetchStatus.Failed);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return new(ticker, FetchStatus.Failed);
        }
    }

    [GeneratedRegex("^[A-Z0-9]{4,6}[0-9]{1,2}F?$|^\\^?[A-Z]{3,6}$")]
    private static partial Regex TickerPattern();

    private sealed record BrapiResponse(List<BrapiQuote>? Results);

    private sealed record BrapiQuote(
        string? Symbol,
        string? ShortName,
        string? LongName,
        string? Currency,
        decimal? RegularMarketPrice,
        decimal? RegularMarketChangePercent,
        DateTimeOffset? RegularMarketTime);
}
