using BasicWsGameServer.Server.Entities;

namespace BasicWsGameServer.Server.Repositories;

using Microsoft.Extensions.Caching.Memory;

public class PlayersRepository
{
    private readonly IMemoryCache _cache;

    public PlayersRepository(IMemoryCache cache)
    {
        _cache = cache;
    }

    public Player Get(string id)
    {
        return _cache.Get<Player>(id);
    }

    public void Add(Player player)
    {
        _cache.Set(player.Id, player);
    }

    public void Update(Player player)
    {
        _cache.Set(player.Id, player);
    }
}
