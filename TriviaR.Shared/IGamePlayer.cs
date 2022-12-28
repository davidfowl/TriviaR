namespace TriviaR;

public interface IGamePlayer
{
    Task<GameAnswer> AskQuestion(GameQuestion question, int timeoutInSeconds, CancellationToken cancellationToken);

    Task WriteMessage(string message);

    Task GameStarted(string game);
    Task GameCompleted(string game, int correct, int incorrect);
}
