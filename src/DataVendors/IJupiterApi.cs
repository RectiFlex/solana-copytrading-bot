using Refit;

namespace SolanaTradingBot.DataVendors;

public interface IJupiterApi
{
    [Get("/v6/quote")]
    Task<JupiterQuoteResponse> GetQuoteAsync(
        [Query] string inputMint,
        [Query] string outputMint, 
        [Query] string amount,
        [Query] int slippageBps,
        [Query] bool onlyDirectRoutes = false,
        [Query] int? maxAccounts = null);

    [Post("/v6/swap")]
    Task<JupiterSwapResponse> GetSwapTransactionAsync([Body] JupiterSwapRequest request);

    [Get("/v6/tokens")]
    Task<List<TokenInfo>> GetTokensAsync();

    [Get("/price")]
    Task<TokenPriceResponse> GetPricesAsync([Query] string ids, [Query] string? vsToken = null);
}