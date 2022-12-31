using TriviaR;

// Ideally this would implement IGamePlayer, but there's an issue with cancellation tokens
class GamePlayer
{
    private readonly AsyncConsole _input;

    public GamePlayer()
    {
        _input = new AsyncConsole();
    }

    public void GameStarted(string game, int numberOfQuestions)
    {
        // Console.Beep();
        Console.Clear();
        Console.WriteLine($"Game {game} has started. Prepare to answer {numberOfQuestions} trivia questions!");
    }
    public void GameCompleted(string game, int correct, int incorrect)
    {
        Console.Clear();
        Console.WriteLine($"Game {game} has completed.");

        Console.WriteLine($"You scored {correct}/{incorrect + correct}!");
    }

    public async Task<GameAnswer> AskQuestion(GameQuestion question, int timeoutInSeconds)
    {
        Console.Clear();
        Console.WriteLine($"You have {timeoutInSeconds} seconds to answer this question.");
        Console.WriteLine(question.Question);

        var index = 0;
        foreach (var choice in question.Choices)
        {
            Console.WriteLine($"{index}. {choice}");
            index++;
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutInSeconds));

        Console.WriteLine();
        Console.Write("Answer: ");
        while (true)
        {
            try
            {
                var choiceLine = await _input.ReadLineAsync(cts.Token);

                if (int.TryParse(choiceLine, out var choice) && choice >= 0 && choice < index)
                {
                    return new GameAnswer { Choice = choice };
                }

                if (choiceLine is not null)
                {
                    Console.WriteLine($"Invalid answer, please choose an answer between {0} and {index - 1}");
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Times up!");
                throw;
            }
        }
    }

    public void WriteMessage(string message) => Console.WriteLine(message);

    public void PlayerJoinedGame(string game)
    {
        Console.WriteLine($"A player joined {game}");
    }

    public void PlayerLeftGame(string game)
    {
        Console.WriteLine($"A player left {game}");
    }
}