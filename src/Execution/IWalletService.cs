using Solnet.Wallet;
using Solnet.Rpc.Models;

namespace SolanaTradingBot.Execution;

public interface IWalletService
{
    /// <summary>
    /// The wallet's public key
    /// </summary>
    PublicKey PublicKey { get; }
    
    /// <summary>
    /// The wallet account for signing transactions
    /// </summary>
    Account Account { get; }
    
    /// <summary>
    /// Gets the current SOL balance in lamports
    /// </summary>
    Task<ulong> GetSolBalanceAsync();
    
    /// <summary>
    /// Gets the balance of a specific token in the wallet
    /// </summary>
    Task<ulong> GetTokenBalanceAsync(string tokenMint);
    
    /// <summary>
    /// Ensures associated token accounts exist for the given token mints
    /// </summary>
    Task EnsureAssociatedTokenAccountsAsync(params string[] tokenMints);
    
    /// <summary>
    /// Wraps SOL to WSOL
    /// </summary>
    Task<string> WrapSolAsync(ulong lamports);
    
    /// <summary>
    /// Unwraps WSOL back to SOL and closes the temporary account
    /// </summary>
    Task<string> UnwrapSolAsync();
    
    /// <summary>
    /// Validates that the wallet has sufficient SOL balance
    /// </summary>
    Task ValidateMinimumBalanceAsync();
}
