using System.Threading.Tasks;
using Coflnet.Sky.Commands.MC;
using Coflnet.Sky.ModCommands.Services;
using Coflnet.Sky.ModCommands.Tutorials;

namespace Coflnet.Sky.BFCS.Services;

/// <summary>
/// Tutorial state is handled by main instance
/// </summary>
public class NothingTutorialService : ITutorialService
{
    public Task CommandInput(MinecraftSocket socket, string v)
    {
        return Task.CompletedTask;
    }

    public Task Trigger<T>(IMinecraftSocket socket) where T : TutorialBase
    {
        return Task.CompletedTask;
    }
}