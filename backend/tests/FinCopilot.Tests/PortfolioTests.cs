using FinCopilot.Api.Tools;

namespace FinCopilot.Tests;

public class PortfolioTests
{
    private static readonly PortfolioAsset Petr = new(AssetKinds.Stock, Ticker: "PETR4", Quantity: 100, AveragePrice: 35.5m);
    private static readonly PortfolioAsset Cdb = new(AssetKinds.FixedIncome, Name: "CDB 110% CDI", Amount: 5000m);

    [Fact]
    public void Carteira_valida_passa()
    {
        Assert.Null(new Portfolio([Petr, Cdb]).Validate());
    }

    public static TheoryData<PortfolioAsset> InvalidAssets => new()
    {
        new PortfolioAsset("crypto", Ticker: "BTC"),
        Petr with { Ticker = "petr4" },
        Petr with { Ticker = "PETR4; DROP" },
        Petr with { Quantity = 0 },
        Petr with { Quantity = 1.5m },
        Petr with { AveragePrice = 0 },
        Petr with { AveragePrice = null },
        Cdb with { Name = " " },
        Cdb with { Amount = -1 },
    };

    [Theory]
    [MemberData(nameof(InvalidAssets))]
    public void Rejeita_ativo_invalido(PortfolioAsset asset)
    {
        Assert.NotNull(new Portfolio([asset]).Validate());
    }

    [Fact]
    public void Rejeita_ticker_repetido_e_excesso_de_ativos()
    {
        Assert.Contains("repetido", new Portfolio([Petr, Petr]).Validate());
        Assert.Contains("máximo", new Portfolio(Enumerable.Repeat(Cdb, Portfolio.MaxAssets + 1).ToList()).Validate());
    }

    [Fact]
    public void Aviso_de_contexto_so_aparece_quando_a_carteira_muda()
    {
        var first = Portfolio.ContextNote(null, new Portfolio([Petr, Cdb]), out var fingerprint);
        Assert.Contains("2 ativos", first);
        Assert.DoesNotContain("35", first); // sem números: quem lê os valores é a ferramenta

        Assert.Null(Portfolio.ContextNote(fingerprint, new Portfolio([Petr, Cdb]), out _));
        Assert.Contains("alterada", Portfolio.ContextNote(fingerprint, new Portfolio([Petr]), out _));

        Assert.Contains("removeu", Portfolio.ContextNote(fingerprint, null, out var cleared));
        Assert.Null(cleared);
        Assert.Null(Portfolio.ContextNote(null, new Portfolio([]), out _));
    }
}
