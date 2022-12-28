namespace TriviaR;

using System.Collections.Concurrent;

/// <summary>
/// The game factory keeps track of games waiting to be started, running and completed games.
/// </summary>
class GameFactory
{
    private readonly IServiceProvider _serviceProvider;

    // FIFO queue of games waiting to be played.
    private readonly ConcurrentQueue<Game> _waitingGames = new();

    // The set of active or completed games.
    private readonly ConcurrentDictionary<string, Game> _activeGames = new();

    public GameFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<Game> AddPlayerToGameAsync(string connectionId)
    {
        while (true)
        {
            // Try to get a waiting game from the queue (the longest waiting game is served first FIFO)
            if (_waitingGames.TryPeek(out var game))
            {
                // Try to add the player to this game. It'll return false if the game is full.
                if (!await game.AddPlayerAsync(connectionId))
                {
                    // We're unable to use this waiting game, so make it an active game.
                    if (_activeGames.TryAdd(game.Name, game))
                    {
                        // Remove the game when it completes
                        game.Completed.UnsafeRegister(_ =>
                        {
                            _activeGames.TryRemove(game.Name, out var _);
                        }, 
                        null);

                        // Remove it from the list of waiting games after we've made it active
                        _waitingGames.TryDequeue(out _);
                    }
                    continue;
                }

                return game;
            }

            // This works because games are transient so a new one gets created
            // when it is requested
            var newGame = _serviceProvider.GetRequiredService<Game>();

            _waitingGames.Enqueue(newGame);
        }
    }
}
