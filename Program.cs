namespace LocalGuardJunior;

internal static class Program
{
    [STAThread] // Главный поток интерфейса WinForms должен работать в режиме STA.
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
