using System.Net;
using System.Text;
using FinCopilot.Api.Tools;
using Microsoft.Extensions.Configuration;

namespace FinCopilot.Tests;

public class BrapiClientTests
{
    private const string PetrJson = """{"results":[{"symbol":"PETR4","regularMarketPrice":52.71}]}""";

    [Fact]
    public async Task Tenta_de_novo_apos_429()
    {
        // Sem token, a brapi limita rajadas de requisições (visto ao analisar carteiras com 6 tickers).
        var handler = new SequenceHandler(HttpStatusCode.TooManyRequests, HttpStatusCode.TooManyRequests, HttpStatusCode.OK);
        var brapi = new BrapiClient(new HttpClient(handler), new ConfigurationBuilder().Build()) { RetryBaseDelay = TimeSpan.Zero };

        var result = await brapi.FetchAsync("PETR4", CancellationToken.None);

        Assert.Equal(QuoteStatus.Ok, result.Status);
        Assert.Equal(52.71m, result.Quote!.Price);
        Assert.Equal(3, handler.Calls);
    }

    [Fact]
    public async Task Desiste_depois_de_tres_429()
    {
        var handler = new SequenceHandler(Enumerable.Repeat(HttpStatusCode.TooManyRequests, 5).ToArray());
        var brapi = new BrapiClient(new HttpClient(handler), new ConfigurationBuilder().Build()) { RetryBaseDelay = TimeSpan.Zero };

        var result = await brapi.FetchAsync("PETR4", CancellationToken.None);

        Assert.Equal(QuoteStatus.Failed, result.Status);
        Assert.Equal(3, handler.Calls);
    }

    [Fact]
    public async Task Nao_repete_401_de_ticker_sem_permissao()
    {
        var handler = new SequenceHandler(HttpStatusCode.Unauthorized, HttpStatusCode.OK);
        var brapi = new BrapiClient(new HttpClient(handler), new ConfigurationBuilder().Build()) { RetryBaseDelay = TimeSpan.Zero };

        var result = await brapi.FetchAsync("HGLG11", CancellationToken.None);

        Assert.Equal(QuoteStatus.Unauthorized, result.Status);
        Assert.Equal(1, handler.Calls);
    }

    private sealed class SequenceHandler(params HttpStatusCode[] statuses) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var status = statuses[Math.Min(Calls++, statuses.Length - 1)];
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(status == HttpStatusCode.OK ? PetrJson : """{"error":true}""", Encoding.UTF8, "application/json"),
            });
        }
    }
}
