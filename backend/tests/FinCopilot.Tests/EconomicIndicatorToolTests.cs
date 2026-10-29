using System.Net;
using System.Text;
using System.Text.Json;
using FinCopilot.Api.Tools;

namespace FinCopilot.Tests;

public class EconomicIndicatorToolTests
{
    // 28/10/2026 às 01:00 UTC ainda é 27/10 em Brasília (UTC-3).
    private static readonly FixedTimeProvider Clock = new(new DateTimeOffset(2026, 10, 28, 1, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Consulta_por_periodo_ate_hoje_em_Brasilia()
    {
        var handler = new StubHandler("""[{"data":"01/09/2026","valor":"10.50"},{"data":"01/10/2026","valor":"10.25"}]""");
        IAgentTool tool = new EconomicIndicatorTool(new HttpClient(handler), Clock);

        var result = (EconomicIndicatorResult)await tool.ExecuteAsync(Input("ipca_mensal", last: 2), CancellationToken.None);

        var url = handler.LastUri!.ToString();
        Assert.Contains("bcdata.sgs.433/dados?formato=json", url);
        Assert.Contains("dataFinal=27/10/2026", url);
        Assert.Equal([10.50m, 10.25m], result.Values.Select(v => v.Value));
        Assert.Equal("% no mês", result.Unit);
    }

    [Fact]
    public async Task Descarta_datas_futuras_da_Selic()
    {
        // A série 432 traz a meta vigente até a próxima reunião do Copom.
        var handler = new StubHandler("""[{"data":"26/10/2026","valor":"13.75"},{"data":"27/10/2026","valor":"13.75"},{"data":"04/11/2026","valor":"13.75"}]""");
        IAgentTool tool = new EconomicIndicatorTool(new HttpClient(handler), Clock);

        var result = (EconomicIndicatorResult)await tool.ExecuteAsync(Input("selic"), CancellationToken.None);

        Assert.Equal("27/10/2026", Assert.Single(result.Values).Date);
    }

    [Fact]
    public async Task Ordena_por_data_e_devolve_os_ultimos_N()
    {
        // O CDI já veio em ordem decrescente do SGS.
        var handler = new StubHandler("""[{"data":"27/10/2026","valor":"13.65"},{"data":"23/10/2026","valor":"13.60"},{"data":"26/10/2026","valor":"13.64"}]""");
        IAgentTool tool = new EconomicIndicatorTool(new HttpClient(handler), Clock);

        var result = (EconomicIndicatorResult)await tool.ExecuteAsync(Input("cdi", last: 2), CancellationToken.None);

        Assert.Equal(["26/10/2026", "27/10/2026"], result.Values.Select(v => v.Date));
    }

    [Fact]
    public async Task Limita_a_quantidade_de_valores_pedidos()
    {
        var points = string.Join(",", Enumerable.Range(0, 30).Select(i =>
            $$"""{"data":"{{new DateOnly(2024, 1, 1).AddMonths(i):dd/MM/yyyy}}","valor":"4.{{i}}"}"""));
        IAgentTool tool = new EconomicIndicatorTool(new HttpClient(new StubHandler($"[{points}]")), Clock);

        var result = (EconomicIndicatorResult)await tool.ExecuteAsync(Input("ipca_12m", last: 500), CancellationToken.None);

        Assert.Equal(24, result.Values.Count);
    }

    [Fact]
    public async Task Resposta_sem_valores_vira_ToolException()
    {
        IAgentTool tool = new EconomicIndicatorTool(new HttpClient(new StubHandler("[]")), Clock);

        await Assert.ThrowsAsync<ToolException>(() => tool.ExecuteAsync(Input("selic"), CancellationToken.None));
    }

    [Fact]
    public async Task Indicador_desconhecido_vira_ToolException()
    {
        IAgentTool tool = new EconomicIndicatorTool(new HttpClient(new StubHandler("[]")), Clock);

        await Assert.ThrowsAsync<ToolException>(() => tool.ExecuteAsync(Input("dolar"), CancellationToken.None));
    }

    private static Dictionary<string, JsonElement> Input(string indicator, int? last = null)
    {
        var input = new Dictionary<string, JsonElement> { ["indicator"] = JsonSerializer.SerializeToElement(indicator) };
        if (last is not null) input["last"] = JsonSerializer.SerializeToElement(last);
        return input;
    }

    private sealed class StubHandler(string json) : HttpMessageHandler
    {
        public Uri? LastUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        }
    }
}
