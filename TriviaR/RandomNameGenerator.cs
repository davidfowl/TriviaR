namespace TriviaR;

internal static class RandomNameGenerator
{
    public static string GenerateRandomName()
    {
        return Guid.NewGuid().ToString();
    }
}
