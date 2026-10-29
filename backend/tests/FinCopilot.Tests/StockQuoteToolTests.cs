using System.Net;
using System.Text;
using System.Text.Json;
using FinCopilot.Api.Tools;
using Microsoft.Extensions.Configuration;

namespace FinCopilot.Tests;

public class StockQuoteToolTests
{
    [Fact]
    public async Task Ticker_sem_permissao_nao_derruba_os_demais()
    {
        var handler = new RoutingHandler(new()
        {
            ["PETR4"] = (HttpStatusCode.OK, """{"results":[{"symbol":"PETR4","longName":"Petrobras","currency":"BRL","regularMarketPrice":52.79,"regularMarketChangePercent":-2.0213}]}"""),
            ["HGLG11"] = (HttpStatusCode.Unauthorized, """{"error":true,"code":"MISSING_TOKEN"}"""),
            ["XPTO3"] = (HttpStatusCode.NotFound, """{"error":true}"""),
        });
        IAgentTool tool = new StockQuoteTool(new BrapiClient(new HttpClient(handler), new ConfigurationBuilder().Build()));

        var result = (StockQuoteResult)await tool.ExecuteAsync(Input("petr4", "HGLG11", "XPTO3"), CancellationToken.None);

        var quote = Assert.Single(result.Quotes);
        Assert.Equal(52.79m, quote.Price);
        Assert.Equal(-2.02m, quote.ChangePercent);
        Assert.Equal(["HGLG11"], result.Unavailable);
        Assert.Equal(["XPTO3"], result.NotFound);
        Assert.NotNull(result.Note);
    }

    [Theory]
    [InlineData("PETR4; DROP")]
    [InlineData("../etc")]
    public async Task Rejeita_ticker_malformado(string ticker)
    {
        IAgentTool tool = new StockQuoteTool(new BrapiClient(new HttpClient(new RoutingHandler([])), new ConfigurationBuilder().Build()));

        await Assert.ThrowsAsync<ToolException>(() => tool.ExecuteAsync(Input(ticker), CancellationToken.None));
    }

    private static Dictionary<string, JsonElement> Input(params string[] tickers) =>
        new() { ["tickers"] = JsonSerializer.SerializeToElement(tickers) };

    private sealed class RoutingHandler(Dictionary<string, (HttpStatusCode Status, string Json)> routes) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var ticker = request.RequestUri!.Segments[^1];
            var (status, json) = routes[ticker];
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        }
    }
}
