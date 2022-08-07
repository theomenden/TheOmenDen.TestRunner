namespace TheOmenDen.TestRunner.Shared;

public partial class TestRunnerFooter: ComponentBase
{
    [Parameter] public EventCallback<string> ThemeColorChanged { get; init; }

    private static string AssemblyProductVersion
    {
        get
        {
            var attributes = Assembly.GetExecutingAssembly()
                .GetCustomAttributes(typeof(AssemblyFileVersionAttribute), false);
            return attributes.Length == 0 
                ? String.Empty 
                : ((AssemblyFileVersionAttribute)attributes[0]).Version;
        }
    }

    private static string ApplicationDevelopmentCompany
    {
        get
        {
            var attributes = Assembly.GetExecutingAssembly()
                .GetCustomAttributes(typeof(AssemblyCompanyAttribute), false);
            return attributes.Length == 0 
                ? "The Omen Den L.L.C."
                : ((AssemblyCompanyAttribute)attributes[0]).Company;
        }
    }
}