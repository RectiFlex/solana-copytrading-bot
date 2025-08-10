# Helius and .env Setup

This project supports configuration via environment variables. For local development, you can place variables in a `.env` file and export them into your shell.

## Required variables
- `Helius__Http` – HTTPS RPC endpoint including your Helius API key
- `Helius__Ws` – WebSocket endpoint including your Helius API key

Optional wallet variables:
- `WALLET__SOURCE` – `Env` or `File`
- `WALLET__KEYFILE` – path to a `solana-keygen` keypair JSON
- `WALLET__PRIVATE_KEY_JSON` – the JSON array contents of the keypair
- `WALLET__MINSOLLAMPORTS` – minimum SOL balance guard (default: 5,000,000 lamports ~0.005 SOL)

## Using .env in your shell
> .NET does not automatically parse .env files. Use it as a source to export variables.

Bash/zsh:
```bash
export $(grep -v '^#' .env | xargs)
```

PowerShell:
```powershell
Get-Content .env | ForEach-Object { if ($_ -and $_ -notmatch '^#') { $k,$v = $_ -split '=',2; [System.Environment]::SetEnvironmentVariable($k,$v) } }
```

Docker Compose automatically loads `.env` in the same directory; you can map variables via the `environment` section.

## Verifying configuration
- Run paper mode: `dotnet run --project src/CLI -- paper`
- Check live WS access (watch mode): `dotnet run --project src/CLI -- watch`
- Show balances (after wallet setup): `dotnet run --project src/CLI -- show-balances`

If using devnet, provide devnet Helius endpoints.