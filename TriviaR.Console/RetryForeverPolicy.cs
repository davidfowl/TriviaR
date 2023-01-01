using Microsoft.AspNetCore.SignalR.Client;

namespace TriviaR;

internal class RetryForeverPolicy : IRetryPolicy
{
    public TimeSpan? NextRetryDelay(RetryContext retryContext)
    {
        return retryContext.PreviousRetryCount switch
        {
            0 => TimeSpan.FromSeconds(2),
            1 => TimeSpan.FromSeconds(5),
            2 => TimeSpan.FromSeconds(10),
            3 => TimeSpan.FromSeconds(20),
            _ => TimeSpan.FromSeconds(40),
        };
    }
}
