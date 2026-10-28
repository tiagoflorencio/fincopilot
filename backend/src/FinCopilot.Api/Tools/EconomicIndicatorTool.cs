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
public sealed class EconomicIndicatorTool(HttpClient http) : AgentTool<EconomicIndicatorInput>
{
    private sealed record Series(int Code, string Description, string Unit);

    private static readonly Dictionary<string, Series> SeriesByIndicator = new()
    {
        ["selic"] = new(432, "Meta da taxa Selic definida pelo Copom", "% ao ano"),
        ["cdi"] = new(4389, "Taxa CDI anualizada (base 252)", "% ao ano"),
        ["ipca_12m"] = new(13522, "IPCA acumulado em 12 meses", "%"),
        ["ipca_mensal"] = new(433, "IPCA - variação mensal", "% no mês"),
    };

    public override string Name => "get_economic_indicator";

    public override string Description =>
        "Busca valores oficiais e atualizados de indicadores econômicos brasileiros no Banco Central (SGS). " +
        "Indicadores: 'selic' (meta Selic), 'cdi' (CDI anualizado), 'ipca_12m' (inflação acumulada em 12 meses), " +
        "'ipca_mensal' (inflação do mês). Use antes de qualquer resposta que dependa de juros ou inflação atuais.";

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
        var url = $"https://api.bcb.gov.br/dados/serie/bcdata.sgs.{series.Code}/dados/ultimos/{last}?formato=json";

        var points = await http.GetFromJsonAsync<List<SgsPoint>>(url, ct)
                     ?? throw new ToolException("Resposta vazia do Banco Central.");

        return new EconomicIndicatorResult(
            input.Indicator,
            series.Description,
            series.Unit,
            points.Select(p => new IndicatorPoint(p.Data, decimal.Parse(p.Valor, CultureInfo.InvariantCulture))).ToList(),
            $"Banco Central do Brasil - SGS série {series.Code}");
    }

    // O SGS retorna: [{ "data": "01/10/2026", "valor": "10.50" }]
    private sealed record SgsPoint(
        [property: JsonPropertyName("data")] string Data,
        [property: JsonPropertyName("valor")] string Valor);
}
