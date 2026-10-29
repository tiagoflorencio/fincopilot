using System.Net;
using System.Text;
using FinCopilot.Api.Tools;
using Microsoft.Extensions.Configuration;

namespace FinCopilot.Tests;

public class AnalyzePortfolioToolTests
{
    private static readonly FixedTimeProvider Clock = new(new DateTimeOffset(2026, 10, 28, 15, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Calcula_valor_de_mercado_pesos_e_alocacao_por_tipo()
    {
        var tool = CreateTool(new Portfolio([
            new(AssetKinds.Stock, Ticker: "PETR4", Quantity: 100, AveragePrice: 30m),
            new(AssetKinds.Fii, Ticker: "HGLG11", Quantity: 10, AveragePrice: 150m),
            new(AssetKinds.FixedIncome, Name: "Tesouro Selic", Amount: 2500m),
        ]), new()
        {
            ["PETR4"] = (HttpStatusCode.OK, """{"results":[{"symbol":"PETR4","regularMarketPrice":40.00}]}"""),
            ["HGLG11"] = (HttpStatusCode.Unauthorized, """{"error":true}"""),
        });

        var result = (PortfolioAnalysis)await tool.ExecuteAsync(new Dictionary<string, System.Text.Json.JsonElement>(), CancellationToken.None);

        // PETR4 4.000 (cotação) + HGLG11 1.500 (preço médio) + renda fixa 2.500 = 8.000
        Assert.Equal(8000m, result.TotalValue);
        Assert.Equal(7000m, result.InvestedValue);
        Assert.Equal(14.29m, result.GainPercent);

        var petr = result.Positions[0];
        Assert.Equal(("PETR4", 4000m, 50m, (decimal?)33.33m, "live"), (petr.Label, petr.MarketValue, petr.WeightPercent, petr.GainPercent, petr.PriceStatus));

        var hglg = result.Positions.Single(p => p.Label == "HGLG11");
        Assert.Equal(("unavailable", 1500m, (decimal?)null, (decimal?)null), (hglg.PriceStatus, hglg.MarketValue, hglg.CurrentPrice, hglg.GainPercent));

        Assert.Equal(
            [(AssetKinds.Stock, 50m), (AssetKinds.Fii, 18.75m), (AssetKinds.FixedIncome, 31.25m)],
            result.Allocation.Select(a => (a.Kind, a.WeightPercent)));
    }

    [Fact]
    public async Task Avisa_cotacao_indisponivel_e_concentracao()
    {
        var tool = CreateTool(new Portfolio([
            new(AssetKinds.Stock, Ticker: "VALE3", Quantity: 10, AveragePrice: 60m),
            new(AssetKinds.Etf, Ticker: "BOVA11", Quantity: 1, AveragePrice: 120m),
            new(AssetKinds.FixedIncome, Name: "CDB", Amount: 300m),
        ]), new()
        {
            ["VALE3"] = (HttpStatusCode.OK, """{"results":[{"symbol":"VALE3","regularMarketPrice":60.00}]}"""),
            ["BOVA11"] = (HttpStatusCode.Unauthorized, """{"error":true}"""),
        });

        var result = (PortfolioAnalysis)await tool.ExecuteAsync(new Dictionary<string, System.Text.Json.JsonElement>(), CancellationToken.None);

        // Total 1.020: VALE3 600 (58,82%), CDB 300 (29,41%), BOVA11 120 (11,76%)
        Assert.Contains(result.Warnings, w => w.Contains("BOVA11") && w.Contains("preço médio"));
        Assert.Contains(result.Warnings, w => w.StartsWith("VALE3 representa 58,82%"));
        Assert.Contains(result.Warnings, w => w.StartsWith("CDB representa 29,41%"));
        Assert.DoesNotContain(result.Warnings, w => w.StartsWith("BOVA11 representa"));
    }

    [Fact]
    public async Task Falha_de_rede_avalia_pelo_preco_medio_sem_derrubar_a_analise()
    {
        var tool = CreateTool(new Portfolio([new(AssetKinds.Stock, Ticker: "ITUB4", Quantity: 5, AveragePrice: 20m)]),
            new() { ["ITUB4"] = (HttpStatusCode.InternalServerError, "") });

        var result = (PortfolioAnalysis)await tool.ExecuteAsync(new Dictionary<string, System.Text.Json.JsonElement>(), CancellationToken.None);

        Assert.Equal(100m, result.TotalValue);
        Assert.Equal("unavailable", Assert.Single(result.Positions).PriceStatus);
        Assert.Contains(result.Warnings, w => w.StartsWith("Não foi possível obter a cotação de ITUB4"));
    }

    [Fact]
    public async Task Sem_carteira_vira_ToolException_explicando_como_cadastrar()
    {
        var tool = CreateTool(null, []);

        var ex = await Assert.ThrowsAsync<ToolException>(() =>
            tool.ExecuteAsync(new Dictionary<string, System.Text.Json.JsonElement>(), CancellationToken.None));
        Assert.Contains("Minha carteira", ex.Message);
    }

    [Fact]
    public void Schema_sem_argumentos()
    {
        // A carteira vem do PortfolioContext, não de argumentos que o modelo teria de transcrever.
        var tool = CreateTool(null, []);

        Assert.Empty(tool.Properties);
        Assert.Empty(tool.Required);
    }

    private static IAgentTool CreateTool(Portfolio? portfolio, Dictionary<string, (HttpStatusCode, string)> routes)
    {
        var brapi = new BrapiClient(new HttpClient(new RoutingHandler(routes)), new ConfigurationBuilder().Build());
        return new AnalyzePortfolioTool(brapi, new PortfolioContext { Current = portfolio }, Clock);
    }

    private sealed class RoutingHandler(Dictionary<string, (HttpStatusCode Status, string Json)> routes) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var (status, json) = routes[request.RequestUri!.Segments[^1]];
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        }
    }
}
