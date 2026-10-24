namespace LocalGuardJunior;

internal static class Program
{
    [STAThread] // Windows Forms requires a single-threaded apartment for its UI thread.
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
