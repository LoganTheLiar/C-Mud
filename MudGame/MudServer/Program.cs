using System.Windows.Forms;

namespace MudServer;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        var server = new GameServer(port: 4000);
        var form = new ServerForm(server);
        _ = server.StartAsync();
        Application.Run(form);
        server.Stop();
    }
}
