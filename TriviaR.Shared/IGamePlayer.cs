namespace TriviaR;

public interface IGamePlayer
{
    Task<GameAnswer> AskQuestion(GameQuestion question, CancellationToken cancellationToken);
    Task WriteMessage(string message);
    Task GameStarted(GameConfiguration gameConfiguration);
    Task GameCompleted(GameCompletedEvent @event);
}
