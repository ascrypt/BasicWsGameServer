namespace BasicWsGameServer.Server.Entities;

public class Player
{
    public string Id { get; set; }
    public int Coins { get; set; }
    public int Rolls { get; set; }
    public bool IsOnline { get; set; }
}