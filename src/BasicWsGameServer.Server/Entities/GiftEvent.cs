namespace BasicWsGameServer.Server.Entities;

public class GiftEvent
{
    public string FromPlayerId { get; set; }
    public string ToPlayerId { get; set; }
    public string ResourceType { get; set; }
    public int ResourceValue { get; set; }
}