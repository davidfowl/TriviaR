namespace TriviaR;

using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;

class Game
{
    // TODO: Make this configuration based
    private const int MaxPlayersPerGame = 4;
    private const int TimePerQuestion = 20;
    private const int QuestionsPerGame = 5;

    private readonly ConcurrentDictionary<string, IGamePlayer> _players = new();
    private readonly IHubContext<GameHub, IGamePlayer> _hubContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger _logger;

    // Number of open player slots in this game
    private readonly Channel<int> _playerSlots = Channel.CreateBounded<int>(MaxPlayersPerGame);

    public Game(IHubContext<GameHub, IGamePlayer> hubContext,
                IHttpClientFactory httpClientFactory,
                ILogger<Game> logger)
    {
        _hubContext = hubContext;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        Name = RandomNameGenerator.GenerateRandomName();
        Group = hubContext.Clients.Group(Name);

        // Fill the slots for this game
        for (int i = 0; i < MaxPlayersPerGame; i++)
        {
            _playerSlots.Writer.TryWrite(0);
        }
    }

    public string Name { get; }

    private IGamePlayer Group { get; }

    public bool Completed { get; private set; }

    public async Task<bool> AddPlayerAsync(string connectionId)
    {
        // Try to grab a player slot
        if (_playerSlots.Reader.TryRead(out _))
        {
            // We succeeded so set up this player
            _players.TryAdd(connectionId, _hubContext.Clients.Client(connectionId));

            await _hubContext.Groups.AddToGroupAsync(connectionId, Name);

            await Group.WriteMessage($"A new player joined game {Name}");

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
                    _ = PlayGame();
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
        static async Task<(string, GameAnswer)> AskPlayerQuestion(string id,
            IGamePlayer player, int timeoutInSeconds, GameQuestion question, CancellationToken cancellationToken)
        {
            while (true)
            {
                // Ask the player this question and wait for the response
                var answer = await player.AskQuestion(question, timeoutInSeconds, cancellationToken);

                // If it's a valid choice, the return the answer
                if (answer.Choice >= 0 && answer.Choice < question.Choices.Length)
                {
                    await player.WriteMessage("Answer received. Waiting for other players to answer.");

                    return (id, answer);
                }

                // REVIEW: We can tell the player the choice in invalid here
            }
        }

        var client = _httpClientFactory.CreateClient();
        var triviaApi = new TriviaApi(client);

        // Did everyone quit the game? Then no point asking anymore questions
        // nobody can join mid-game.
        var emptyGame = false;

        // The per question cancellation token source
        var questionCts = new CancellationTokenSource();

        try
        {
            await Group.GameStarted(Name);

            // Get the trivia questions for this game
            var triviaQuestions = await triviaApi.GetQuestionsAsync(QuestionsPerGame);

            var playerAnswers = new List<Task<(string, GameAnswer)>>(MaxPlayersPerGame);
            var allPlayerAnswers = new Dictionary<string, bool[]>();

            // Stores if the player was right for a specific round
            foreach (var (id, _) in _players)
            {
                allPlayerAnswers[id] = new bool[triviaQuestions.Length];
            }

            await Group.WriteMessage($"Retrieved {triviaQuestions.Length} questions...");

            await Task.Delay(3000);

            var questionId = 0;
            foreach (var question in triviaQuestions)
            {
                // Copy the choices into an array
                var choices = new string[question.IncorrectAnswers.Length + 1];
                choices[^1] = question.CorrectAnswer;
                for (int i = 0; i < question.IncorrectAnswers.Length; i++)
                {
                    choices[i] = question.IncorrectAnswers[i];
                }

                // Shuffle the choices so it's randomly placed
                Shuffle(choices);

                var gameQuestion = new GameQuestion
                {
                    Question = question.Question,
                    Choices = choices
                };

                // Each question has a timeout (give the client some buffer before the server stops waiting for a reply)
                questionCts.CancelAfter(TimeSpan.FromSeconds(TimePerQuestion + 5));

                // Clear the answers from the last round
                playerAnswers.Clear();

                _logger.LogInformation("Asking question {QuestionId}", questionId);

                emptyGame = true;

                foreach (var (id, player) in _players)
                {
                    emptyGame = false;
                    playerAnswers.Add(AskPlayerQuestion(id, player, TimePerQuestion, gameQuestion, questionCts.Token));
                }

                if (emptyGame)
                {
                    break;
                }

                // We don't want to throw exceptions when answers don't come back
                await Task.WhenAll(playerAnswers).NoThrow();

                if (!questionCts.TryReset())
                {
                    // We were unable to reset so make a new token
                    questionCts = new();
                }

                _logger.LogInformation("Received all answers for question {QuestionId}", questionId);

                // Observe the valid responses to questions
                foreach (var (id, answer) in playerAnswers.Where(t => t.IsCompletedSuccessfully).Select(t => t.Result))
                {
                    var isCorrect = answer.Choice is { } choice && choices[choice] == question.CorrectAnswer;
                    allPlayerAnswers[id][questionId] = isCorrect;

                    // The player might have left so, check if they are still around
                    if (_players.TryGetValue(id, out var player))
                    {
                        if (isCorrect)
                        {
                            await player.WriteMessage($"That answer is correct!");
                        }
                        else
                        {
                            var indexOfCorrectAnswer = Array.IndexOf(choices, question.CorrectAnswer);
                            await player.WriteMessage($"That answer is incorrect! The correct answer is {indexOfCorrectAnswer}. {question.CorrectAnswer}.");
                        }
                    }
                }

                questionId++;

                if (questionId < QuestionsPerGame)
                {
                    // Tell each player that we're moving to the next question
                    await Group.WriteMessage("Moving to the next question in 5 seconds...");

                    await Task.Delay(5000);
                }
            }

            if (!emptyGame)
            {
                await Group.WriteMessage("Tallying scores...");

                await Task.Delay(4000);

                // Tally the scores
                foreach (var (id, scores) in allPlayerAnswers)
                {
                    // The player might have left so, check if they are still around
                    if (_players.TryGetValue(id, out var player))
                    {
                        var correct = scores.Count(b => b);
                        var incorrect = scores.Count(b => !b);
                        await player.GameCompleted(Name, correct, incorrect);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "The processing for game {Name} failed unexpctedly", Name);

            await Group.WriteMessage($"The processing for game {Name} failed unexpctedly: {ex}");
        }
        finally
        {
            _logger.LogInformation("The game {Name} has run to completion.", Name);

            Completed = true;

            questionCts?.Dispose();
        }
    }

    static void Shuffle<T>(T[] array)
    {
        // In-place Fisher-Yates shuffle
        for (int i = 0; i < array.Length - 1; ++i)
        {
            int j = Random.Shared.Next(i, array.Length);
            (array[j], array[i]) = (array[i], array[j]);
        }
    }
}
