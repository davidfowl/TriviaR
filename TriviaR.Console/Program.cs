using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR.Client;
using TriviaR;

var connection = new HubConnectionBuilder()
                    .WithUrl("https://localhost:7062/trivia")
                    .WithAutomaticReconnect(new RetryForeverPolicy())
                    .Build();

var asyncConsole = new AsyncConsole();
var events = Channel.CreateUnbounded<bool>();

// Write a single event that will signal the start of a new game
events.Writer.TryWrite(true);

var player = new GamePlayer(asyncConsole, events);

connection.On<string>(nameof(GamePlayer.WriteMessage), player.WriteMessage);
connection.On<GameQuestion, GameAnswer>(nameof(GamePlayer.AskQuestion), player.AskQuestion);
connection.On<GameConfiguration>(nameof(GamePlayer.GameStarted), player.GameStarted);
connection.On<GameCompletedEvent>(nameof(GamePlayer.GameCompleted), player.GameCompleted);

connection.Closed += (ex) =>
{
    Console.WriteLine("Reconnect attempts failed.");

    return Task.CompletedTask;
};

connection.Reconnecting += (ex) =>
{
    Console.WriteLine("Connection dropped. Attempting to reconnect...");

    return Task.CompletedTask;
};

connection.Reconnected += (connectionId) =>
{
    Console.WriteLine("Successfully reconnected, attempting to join a new game.");

    // Join a game without prompting
    events.Writer.TryWrite(false);

    return Task.CompletedTask;
};

// Wait for SIGTERM/Control+C
Console.CancelKeyPress += (sender, e) =>
{
    e.Cancel = true;

    // No more events
    events.Writer.TryComplete();
};

Console.WriteLine("Welcome to TriviaR!");

// Write an event
await connection.StartAsync();

await foreach (var prompt in events.Reader.ReadAllAsync())
{
    if (prompt)
    {
        Console.WriteLine("Press any key to join new game, or CTRL + C to quit.");

        if (Console.ReadLine() is null) break;
    }

    var gameName = await connection.InvokeAsync<string>("JoinGame");

    Console.WriteLine($"Joined game {gameName}.");
}
