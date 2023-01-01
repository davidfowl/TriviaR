namespace TriviaR;

public class GameAnswer
{
    public int? Choice { get; set; }
}

public class GameQuestion
{
    public required string Question { get; set; }
    public required string[] Choices { get; set; }
}

public class GameConfiguration
{
    public required int NumberOfQuestions { get; init; }

    public required int QuestionTimeout { get; init; }
}

public class GameCompletedEvent
{
    public required string Name { get; init; }
    public required int Correct { get; init; }
}