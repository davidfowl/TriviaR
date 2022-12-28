namespace TriviaR;

using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

/// <summary>
/// The game factory keeps track of games waiting to be started, running and completed games.
/// </summary>
class GameFactory
{
    private readonly IHubContext<GameHub, IGamePlayer> _hubContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILoggerFactory _loggerFactory;

    // FIFO queue of games waiting to be played.
    private readonly ConcurrentQueue<Game> _waitingGames = new();

    // The set of active or completed games.
    private readonly ConcurrentDictionary<string, Game> _activeGames = new();

    public GameFactory(IHubContext<GameHub, IGamePlayer> hubContext,
                       IHttpClientFactory httpClientFactory,
                       ILoggerFactory loggerFactory)
    {
        _hubContext = hubContext;
        _httpClientFactory = httpClientFactory;
        _loggerFactory = loggerFactory;
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
                        // Remove it from the list of waiting games after we've made it active
                        _waitingGames.TryDequeue(out _);
                    }
                    continue;
                }

                return game;
            }

            // Generate a new name and add a new game to the queue
            var name = RandomNameGenerator.GenerateRandomName();
            var logger = _loggerFactory.CreateLogger(name);

            _waitingGames.Enqueue(new Game(_hubContext, _httpClientFactory, logger, name));
        }
    }
}
