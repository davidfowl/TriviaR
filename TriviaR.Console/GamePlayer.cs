﻿using TriviaR;

// Ideally this would implement IGamePlayer, but there's an issue with cancellation tokens
class GamePlayer
{
    private readonly AsyncConsole _input;
    private TimeSpan _timeoutPerQuestion;
    private int _totalQuestions;

    public GamePlayer()
    {
        _input = new AsyncConsole();
    }

    public void GameStarted(GameConfiguration gameConfiguration)
    {
        _timeoutPerQuestion = TimeSpan.FromSeconds(gameConfiguration.QuestionTimeout);
        _totalQuestions = gameConfiguration.NumberOfQuestions;

        // Console.Beep();
        Console.Clear();
        Console.WriteLine($"Game {gameConfiguration.Name} has started. Prepare to answer {gameConfiguration.NumberOfQuestions} trivia questions!");
    }
    public void GameCompleted(string game, int correct)
    {
        Console.Clear();
        Console.WriteLine($"Game {game} has completed.");

        Console.WriteLine($"You scored {correct}/{_totalQuestions}!");
    }

    public async Task<GameAnswer> AskQuestion(GameQuestion question)
    {
        Console.Clear();
        Console.WriteLine($"You have {_timeoutPerQuestion.TotalSeconds} seconds to answer this question.");
        Console.WriteLine(question.Question);

        var index = 0;
        foreach (var choice in question.Choices)
        {
            Console.WriteLine($"{index}. {choice}");
            index++;
        }

        using var cts = new CancellationTokenSource(_timeoutPerQuestion);

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