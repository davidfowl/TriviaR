namespace TriviaR;

using Microsoft.AspNetCore.SignalR;

class GameHub : Hub<IGamePlayer>
{
    private readonly Lobby _gameFactory;

    public GameHub(Lobby gameFactory) => _gameFactory = gameFactory;

    public async Task<string> JoinGame()
    {
        Game game = await _gameFactory.AddPlayerToGameAsync(Context);

        return game.Name;
    }
}
