using System;
using System.Windows.Forms;

namespace BetterTaskBar;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => FatalError(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => FatalError(e.ExceptionObject as Exception);

        var form = new TrayForm();
        _ = form.Handle;
        Application.Run(form);
    }

    private static void FatalError(Exception? ex)
    {
        TaskbarController.ShowAll();
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BetterTaskBar");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "error.log"), ex?.ToString() ?? "unknown error");
        }
        catch { }
        MessageBox.Show("Wystąpił błąd. Szczegóły: %APPDATA%\\BetterTaskBar\\error.log",
            "BetterTaskBar", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
