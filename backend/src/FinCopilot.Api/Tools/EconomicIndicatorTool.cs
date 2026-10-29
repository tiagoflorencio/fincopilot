using System.Globalization;
using System.Text.Json.Serialization;

namespace FinCopilot.Api.Tools;

public sealed record EconomicIndicatorInput(string Indicator, int? Last = null);

public sealed record IndicatorPoint(string Date, decimal Value);

public sealed record EconomicIndicatorResult(
    string Indicator,
    string Description,
    string Unit,
    IReadOnlyList<IndicatorPoint> Values,
    string Source);

/// <summary>
/// Indicadores econômicos via API SGS do Banco Central (dados abertos, sem chave).
/// </summary>
public sealed class EconomicIndicatorTool(HttpClient http, TimeProvider time) : AgentTool<EconomicIndicatorInput>
{
    /// <param name="DaysPerPoint">Dias corridos por valor publicado, para calcular a janela da consulta.</param>
    private sealed record Series(int Code, string Description, string Unit, int DaysPerPoint);

    private static readonly Dictionary<string, Series> SeriesByIndicator = new()
    {
        ["selic"] = new(432, "Meta da taxa Selic definida pelo Copom", "% ao ano", 1),
        ["cdi"] = new(4389, "Taxa CDI anualizada (base 252)", "% ao ano", 2),
        ["ipca_12m"] = new(13522, "IPCA acumulado em 12 meses", "%", 31),
        ["ipca_mensal"] = new(433, "IPCA - variação mensal", "% no mês", 31),
    };

    private static readonly TimeZoneInfo Brasilia = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public override string Name => "get_economic_indicator";

    public override string Description =>
        "Busca valores oficiais e atualizados de indicadores econômicos brasileiros no Banco Central (SGS). " +
        "Indicadores: 'selic' (meta Selic), 'cdi' (CDI anualizado), 'ipca_12m' (inflação acumulada em 12 meses), " +
        "'ipca_mensal' (inflação do mês). Use antes de qualquer resposta que dependa de juros ou inflação atuais. " +
        "Os valores vêm em ordem cronológica, o último é o mais recente. A data de cada valor é a de referência " +
        "do dado (o IPCA sai com cerca de um mês de atraso e o CDI com um dia útil), não a data de hoje.";

    public override IReadOnlyDictionary<string, object> Properties => new Dictionary<string, object>
    {
        ["indicator"] = new { type = "string", @enum = SeriesByIndicator.Keys.ToArray(), description = "Indicador desejado." },
        ["last"] = new { type = "integer", description = "Quantos valores mais recentes retornar (1 a 24). Padrão: 1." },
    };

    public override IReadOnlyList<string> Required => ["indicator"];

    protected override async Task<object> ExecuteAsync(EconomicIndicatorInput input, CancellationToken ct)
    {
        if (!SeriesByIndicator.TryGetValue(input.Indicator, out var series))
            throw new ToolException($"Indicador desconhecido '{input.Indicator}'. Use: {string.Join(", ", SeriesByIndicator.Keys)}.");

        var last = Math.Clamp(input.Last ?? 1, 1, 24);

        // Consulta por período, e não "ultimos/N": a série da Selic traz datas futuras (até a próxima
        // reunião do Copom), o que fazia o modelo achar que "hoje" era essa data. A folga cobre o
        // atraso de publicação, fins de semana e feriados.
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), Brasilia).DateTime);
        var start = today.AddDays(-(last + 3) * series.DaysPerPoint - 7);
        // Cultura invariante: no formato, "/" seria trocada pelo separador de data da cultura atual.
        var url = string.Create(CultureInfo.InvariantCulture,
            $"https://api.bcb.gov.br/dados/serie/bcdata.sgs.{series.Code}/dados?formato=json&dataInicial={start:dd/MM/yyyy}&dataFinal={today:dd/MM/yyyy}");

        var points = await http.GetFromJsonAsync<List<SgsPoint>>(url, ct)
                     ?? throw new ToolException("Resposta vazia do Banco Central.");

        // A ordem que o SGS devolve varia por série (o CDI já veio decrescente): ordenamos aqui.
        var values = points
            .Select(p => (Date: DateOnly.ParseExact(p.Data, "dd/MM/yyyy", CultureInfo.InvariantCulture), p.Valor))
            .Where(p => p.Date <= today)
            .OrderBy(p => p.Date)
            .TakeLast(last)
            .Select(p => new IndicatorPoint(p.Date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), decimal.Parse(p.Valor, CultureInfo.InvariantCulture)))
            .ToList();

        if (values.Count == 0)
            throw new ToolException("O Banco Central não retornou valores recentes para esse indicador.");

        return new EconomicIndicatorResult(
            input.Indicator,
            series.Description,
            series.Unit,
            values,
            $"Banco Central do Brasil - SGS série {series.Code}");
    }

    // O SGS retorna: [{ "data": "01/10/2026", "valor": "10.50" }]
    private sealed record SgsPoint(
        [property: JsonPropertyName("data")] string Data,
        [property: JsonPropertyName("valor")] string Valor);
}
