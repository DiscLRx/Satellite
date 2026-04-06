namespace Server;

public class PeConfigurationSource : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new PeConfigurationProvider();
    }
}

public class PeConfigurationProvider : ConfigurationProvider
{
    public override void Load()
    {
        Data = new Dictionary<string, string?>();
    }
}

public static class ConfigurationExtension
{
    public static IConfigurationBuilder AddEmptyConfiguration(this IConfigurationBuilder builder)
    {
        return builder.Add(new PeConfigurationSource());
    }
}