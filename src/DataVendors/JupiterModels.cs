using System.Text.Json.Serialization;

namespace SolanaTradingBot.DataVendors;

// Jupiter Quote API models
public class JupiterQuoteRequest
{
    [JsonPropertyName("inputMint")]
    public required string InputMint { get; set; }

    [JsonPropertyName("outputMint")]
    public required string OutputMint { get; set; }

    [JsonPropertyName("amount")]
    public required string Amount { get; set; }

    [JsonPropertyName("slippageBps")]
    public int SlippageBps { get; set; }

    [JsonPropertyName("onlyDirectRoutes")]
    public bool OnlyDirectRoutes { get; set; } = false;

    [JsonPropertyName("maxAccounts")]
    public int? MaxAccounts { get; set; }
}

public class JupiterQuoteResponse
{
    [JsonPropertyName("inputMint")]
    public required string InputMint { get; set; }

    [JsonPropertyName("inAmount")]
    public required string InAmount { get; set; }

    [JsonPropertyName("outputMint")]
    public required string OutputMint { get; set; }

    [JsonPropertyName("outAmount")]
    public required string OutAmount { get; set; }

    [JsonPropertyName("otherAmountThreshold")]
    public required string OtherAmountThreshold { get; set; }

    [JsonPropertyName("swapMode")]
    public required string SwapMode { get; set; }

    [JsonPropertyName("slippageBps")]
    public int SlippageBps { get; set; }

    [JsonPropertyName("platformFee")]
    public PlatformFee? PlatformFee { get; set; }

    [JsonPropertyName("priceImpactPct")]
    public required string PriceImpactPct { get; set; }

    [JsonPropertyName("routePlan")]
    public List<RoutePlan> RoutePlan { get; set; } = new();

    [JsonPropertyName("contextSlot")]
    public long? ContextSlot { get; set; }

    [JsonPropertyName("timeTaken")]
    public double? TimeTaken { get; set; }
}

public class PlatformFee
{
    [JsonPropertyName("amount")]
    public required string Amount { get; set; }

    [JsonPropertyName("feeBps")]
    public int FeeBps { get; set; }
}

public class RoutePlan
{
    [JsonPropertyName("swapInfo")]
    public required SwapInfo SwapInfo { get; set; }

    [JsonPropertyName("percent")]
    public int Percent { get; set; }
}

public class SwapInfo
{
    [JsonPropertyName("ammKey")]
    public required string AmmKey { get; set; }

    [JsonPropertyName("label")]
    public required string Label { get; set; }

    [JsonPropertyName("inputMint")]
    public required string InputMint { get; set; }

    [JsonPropertyName("outputMint")]
    public required string OutputMint { get; set; }

    [JsonPropertyName("inAmount")]
    public required string InAmount { get; set; }

    [JsonPropertyName("outAmount")]
    public required string OutAmount { get; set; }

    [JsonPropertyName("feeAmount")]
    public required string FeeAmount { get; set; }

    [JsonPropertyName("feeMint")]
    public required string FeeMint { get; set; }
}

public class JupiterSwapRequest
{
    [JsonPropertyName("quoteResponse")]
    public required JupiterQuoteResponse QuoteResponse { get; set; }

    [JsonPropertyName("userPublicKey")]
    public required string UserPublicKey { get; set; }

    [JsonPropertyName("wrapAndUnwrapSol")]
    public bool WrapAndUnwrapSol { get; set; } = true;

    [JsonPropertyName("useSharedAccounts")]
    public bool UseSharedAccounts { get; set; } = true;

    [JsonPropertyName("feeAccount")]
    public string? FeeAccount { get; set; }

    [JsonPropertyName("trackingAccount")]
    public string? TrackingAccount { get; set; }

    [JsonPropertyName("computeUnitPriceMicroLamports")]
    public string? ComputeUnitPriceMicroLamports { get; set; }

    [JsonPropertyName("prioritizationFeeLamports")]
    public string? PrioritizationFeeLamports { get; set; }

    [JsonPropertyName("asLegacyTransaction")]
    public bool AsLegacyTransaction { get; set; } = false;

    [JsonPropertyName("useTokenLedger")]
    public bool UseTokenLedger { get; set; } = false;

    [JsonPropertyName("destinationTokenAccount")]
    public string? DestinationTokenAccount { get; set; }
}

public class JupiterSwapResponse
{
    [JsonPropertyName("swapTransaction")]
    public required string SwapTransaction { get; set; }

    [JsonPropertyName("lastValidBlockHeight")]
    public long? LastValidBlockHeight { get; set; }

    [JsonPropertyName("prioritizationFeeLamports")]
    public long? PrioritizationFeeLamports { get; set; }

    [JsonPropertyName("computeUnitLimit")]
    public long? ComputeUnitLimit { get; set; }

    [JsonPropertyName("computeUnitPrice")]
    public long? ComputeUnitPrice { get; set; }

    [JsonPropertyName("dynamicSlippageReport")]
    public string? DynamicSlippageReport { get; set; }

    [JsonPropertyName("simulationError")]
    public SimulationError? SimulationError { get; set; }
}

public class SimulationError
{
    [JsonPropertyName("code")]
    public required string Code { get; set; }

    [JsonPropertyName("message")]
    public required string Message { get; set; }
}

// Price and token info models
public class TokenInfo
{
    [JsonPropertyName("chainId")]
    public int ChainId { get; set; }

    [JsonPropertyName("address")]
    public required string Address { get; set; }

    [JsonPropertyName("symbol")]
    public required string Symbol { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("decimals")]
    public int Decimals { get; set; }

    [JsonPropertyName("logoURI")]
    public string? LogoUri { get; set; }

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();
}

public class TokenPriceResponse
{
    [JsonPropertyName("data")]
    public Dictionary<string, TokenPriceData> Data { get; set; } = new();

    [JsonPropertyName("timeTaken")]
    public double TimeTaken { get; set; }
}

public class TokenPriceData
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("mintSymbol")]
    public required string MintSymbol { get; set; }

    [JsonPropertyName("vsToken")]
    public required string VsToken { get; set; }

    [JsonPropertyName("vsTokenSymbol")]
    public required string VsTokenSymbol { get; set; }

    [JsonPropertyName("price")]
    public decimal Price { get; set; }
}
