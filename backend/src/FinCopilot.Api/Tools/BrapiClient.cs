using System.Net;
using System.Text.RegularExpressions;

namespace FinCopilot.Api.Tools;

public sealed record StockQuote(
    string Symbol,
    string? Name,
    decimal? Price,
    decimal? ChangePercent,
    string? Currency,
    DateTimeOffset? UpdatedAt);

public enum QuoteStatus { Ok, NotFound, Unauthorized, Failed }

public sealed record QuoteFetch(string Ticker, QuoteStatus Status, StockQuote? Quote = null);

/// <summary>
/// Cliente da brapi.dev compartilhado pelas ferramentas que precisam de cotação
/// (get_stock_quote e analyze_portfolio). Nunca lança por falha de um ticker:
/// devolve o status para a ferramenta decidir o que dizer ao modelo.
/// </summary>
public sealed partial class BrapiClient(HttpClient http, IConfiguration config)
{
    /// <summary>
    /// Ações (PETR4), units (TAEE11), fracionário (PETR4F) e índices (^BVSP). O frontend
    /// (lib/portfolio.ts) usa o subconjunto sem índices, que não são ativos de carteira.
    /// </summary>
    public static bool IsValidTicker(string ticker) => TickerPattern().IsMatch(ticker);

    private const int MaxAttempts = 3;

    /// <summary>Espera base entre tentativas após um 429 (multiplicada pela tentativa). Zero nos testes.</summary>
    public TimeSpan RetryBaseDelay { get; init; } = TimeSpan.FromSeconds(1);

    // O plano gratuito da brapi aceita um ticker por requisição; quem chama busca em paralelo.
    public async Task<QuoteFetch> FetchAsync(string ticker, CancellationToken ct)
    {
        var url = $"https://brapi.dev/api/quote/{ticker}";
        if (config["Brapi:Token"] is { Length: > 0 } token)
            url += $"?token={Uri.EscapeDataString(token)}";

        try
        {
            using var response = await GetWithRetryAsync(url, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return new(ticker, QuoteStatus.NotFound);
            // Sem token, só PETR4, VALE3, ITUB4 e MGLU3 são liberados; os demais voltam 401.
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.PaymentRequired or HttpStatusCode.Forbidden)
                return new(ticker, QuoteStatus.Unauthorized);
            if (!response.IsSuccessStatusCode)
                return new(ticker, QuoteStatus.Failed);

            var body = await response.Content.ReadFromJsonAsync<BrapiResponse>(ct);
            if (body?.Results?.FirstOrDefault() is not { } r)
                return new(ticker, QuoteStatus.NotFound);

            return new(ticker, QuoteStatus.Ok, new StockQuote(
                r.Symbol ?? ticker,
                r.LongName ?? r.ShortName,
                r.RegularMarketPrice,
                r.RegularMarketChangePercent is { } c ? Math.Round(c, 2) : null,
                r.Currency,
                r.RegularMarketTime));
        }
        catch (HttpRequestException)
        {
            return new(ticker, QuoteStatus.Failed);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return new(ticker, QuoteStatus.Failed);
        }
    }

    // Sem token, a brapi devolve 429 quando várias cotações saem ao mesmo tempo (ex.: analisar uma
    // carteira com 6 tickers). Tentar de novo após uma pausa resolve quase sempre.
    private async Task<HttpResponseMessage> GetWithRetryAsync(string url, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            var response = await http.GetAsync(url, ct);
            if (response.StatusCode != HttpStatusCode.TooManyRequests || attempt == MaxAttempts)
                return response;

            var delay = response.Headers.RetryAfter?.Delta is { } retryAfter
                ? TimeSpan.FromTicks(Math.Min(retryAfter.Ticks, TimeSpan.FromSeconds(3).Ticks))
                : RetryBaseDelay * attempt;
            response.Dispose();
            await Task.Delay(delay, ct);
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
