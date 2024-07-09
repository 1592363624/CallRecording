using System.IO;
using System.Windows;
using Microsoft.Extensions.Configuration;

namespace CallRecording;

public partial class App : Application
{
    public IConfiguration Configuration { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", false, true);
        Configuration = builder.Build();
    }
}