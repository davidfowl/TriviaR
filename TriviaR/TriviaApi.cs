namespace TriviaR;

public class TriviaApi
{
    private readonly HttpClient _client;
    public TriviaApi(HttpClient client)
    {
        _client = client;
    }

    public async Task<TriviaQuestion[]> GetQuestionsAsync(int numberOfQuestions)
    {
        var results = new List<TriviaQuestion>();

        while (results.Count < numberOfQuestions)
        {
            // Get 10 trivia questions that match a certain criteria
            var triviaQuestions = await _client.GetFromJsonAsync<TriviaQuestion[]>("?limit=10");

            foreach (var q in triviaQuestions!)
            {
                if (q.Type == "Multiple Choice" && !q.IsNiche)
                {
                    results.Add(q);

                    if (results.Count == numberOfQuestions)
                    {
                        break;
                    }
                }
            }
        }

        return results.ToArray();
    }
}
