namespace TheOmenDen.TestRunner.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddXUnitServices(this IServiceCollection services)
    {
        services.AddScoped<ITestContextService, TestContextService>();

        return services;
    }
}
