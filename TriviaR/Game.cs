namespace TriviaR;

using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

class Game
{
    private static readonly TimeSpan _gameTransitionDelay = TimeSpan.FromSeconds(5);

    private readonly GameOptions _options;
    private readonly TimeSpan _serverTimeout;

    // Injected dependencies
    private readonly IHubContext<GameHub, IGamePlayer> _hubContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger _logger;

    // Player state keyed by connection id
    private readonly ConcurrentDictionary<string, PlayerState> _players = new();

    // Notification when the game is completed
    private readonly CancellationTokenSource _completedCts = new();

    // Number of open player slots in this game
    private readonly Channel<int> _playerSlots;

    public Game(IHubContext<GameHub, IGamePlayer> hubContext,
                IHttpClientFactory httpClientFactory,
                ILogger<Game> logger,
                IOptionsMonitor<GameOptions> options)
    {
        _hubContext = hubContext;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _options = options.CurrentValue;
        _playerSlots = Channel.CreateBounded<int>(_options.MaxPlayersPerGame);

        Name = RandomNameGenerator.GenerateRandomName();
        Group = hubContext.Clients.Group(Name);

        // Give the client some buffer
        _serverTimeout = TimeSpan.FromSeconds(_options.TimePerQuestion + 5);

        // Fill the slots for this game
        for (int i = 0; i < _options.MaxPlayersPerGame; i++)
        {
            _playerSlots.Writer.TryWrite(0);
        }
    }

    public string Name { get; }

    private IGamePlayer Group { get; }

    public CancellationToken Completed => _completedCts.Token;

    public async Task<bool> AddPlayerAsync(string connectionId)
    {
        // Try to grab a player slot
        if (_playerSlots.Reader.TryRead(out _))
        {
            // We succeeded so set up this player
            _players.TryAdd(connectionId, new PlayerState
            {
                Proxy = _hubContext.Clients.Client(connectionId)
            });

            await _hubContext.Groups.AddToGroupAsync(connectionId, Name);

            await _hubContext.Clients.GroupExcept(Name, connectionId).WriteMessage($"A new player joined game {Name}");

            var waitingForPlayers = true;

            // If we don't have any more slots, it means we're full, lets start the game.
            if (!_playerSlots.Reader.TryPeek(out _))
            {
                // Complete the channel so players can no longer join the game
                _playerSlots.Writer.TryComplete();

                // Check to see any slots were given back from players that might have dropped from the game while waiting on the game to start.
                // We check this after TryComplete since it means no new players can join.
                if (!_playerSlots.Reader.TryPeek(out _))
                {
                    waitingForPlayers = false;

                    // We're clear, start the game
                    _ = Task.Run(PlayGame);
                }

                // More players can join, let's wait
            }

            if (waitingForPlayers)
            {
                await Group.WriteMessage($"Waiting for {_playerSlots.Reader.Count} player(s) to join.");
            }

            return true;
        }

        return false;
    }

    public async Task RemovePlayerAsync(string connectionId)
    {
        // This should never be false, since we only remove players from games they are associated with
        if (_players.TryRemove(connectionId, out _))
        {
            // If the game hasn't started (the channel was completed for e.g.), we can give this slot back to the game.
            _playerSlots.Writer.TryWrite(0);

            await Group.WriteMessage($"A player has left the game");
        }
    }

    // This method runs the entire game loop
    private async Task PlayGame()
    {
        // Ask the player a question until we get a valid answer
        static async Task<(PlayerState, GameAnswer?)> AskPlayerQuestion(PlayerState playerState, GameQuestion question, CancellationToken cancellationToken)
        {
            try
            {
                var player = playerState.Proxy;

                while (true)
                {
                    // Ask the player this question and wait for the response
                    var answer = await player.AskQuestion(question, cancellationToken);

                    // If it's a valid choice, the return the answer
                    if (answer.Choice >= 0 && answer.Choice < question.Choices.Length)
                    {
                        await player.WriteMessage("Answer received. Waiting for other players to answer.");

                        return (playerState, answer);
                    }

                    // REVIEW: We can tell the player the choice in invalid here
                }
            }
            catch
            {
                // We don't want to throw exceptions when answers don't come back successfully.
                return (playerState, null);
            }
        }

        var questionsPerGame = _options.QuestionsPerGame;
        var maxPlayersPerGame = _options.MaxPlayersPerGame;
        var timePerQuestion = _options.TimePerQuestion;

        // Did everyone rage quit the game? Then no point asking anymore questions
        // nobody can join mid-game.
        var emptyGame = false;

        // The per question cancellation token source
        var questionTimoutTokenSource = new CancellationTokenSource();

        try
        {
            // Get the trivia questions for this game
            var client = _httpClientFactory.CreateClient();
            var triviaApi = new TriviaApi(client);

            var triviaQuestions = await triviaApi.GetQuestionsAsync(questionsPerGame);

            var playerAnswers = new List<Task<(PlayerState, GameAnswer?)>>(maxPlayersPerGame);

            var configuration = new GameConfiguration
            {
                NumberOfQuestions = triviaQuestions.Length,
                QuestionTimeout = timePerQuestion
            };

            await Group.GameStarted(configuration);

            await Task.Delay(_gameTransitionDelay);

            var questionId = 0;
            foreach (var question in triviaQuestions)
            {
                // Prepare the question to send to the client
                var (gameQuestion, indexOfCorrectAnswer) = CreateGameQuestion(question);

                // Each question has a timeout (give the client some buffer before the server stops waiting for a reply)
                questionTimoutTokenSource.CancelAfter(_serverTimeout);

                // Clear the answers from the previous round
                playerAnswers.Clear();

                _logger.LogInformation("Asking question {QuestionId}", questionId);

                // Ask the players the question concurrently
                foreach (var (_, player) in _players)
                {
                    playerAnswers.Add(AskPlayerQuestion(player, gameQuestion, questionTimoutTokenSource.Token));
                }

                // Detect if all players exit the game. This is an optimization so we can clean up early.
                emptyGame = playerAnswers.Count == 0;

                if (emptyGame)
                {
                    // Early exit if there are no players
                    break;
                }

                // Wait for all of the responses to come back (or timeouts).
                await Task.WhenAll(playerAnswers);

                if (!questionTimoutTokenSource.TryReset())
                {
                    // We were unable to reset so make a new token
                    questionTimoutTokenSource = new();
                }

                _logger.LogInformation("Received all answers for question {QuestionId}", questionId);

                // Observe the valid responses to questions
                foreach (var (player, answer) in playerAnswers.Select(t => t.Result))
                {
                    // Increment the correct answers for this player
                    if (answer?.Choice == indexOfCorrectAnswer)
                    {
                        player.Correct++;
                        await player.Proxy.WriteMessage($"{question.CorrectAnswer} is correct!");
                    }
                    else if (answer is not null)
                    {
                        await player.Proxy.WriteMessage($"That answer is incorrect! The correct answer is {question.CorrectAnswer}.");
                    }
                    else
                    {
                        await player.Proxy.WriteMessage($"The correct answer is {question.CorrectAnswer}.");
                    }
                }

                questionId++;

                if (questionId < questionsPerGame)
                {
                    // Tell each player that we're moving to the next question
                    await Group.WriteMessage($"Moving to the next question in {_gameTransitionDelay.TotalSeconds} seconds...");

                    await Task.Delay(_gameTransitionDelay);
                }
            }

            if (!emptyGame)
            {
                await Group.WriteMessage("Calculating scores...");

                await Task.Delay(_gameTransitionDelay);

                // Report the scores
                foreach (var (_, player) in _players)
                {
                    await player.Proxy.GameCompleted(new GameCompletedEvent
                    {
                        Name = Name,
                        Correct = player.Correct
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "The processing for game {Name} failed unexpectedly", Name);

            await Group.WriteMessage($"The processing for game {Name} failed unexpectedly: {ex}");
        }
        finally
        {
            _logger.LogInformation("The game {Name} has run to completion.", Name);

            questionTimoutTokenSource.Dispose();

            // Signal that we're done
            _completedCts.Cancel();
        }
    }

    static (GameQuestion, int) CreateGameQuestion(TriviaQuestion question)
    {
        static void Shuffle<T>(T[] array)
        {
            // In-place Fisher-Yates shuffle
            for (int i = 0; i < array.Length - 1; ++i)
            {
                int j = Random.Shared.Next(i, array.Length);
                (array[j], array[i]) = (array[i], array[j]);
            }
        }

        // Copy the choices into an array and shuffle
        var choices = new string[question.IncorrectAnswers.Length + 1];
        choices[^1] = question.CorrectAnswer;
        for (int i = 0; i < question.IncorrectAnswers.Length; i++)
        {
            choices[i] = question.IncorrectAnswers[i];
        }

        // Shuffle the choices so it's randomly placed
        Shuffle(choices);

        var indexOfCorrectAnswer = choices.AsSpan().IndexOf(question.CorrectAnswer);
        var gameQuestion = new GameQuestion
        {
            Question = question.Question,
            Choices = choices
        };

        return (gameQuestion, indexOfCorrectAnswer);
    }

    private sealed class PlayerState
    {
        public required IGamePlayer Proxy { get; init; }
        public int Correct { get; set; }
    }
}
