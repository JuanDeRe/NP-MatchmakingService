namespace MatchmakingService.Configuration;

public class RedisOptions
{
    public string ConnectionString { get; set; } =
        "localhost:6379";

    public string InstanceName { get; set; } =
        "nightwatch:";
}