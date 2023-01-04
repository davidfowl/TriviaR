namespace TriviaR;

using System;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

/// <summary>
/// The game factory keeps track of games waiting to be started, running and completed games.
/// </summary>
class Lobby
{
    private readonly IServiceProvider _serviceProvider;

    // FIFO queue of games waiting to be played.
    private readonly ConcurrentQueue<Game> _waitingGames = new();

    // The set of active or completed games.
    private readonly ConcurrentDictionary<string, Game> _activeGames = new();

    // The key into the per connection dictionary used to look up the current game;
    private static readonly object _gameKey = new();

    public Lobby(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<Game> AddPlayerToGameAsync(HubCallerContext hubCallerContext)
    {
        // There's already a game associated with this player, just return it
        if (hubCallerContext.Items[_gameKey] is Game g)
        {
            return g;
        }

        while (true)
        {
            // Try to get a waiting game from the queue (the longest waiting game is served first FIFO)
            if (_waitingGames.TryPeek(out var game))
            {
                // Try to add the player to this game. It'll return false if the game is full.
                if (!await game.AddPlayerAsync(hubCallerContext.ConnectionId))
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
                else
                {
                    // Store the association of this player to this game
                    hubCallerContext.Items[_gameKey] = game;

                    // When the player disconnects, remove them from the game
                    hubCallerContext.ConnectionAborted.Register(() =>
                    {
                        // We can't wait here (since this is synchronous), so fire and forget
                        _ = game.RemovePlayerAsync(hubCallerContext.ConnectionId);
                    });

                    // When the game ends, remove the game from the player (they can join another game)
                    game.Completed.Register(() => hubCallerContext.Items.Remove(_gameKey));
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
