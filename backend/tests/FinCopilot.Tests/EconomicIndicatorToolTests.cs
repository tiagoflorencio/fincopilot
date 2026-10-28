using System.Net;
using System.Text;
using System.Text.Json;
using FinCopilot.Api.Tools;

namespace FinCopilot.Tests;

public class EconomicIndicatorToolTests
{
    [Fact]
    public async Task Converte_resposta_do_SGS_e_monta_a_url_da_serie()
    {
        var handler = new StubHandler("""[{"data":"01/09/2026","valor":"10.50"},{"data":"01/10/2026","valor":"10.25"}]""");
        IAgentTool tool = new EconomicIndicatorTool(new HttpClient(handler));

        var result = (EconomicIndicatorResult)await tool.ExecuteAsync(Input("selic", last: 2), CancellationToken.None);

        Assert.Contains("bcdata.sgs.432/dados/ultimos/2", handler.LastUri!.ToString());
        Assert.Equal([10.50m, 10.25m], result.Values.Select(v => v.Value));
        Assert.Equal("% ao ano", result.Unit);
    }

    [Fact]
    public async Task Limita_a_quantidade_de_valores_pedidos()
    {
        var handler = new StubHandler("[]");
        IAgentTool tool = new EconomicIndicatorTool(new HttpClient(handler));

        await tool.ExecuteAsync(Input("ipca_12m", last: 500), CancellationToken.None);

        Assert.Contains("/ultimos/24", handler.LastUri!.ToString());
    }

    [Fact]
    public async Task Indicador_desconhecido_vira_ToolException()
    {
        IAgentTool tool = new EconomicIndicatorTool(new HttpClient(new StubHandler("[]")));

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
