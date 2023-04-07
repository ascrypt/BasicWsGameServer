using BasicWsGameServer.Server.Entities;
using BasicWsGameServer.Server.Repositories;
using Newtonsoft.Json;

namespace BasicWsGameServer.Server.Handlers;

public class SendGiftHandler
{
    private readonly PlayersRepository _playersRepository;

    public SendGiftHandler(PlayersRepository playersRepository)
    {
        _playersRepository = playersRepository;
    }

    public async Task HandleAsync(HttpContext context)
    {
        var playerId = context.Request.Query["playerId"].ToString();
        var friendPlayerId = context.Request.Query["friendPlayerId"].ToString();
        var resourceType = context.Request.Query["resourceType"].ToString();
        var resourceValue = int.Parse(context.Request.Query["resourceValue"].ToString());
        var player = _playersRepository.Get(playerId);
        var friendPlayer = _playersRepository.Get(friendPlayerId);

        if (friendPlayer == null)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("Friend player not found");
            return;
        }

        if (resourceType == "coins")
        {
            if (player.Coins < resourceValue)
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("Insufficient coins");
                return;
            }

            player.Coins -= resourceValue;
            friendPlayer.Coins += resourceValue;
        }
        else if (resourceType == "rolls")
        {
            if (player.Rolls < resourceValue)
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("Insufficient rolls");
                return;
            }

            player.Rolls -= resourceValue;
            friendPlayer.Rolls += resourceValue;
        }
        else
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("Invalid resource type");
            return;
        }

        _playersRepository.Update(player);
        _playersRepository.Update(friendPlayer);

        if (friendPlayer.IsOnline)
        {
            var giftEvent = new GiftEvent
            {
                FromPlayerId = playerId,
                ToPlayerId = friendPlayerId,
                ResourceType = resourceType,
                ResourceValue = resourceValue
            };

            //send event to friend player
        }

        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(
            JsonConvert.SerializeObject(new { coins = player.Coins, rolls = player.Rolls }));
    }
}
