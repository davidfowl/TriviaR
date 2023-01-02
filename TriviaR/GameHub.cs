namespace TriviaR;

using Microsoft.AspNetCore.SignalR;

class GameHub : Hub<IGamePlayer>
{
    private readonly GameFactory _gameFactory;

    public GameHub(GameFactory gameFactory) => _gameFactory = gameFactory;

    public async Task<string> JoinGame()
    {
        Game game = await _gameFactory.AddPlayerToGameAsync(Context);

        return game.Name;
    }
}
