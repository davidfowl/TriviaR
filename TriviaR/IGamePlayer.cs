namespace TriviaR;

public interface IGamePlayer
{
    Task<GameAnswer> AskQuestion(GameQuestion question, int timeoutInSeconds, CancellationToken cancellationToken);

    Task WriteMessage(string message);

    Task GameStarted(string name);
    Task GameCompleted(string name, int correct, int incorrect);
}
