using System.Globalization;

namespace FinCopilot.Api.Tools;

/// <summary>Sem argumentos: a carteira vem da requisição (PortfolioContext), não do modelo.</summary>
public sealed record AnalyzePortfolioInput;

/// <param name="PriceStatus">
/// "live" = cotação atual da brapi; "unavailable" = sem cotação, avaliado pelo preço médio;
/// "manual" = renda fixa, valor informado pelo usuário.
/// </param>
public sealed record PortfolioPosition(
    string Kind,
    string Label,
    decimal? Quantity,
    decimal? AveragePrice,
    decimal? CurrentPrice,
    decimal InvestedValue,
    decimal MarketValue,
    decimal WeightPercent,
    decimal? GainPercent,
    string PriceStatus);

public sealed record AllocationSlice(string Kind, decimal Value, decimal WeightPercent);

public sealed record PortfolioAnalysis(
    DateTimeOffset AsOf,
    decimal TotalValue,
    decimal InvestedValue,
    decimal GainPercent,
    IReadOnlyList<PortfolioPosition> Positions,
    IReadOnlyList<AllocationSlice> Allocation,
    IReadOnlyList<string> Warnings,
    string Source);

/// <summary>
/// Avalia a carteira cadastrada no app: preço atual de cada ativo listado, valor de mercado,
/// peso de cada posição e alocação por tipo. Todas as contas ficam aqui, não no modelo.
/// O resultado também alimenta o gráfico do frontend.
/// </summary>
public sealed class AnalyzePortfolioTool(BrapiClient brapi, PortfolioContext context, TimeProvider time)
    : AgentTool<AnalyzePortfolioInput>
{
    /// <summary>Peso a partir do qual uma posição é sinalizada como concentrada.</summary>
    public const decimal ConcentrationThresholdPercent = 25m;

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public override string Name => "analyze_portfolio";

    public override string Description =>
        "Analisa a carteira que o usuário cadastrou no painel 'Minha carteira' do app (não recebe argumentos: " +
        "os ativos vêm do app). Busca a cotação atual de ações, FIIs e ETFs e devolve valor de mercado, " +
        "valor investido, ganho em %, peso de cada posição e alocação por tipo de ativo, além de avisos de " +
        "concentração. Use para qualquer pergunta sobre a carteira do usuário em vez de fazer as contas. " +
        "Não considera dividendos, impostos, custos de corretagem nem o setor das empresas. Ativos sem " +
        "cotação disponível são avaliados pelo preço médio, e a renda fixa pelo valor que o usuário informou.";

    public override IReadOnlyDictionary<string, object> Properties => new Dictionary<string, object>();

    public override IReadOnlyList<string> Required => [];

    protected override async Task<object> ExecuteAsync(AnalyzePortfolioInput input, CancellationToken ct)
    {
        if (context.Current?.Assets is not { Count: > 0 } assets)
            throw new ToolException(
                "O usuário não tem carteira cadastrada. Explique que ele pode adicionar os ativos no botão " +
                "'Minha carteira', no topo da tela, ou usar 'Carregar exemplo'.");

        var listed = assets.Where(a => AssetKinds.IsListed(a.Kind)).ToList();
        var fetches = await Task.WhenAll(listed.Select(a => brapi.FetchAsync(a.Ticker!, ct)));
        var quotes = fetches.ToDictionary(f => f.Ticker);

        var valued = assets.Select(a => Value(a, quotes)).ToList();
        var total = valued.Sum(v => v.Market);
        var invested = valued.Sum(v => v.Invested);

        var positions = valued
            .Select(v => v.Position with { WeightPercent = Percent(v.Market, total) })
            .OrderByDescending(p => p.MarketValue)
            .ToList();

        // Ordem fixa por tipo (não por tamanho): a cor de cada fatia no gráfico segue o tipo.
        var allocation = AssetKinds.All
            .Select(kind => (kind, value: positions.Where(p => p.Kind == kind).Sum(p => p.MarketValue)))
            .Where(x => x.value > 0)
            .Select(x => new AllocationSlice(x.kind, Math.Round(x.value, 2), Percent(x.value, total)))
            .ToList();

        return new PortfolioAnalysis(
            time.GetUtcNow(),
            Math.Round(total, 2),
            Math.Round(invested, 2),
            Percent(total - invested, invested),
            positions,
            allocation,
            Warnings(fetches, positions),
            "brapi.dev (cotações com atraso de até 15 minutos) e valores informados pelo usuário");
    }

    private sealed record Valued(PortfolioPosition Position, decimal Market, decimal Invested);

    private static Valued Value(PortfolioAsset a, IReadOnlyDictionary<string, QuoteFetch> quotes)
    {
        if (!AssetKinds.IsListed(a.Kind))
        {
            var amount = a.Amount!.Value;
            return new(new PortfolioPosition(a.Kind, a.Name!.Trim(), null, null, null,
                amount, amount, 0, null, "manual"), amount, amount);
        }

        var quantity = a.Quantity!.Value;
        var average = a.AveragePrice!.Value;
        var livePrice = quotes[a.Ticker!].Quote?.Price;
        var price = livePrice ?? average;
        var invested = quantity * average;
        var market = quantity * price;

        return new(new PortfolioPosition(
            a.Kind, a.Ticker!, quantity, average, livePrice,
            Math.Round(invested, 2), Math.Round(market, 2), 0,
            livePrice is null ? null : Percent(market - invested, invested),
            livePrice is null ? "unavailable" : "live"), market, invested);
    }

    private static List<string> Warnings(QuoteFetch[] fetches, List<PortfolioPosition> positions)
    {
        List<string> warnings = [];

        var unauthorized = fetches.Where(f => f.Status == QuoteStatus.Unauthorized).Select(f => f.Ticker).ToList();
        if (unauthorized.Count > 0)
            warnings.Add($"Sem cotação para {string.Join(", ", unauthorized)}: a brapi exige token para esses tickers. " +
                         "Eles foram avaliados pelo preço médio.");

        var missing = fetches.Where(f => f.Status is QuoteStatus.NotFound or QuoteStatus.Failed || (f.Status == QuoteStatus.Ok && f.Quote?.Price is null))
            .Select(f => f.Ticker).ToList();
        if (missing.Count > 0)
            warnings.Add($"Não foi possível obter a cotação de {string.Join(", ", missing)}; avaliados pelo preço médio.");

        foreach (var p in positions.Where(p => p.WeightPercent >= ConcentrationThresholdPercent))
            warnings.Add(string.Create(PtBr, $"{p.Label} representa {p.WeightPercent:0.##}% da carteira (acima de {ConcentrationThresholdPercent:0}%)."));

        return warnings;
    }

    private static decimal Percent(decimal part, decimal whole) =>
        whole == 0 ? 0 : Math.Round(part / whole * 100, 2, MidpointRounding.AwayFromZero);
}
