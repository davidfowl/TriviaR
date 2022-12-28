namespace TriviaR;

using Microsoft.AspNetCore.SignalR;

class GameHub : Hub<IGamePlayer>
{
    private readonly GameFactory _gameFactory;

    public GameHub(GameFactory gameFactory)
    {
        _gameFactory = gameFactory;
    }

    public override async Task OnConnectedAsync()
    {
        // Store this game as part of the connection state
        Context.Items[typeof(Game)] = await _gameFactory.AddPlayerToGameAsync(Context.ConnectionId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items[typeof(Game)] is Game game)
        {
            await game.RemovePlayerAsync(Context.ConnectionId);
        }
    }
}
