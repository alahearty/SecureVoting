namespace SecureVoting.Application.Configuration;

public static class SeedPolicy
{
    public static void EnsureAllowed(bool enabled, string environmentName)
    {
        if (!enabled)
        {
            return;
        }

        if (string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        throw new InvalidOperationException(
            "Demo seeding is only permitted in Development or Testing. Set Seed__Enabled=false outside those environments.");
    }
}
