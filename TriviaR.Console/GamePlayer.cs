using TriviaR;

class GamePlayer
{
    private readonly AsyncConsole _input;

    public GamePlayer()
    {
        _input = new AsyncConsole();
    }

    public void GameStarted(string name)
    {
        // Console.Beep();
        Console.Clear();
        Console.WriteLine($"Game {name} has started.");
    }
    public void GameCompleted(string name, int correct, int incorrect)
    {
        Console.Clear();
        Console.WriteLine($"Game {name} has completed.");

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
}