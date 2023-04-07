using BasicWsGameServer.Server.Repositories;
using Newtonsoft.Json;

namespace BasicWsGameServer.Server.Handlers;

public class UpdateResourcesHandler
{
    private readonly PlayersRepository _playersRepository;

    public UpdateResourcesHandler(PlayersRepository playersRepository)
    {
        _playersRepository = playersRepository;
    }

    public async Task HandleAsync(HttpContext context)
    {
        var playerId = context.Request.Query["playerId"].ToString();
        var resourceType = context.Request.Query["resourceType"].ToString();
        var resourceValue = int.Parse(context.Request.Query["resourceValue"].ToString());

        var player = _playersRepository.Get(playerId);

        if (resourceType == "coins")
        {
            player.Coins += resourceValue;
        }
        else if (resourceType == "rolls")
        {
            player.Rolls += resourceValue;
        }
        else
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("Invalid resource type");
            return;
        }

        _playersRepository.Update(player);

        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonConvert.SerializeObject(new { coins = player.Coins, rolls = player.Rolls }));
    }
}
