using Microsoft.Extensions.Configuration;

namespace ConsertaPraMim.Infrastructure.Configuration;

public static class SystemSettingsConfigurationExtensions
{
    public static IConfigurationBuilder AddSystemSettingsOverridesFromDatabase(
        this IConfigurationBuilder builder)
    {
        var bootstrapConfiguration = builder.Build();
        var connectionString = bootstrapConfiguration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return builder;
        }

        builder.Add(new SystemSettingsConfigurationSource(connectionString));
        return builder;
    }
}
