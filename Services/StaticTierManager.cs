using System;
using Coflnet.Sky.Commands.MC;
using Coflnet.Sky.Commands.Shared;
using System.Threading.Tasks;

namespace Coflnet.Sky.BFCS.Services;

public class StaticTierManager : IAccountTierManager
{
    private AccountTier tier;
    private DateTime expiresAt;

    public StaticTierManager(ProxyReqSyncCommand.Format data)
    {
        Update(data);
    }

    public void Update(ProxyReqSyncCommand.Format data)
    {
        tier = data.SessionInfo.SessionTier;
        expiresAt = data.AccountInfo.ExpiresAt;
    }

    public DateTime ExpiresAt => expiresAt;

    public event EventHandler<AccountTier> OnTierChange;

    public Task ChangeDefaultTo(string mcUuid)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        // ignore
    }

    public Task<AccountTier> GetCurrentCached()
    {
        return Task.FromResult(tier);
    }

    public Task<(AccountTier tier, DateTime expiresAt)> GetCurrentTierWithExpire()
    {
        return Task.FromResult((tier, expiresAt));
    }

    public string GetSessionInfo()
    {
        return tier.ToString();
    }

    public bool HasAtLeast(AccountTier tier)
    {
        return this.tier >= tier;
    }

    public bool IsConnectedFromOtherAccount(out string otherAccount, out AccountTier tier)
    {
        throw new NotImplementedException();
    }

    public Task RefreshTier()
    {
        return Task.CompletedTask;
    }
}
