using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Solnet.Wallet;
using Solnet.Rpc;
using Solnet.Rpc.Models;
using Solnet.Programs;
using SolanaTradingBot.Common;
using System.Text.Json;
using SolnetWallet = Solnet.Wallet.Wallet;

namespace SolanaTradingBot.Execution;

public class WalletService : IWalletService
{
    private readonly ILogger<WalletService> _logger;
    private readonly TradingConfig _config;
    private readonly IRpcClient _rpcClient;
    private Account? _account;

    public WalletService(ILogger<WalletService> logger, IOptions<TradingConfig> config, IRpcClient rpcClient)
    {
        _logger = logger;
        _config = config.Value;
        _rpcClient = rpcClient;
    }

    public PublicKey PublicKey => Account.PublicKey;

    public Account Account
    {
        get
        {
            if (_account == null)
            {
                LoadWallet();
            }
            return _account!;
        }
    }

    public async Task<ulong> GetSolBalanceAsync()
    {
        var response = await _rpcClient.GetBalanceAsync(PublicKey);
        if (!response.WasSuccessful)
        {
            throw new InvalidOperationException($"Failed to get SOL balance: {response.Reason}");
        }
        return response.Result.Value;
    }

    public async Task<ulong> GetTokenBalanceAsync(string tokenMint)
    {
        var tokenAccounts = await _rpcClient.GetTokenAccountsByOwnerAsync(
            PublicKey,
            tokenMint,
            TokenProgram.ProgramIdKey);

        if (!tokenAccounts.WasSuccessful)
        {
            throw new InvalidOperationException($"Failed to get token accounts: {tokenAccounts.Reason}");
        }

        var account = tokenAccounts.Result.Value.FirstOrDefault();
        if (account == null)
        {
            return 0;
        }

        var balance = await _rpcClient.GetTokenAccountBalanceAsync(account.PublicKey);
        if (!balance.WasSuccessful)
        {
            throw new InvalidOperationException($"Failed to get token balance: {balance.Reason}");
        }

        return balance.Result.Value.AmountUlong;
    }

    public async Task EnsureAssociatedTokenAccountsAsync(params string[] tokenMints)
    {
        foreach (var tokenMint in tokenMints)
        {
            var ata = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(
                PublicKey, 
                new PublicKey(tokenMint));

            var accountInfo = await _rpcClient.GetAccountInfoAsync(ata);
            if (accountInfo.Result?.Value == null)
            {
                _logger.LogInformation("Creating ATA for token {TokenMint}", tokenMint);
                
                var instruction = AssociatedTokenAccountProgram.CreateAssociatedTokenAccount(
                    PublicKey,
                    PublicKey,
                    new PublicKey(tokenMint));

                var transaction = new Transaction()
                {
                    RecentBlockHash = (await _rpcClient.GetLatestBlockHashAsync()).Result.Value.Blockhash,
                    FeePayer = PublicKey,
                    Instructions = new List<TransactionInstruction> { instruction }
                };

                transaction.Sign(Account);

                var result = await _rpcClient.SendTransactionAsync(transaction.Serialize());
                if (!result.WasSuccessful)
                {
                    throw new InvalidOperationException($"Failed to create ATA for {tokenMint}: {result.Reason}");
                }

                _logger.LogInformation("Created ATA for token {TokenMint}, signature: {Signature}", 
                    tokenMint, result.Result);
            }
        }
    }

    public async Task<string> WrapSolAsync(ulong lamports)
    {
        // Implementation would create a temporary WSOL account, transfer SOL, and sync native
        throw new NotImplementedException("WSOL wrapping not yet implemented");
    }

    public async Task<string> UnwrapSolAsync()
    {
        // Implementation would close WSOL accounts and reclaim lamports
        throw new NotImplementedException("WSOL unwrapping not yet implemented");
    }

    public async Task ValidateMinimumBalanceAsync()
    {
        var balance = await GetSolBalanceAsync();
        if (balance < (ulong)_config.Wallet.MinSolLamports)
        {
            throw new InvalidOperationException(
                $"Insufficient SOL balance. Required: {_config.Wallet.MinSolLamports} lamports, " +
                $"Current: {balance} lamports");
        }
    }

    private void LoadWallet()
    {
        try
        {
            switch (_config.Wallet.Source)
            {
                case WalletSource.Env:
                    LoadFromEnvironmentVariable();
                    break;
                case WalletSource.File:
                    LoadFromFile();
                    break;
                default:
                    throw new InvalidOperationException($"Unknown wallet source: {_config.Wallet.Source}");
            }

            _logger.LogInformation("Wallet loaded successfully. Public key: {PublicKey}", PublicKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load wallet");
            throw;
        }
    }

    private void LoadFromEnvironmentVariable()
    {
        if (string.IsNullOrEmpty(_config.Wallet.PrivateKeyJson))
        {
            throw new InvalidOperationException("WALLET__PRIVATE_KEY_JSON environment variable is not set");
        }

        try
        {
            var privateKeyBytes = JsonSerializer.Deserialize<byte[]>(_config.Wallet.PrivateKeyJson);
            if (privateKeyBytes == null || privateKeyBytes.Length != 64)
            {
                throw new InvalidOperationException("Invalid private key format in environment variable");
            }

            _account = new SolnetWallet(privateKeyBytes).Account;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Failed to parse private key JSON from environment variable", ex);
        }
    }

    private void LoadFromFile()
    {
        if (string.IsNullOrEmpty(_config.Wallet.KeyFile))
        {
            throw new InvalidOperationException("WALLET__KEYFILE is not set");
        }

        if (!File.Exists(_config.Wallet.KeyFile))
        {
            throw new FileNotFoundException($"Wallet keyfile not found: {_config.Wallet.KeyFile}");
        }

        try
        {
            var keyFileContent = File.ReadAllText(_config.Wallet.KeyFile);
            var privateKeyBytes = JsonSerializer.Deserialize<byte[]>(keyFileContent);
            
            if (privateKeyBytes == null || privateKeyBytes.Length != 64)
            {
                throw new InvalidOperationException("Invalid private key format in keyfile");
            }

            _account = new SolnetWallet(privateKeyBytes).Account;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to parse keyfile: {_config.Wallet.KeyFile}", ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load keyfile: {_config.Wallet.KeyFile}", ex);
        }
    }
}