using Microsoft.AspNetCore.SignalR.Client;
using TriviaR;

Console.WriteLine("Welcome to TriviaR");

var connection = new HubConnectionBuilder()
                    .WithUrl("https://localhost:7062/trivia")
                    // .ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Debug).AddConsole())
                    .Build();

var player = new GamePlayer();

connection.On<string>(nameof(GamePlayer.WriteMessage), player.WriteMessage);
connection.On<GameQuestion, int, GameAnswer>(nameof(GamePlayer.AskQuestion), player.AskQuestion);
connection.On<string, int>(nameof(GamePlayer.GameStarted), player.GameStarted);
connection.On<string, int, int>(nameof(GamePlayer.GameCompleted), player.GameCompleted);

var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

connection.Closed += (ex) =>
{
    Console.WriteLine("The connection was closed");
    tcs.TrySetResult();
    return Task.CompletedTask;
};

await connection.StartAsync();

// Wait for SIGTERM/Control+C
Console.CancelKeyPress += (sender, e) =>
{
    e.Cancel = true;
    tcs.TrySetResult();
};

await tcs.Task;
