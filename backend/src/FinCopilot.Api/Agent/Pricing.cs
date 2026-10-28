namespace FinCopilot.Api.Agent;

/// <summary>
/// Preço por milhão de tokens (USD). Escrita de cache de 5 min custa 1,25x a entrada.
/// </summary>
public sealed record ModelPricing(decimal InputPerMTok, decimal OutputPerMTok, decimal CacheReadPerMTok)
{
    public decimal CacheWritePerMTok => InputPerMTok * 1.25m;

    private static readonly Dictionary<string, ModelPricing> Known = new()
    {
        ["claude-opus-5-5"] = new(4.00m, 20.00m, 0.20m),
        ["claude-sonnet-5-5"] = new(2.00m, 10.00m, 0.20m),
        ["claude-haiku-5-5"] = new(0.10m, 0.50m, 0.01m),
    };

    public static ModelPricing For(string model) =>
        Known.TryGetValue(model, out var pricing) ? pricing : Known["claude-opus-5-5"];

    public decimal Cost(long input, long output, long cacheRead, long cacheWrite) =>
        (input * InputPerMTok + output * OutputPerMTok + cacheRead * CacheReadPerMTok + cacheWrite * CacheWritePerMTok)
        / 1_000_000m;
}
