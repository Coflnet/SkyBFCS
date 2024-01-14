using Coflnet.Sky.ModCommands.Services;

namespace Coflnet.Sky.BFCS.Services;

public class NeverIsSoldService : IIsSold
{
    public bool IsSold(string id)
    {
        return false;
    }
}
