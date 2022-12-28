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
