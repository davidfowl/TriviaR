namespace TriviaR;

internal static class RandomNameGenerator
{
    private static readonly string[] Adjectives = { "adorable", "amazing", "brave", "charming", "clever", "dashing", "dazzling", "elegant", "fierce", "friendly", "funny", "gentle", "glorious", "handsome", "happy", "helpful", "jolly", "kind", "lively", "lovely", "loyal", "nice", "perfect", "polite", "powerful", "proud", "silly", "talented", "thoughtful", "trustworthy", "wise" };
    private static readonly string[] Nouns = { "ant", "bird", "cat", "chicken", "cow", "dog", "dolphin", "duck", "elephant", "fish", "giraffe", "goat", "hamster", "horse", "kangaroo", "lion", "monkey", "otter", "panda", "pig", "rabbit", "snake", "tiger", "turtle", "wolf" };

    public static string GenerateRandomName()
    {
        var adjective = Adjectives[Random.Shared.Next(Adjectives.Length)];
        var noun = Nouns[Random.Shared.Next(Nouns.Length)];
        var id = Guid.NewGuid().ToString()[0..4];

        return $"{adjective}_{noun}_{id}";
    }
}
