namespace TriviaR;

using Microsoft.AspNetCore.SignalR;

class GameHub : Hub<IGamePlayer>
{
    private readonly GameFactory _gameFactory;

    public GameHub(GameFactory gameFactory) => _gameFactory = gameFactory;

    public async Task<string> JoinGame()
    {
        var game = await _gameFactory.AddPlayerToGameAsync(Context.ConnectionId);
        Context.Items[typeof(Game)] = game;
        return game.Name;
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items[typeof(Game)] is Game game)
        {
            await game.RemovePlayerAsync(Context.ConnectionId);
        }
    }
}
