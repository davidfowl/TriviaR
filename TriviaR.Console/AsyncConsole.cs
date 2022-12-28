using System.Threading.Channels;

namespace TriviaR;

// Console IO is still is a blocking operation and there's no way to cancel it.
// We're using this long running task to read from the console and write into a channel
// so the application logic can cancel reads without blocking the SignalR client connection event loop.
internal class AsyncConsole
{
    private readonly Channel<string> _channel;
    private readonly ManualResetEvent _mre = new(false);

    public AsyncConsole()
    {
        _channel = Channel.CreateUnbounded<string>();

        _ = Task.Factory.StartNew(() =>
        {
            while (true)
            {
                // Wait for the next signal to read
                _mre.WaitOne();

                var line = Console.ReadLine();
                if (line is null)
                {
                    _channel.Writer.TryComplete();
                    break;
                }
                else
                {
                    _channel.Writer.TryWrite(line);
                }

                // Reset the event after we've consumed data from the console
                _mre.Reset();
            }
        },
        TaskCreationOptions.LongRunning);
    }

    public async ValueTask<string> ReadLineAsync(CancellationToken cancellationToken)
    {
        // Issue a new read if we aren't already waiting on one
        _mre.Set();
        return await _channel.Reader.ReadAsync(cancellationToken);
    }
}
