using Coflnet.Sky.Updater.Models;
using Coflnet.Sky.ModCommands.Services;

namespace Coflnet.Sky.BFCS.Services;
public class IsSoldMock : IIsSold
{
    public bool IsSold(string uuid)
    {
        return false;
    }
}