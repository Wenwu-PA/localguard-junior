using System.Diagnostics;

namespace LocalGuardJunior;

internal sealed class MainForm : Form
{
    private readonly TextBox folderBox = new();
    private readonly TextBox wordsBox = new();
    private readonly Button scanButton = new();
    private readonly Button cancelButton = new();
    private readonly ProgressBar scanProgress = new();
    private readonly Label scanStatus = new();
    private readonly TextBox scanLog = new();

    private readonly TextBox blockedAppsBox = new();
    private readonly Button startWatchButton = new();
    private readonly Button stopWatchButton = new();
    private readonly Label watchStatus = new();
    private readonly TextBox watchLog = new();

    private readonly TextBox chatHistory = new();
    private readonly TextBox chatInput = new();

    private CancellationTokenSource? scanCancellation;
    private CancellationTokenSource? watchCancellation;

    public MainForm()
    {
        Text = "LocalGuard Junior - родительский контроль (демо)";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(900, 660);
        MinimumSize = new Size(780, 580);
        Font = new Font("Segoe UI", 10);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(CreateFilesTab());
        tabs.TabPages.Add(CreateProcessesTab());
        tabs.TabPages.Add(CreateChatTab());
        Controls.Add(tabs);

        FormClosing += (_, _) =>
        {
            // При закрытии окна просим фоновые задачи остановиться.
            scanCancellation?.Cancel();
            watchCancellation?.Cancel();
        };
    }

    private TabPage CreateFilesTab()
    {
        var page = new TabPage("1. Проверка файлов");
        page.Controls.Add(Label("Папка для проверки", 20, 17, 300));
        folderBox.SetBounds(20, 49, 716, 30);
        folderBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        page.Controls.Add(folderBox);

        var browseButton = Button("Обзор...", 748, 47, 110);
        browseButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        browseButton.Click += (_, _) =>
        {
            using var dialog = new FolderBrowserDialog { Description = "Выберите папку для проверки" };
            if (dialog.ShowDialog(this) == DialogResult.OK)
                folderBox.Text = dialog.SelectedPath;
        };
        page.Controls.Add(browseButton);

        page.Controls.Add(Label("Слова для замены (через запятую)", 20, 93, 500));
        wordsBox.SetBounds(20, 124, 838, 30);
        wordsBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        wordsBox.Text = "дурак, грубость"; // Это примеры слов: пользователь может их изменить.
        page.Controls.Add(wordsBox);

        page.Controls.Add(Label(
            "Проверяются TXT, LOG, MD, CSV и подпапки. Исходники не меняются: файлы с заменами " +
            "и отчёт сохраняются в Документы\\LocalGuardResults.", 20, 168, 835, 45));

        scanButton.Text = "Начать проверку";
        scanButton.SetBounds(20, 225, 180, 34);
        scanButton.Click += ScanButton_Click;
        page.Controls.Add(scanButton);

        cancelButton.Text = "Остановить";
        cancelButton.SetBounds(212, 225, 135, 34);
        cancelButton.Enabled = false;
        cancelButton.Click += (_, _) => scanCancellation?.Cancel();
        page.Controls.Add(cancelButton);

        scanProgress.SetBounds(20, 282, 838, 24);
        scanProgress.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        page.Controls.Add(scanProgress);

        scanStatus.Text = "Ожидание";
        scanStatus.SetBounds(20, 316, 830, 26);
        page.Controls.Add(scanStatus);

        scanLog.Multiline = true;
        scanLog.ReadOnly = true;
        scanLog.ScrollBars = ScrollBars.Vertical;
        scanLog.SetBounds(20, 349, 838, 220);
        scanLog.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        page.Controls.Add(scanLog);
        return page;
    }

    private async void ScanButton_Click(object? sender, EventArgs e)
    {
        string folder = folderBox.Text.Trim();
        List<string> words = TextFilter.ParseWords(wordsBox.Text);

        if (!Directory.Exists(folder))
        {
            MessageBox.Show(this, "Выберите существующую папку.", "Проверка");
            return;
        }
        if (words.Count == 0)
        {
            MessageBox.Show(this, "Введите хотя бы одно слово.", "Проверка");
            return;
        }

        scanCancellation = new CancellationTokenSource();
        scanButton.Enabled = false;
        cancelButton.Enabled = true;
        scanProgress.Value = 0;
        scanStatus.Text = "Проверка...";
        scanLog.Clear();

        // Progress<int> безопасно обновляет интерфейс из фонового кода.
        var progress = new Progress<int>(percent =>
        {
            if (IsDisposed || scanCancellation is null) return;
            scanProgress.Value = Math.Clamp(percent, 0, 100);
            scanStatus.Text = $"Готово: {percent}%";
        });

        try
        {
            // await не блокирует окно во время длительной проверки файлов.
            ScanSummary result = await FileScanService.ScanAsync(
                folder, words, progress, scanCancellation.Token);
            scanStatus.Text = "Проверка завершена";
            scanProgress.Value = 100;
            scanLog.Text = $"Файлов: {result.CheckedFiles}\r\n" +
                           $"Замен: {result.Replacements}\r\n" +
                           $"Отчёт: {result.ReportPath}\r\n" +
                           "Исходные файлы не изменены.";
        }
        catch (OperationCanceledException)
        {
            scanStatus.Text = "Проверка остановлена";
            scanLog.Text = "Проверка отменена. Если уже были созданы копии, они могут остаться в папке результатов.";
        }
        catch (Exception ex)
        {
            scanStatus.Text = "Ошибка";
            scanLog.Text = ex.Message;
        }
        finally
        {
            scanCancellation?.Dispose();
            scanCancellation = null;
            if (!IsDisposed)
            {
                scanButton.Enabled = true;
                cancelButton.Enabled = false;
            }
        }
    }

    private TabPage CreateProcessesTab()
    {
        var page = new TabPage("2. Контроль программ");
        page.Controls.Add(Label(
            "Проверяет список процессов каждые 2 секунды и закрывает указанные программы. " +
            "Работает только при открытом приложении и включённом контроле.", 20, 18, 835, 58));
        page.Controls.Add(Label("Имена EXE через запятую (без .exe)", 20, 95, 640));

        blockedAppsBox.SetBounds(20, 128, 838, 30);
        blockedAppsBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        blockedAppsBox.PlaceholderText = "examplegame, testapp";
        page.Controls.Add(blockedAppsBox);

        startWatchButton.Text = "Включить контроль";
        startWatchButton.SetBounds(20, 178, 180, 35);
        startWatchButton.Click += StartWatchButton_Click;
        page.Controls.Add(startWatchButton);

        stopWatchButton.Text = "Выключить";
        stopWatchButton.SetBounds(212, 178, 140, 35);
        stopWatchButton.Enabled = false;
        stopWatchButton.Click += (_, _) => StopWatching();
        page.Controls.Add(stopWatchButton);

        watchStatus.Text = "Контроль выключен";
        watchStatus.SetBounds(20, 232, 820, 30);
        page.Controls.Add(watchStatus);

        watchLog.Multiline = true;
        watchLog.ReadOnly = true;
        watchLog.ScrollBars = ScrollBars.Vertical;
        watchLog.SetBounds(20, 275, 838, 280);
        watchLog.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        page.Controls.Add(watchLog);
        return page;
    }

    private void StartWatchButton_Click(object? sender, EventArgs e)
    {
        List<string> names = TextFilter.ParseWords(blockedAppsBox.Text)
            .Select(name => name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name)
            .Where(name => name.Length > 0 && !name.Contains(' ') && !name.Contains('\\') && !name.Contains('/'))
            .ToList();

        // Защищаем важные системные процессы от случайного завершения.
        string[] protectedNames =
        ["system", "idle", "csrss", "wininit", "winlogon", "lsass", "services", "svchost",
         "smss", "dwm", "explorer", "taskmgr", "powershell", "pwsh", "dotnet",
         Path.GetFileNameWithoutExtension(Application.ExecutablePath)];
        names.RemoveAll(name => protectedNames.Contains(name, StringComparer.OrdinalIgnoreCase));

        if (names.Count == 0)
        {
            MessageBox.Show(this, "Введите названия тестовых программ, например: notepad", "Контроль");
            return;
        }

        if (MessageBox.Show(this,
                "Контроль будет принудительно завершать выбранные процессы. " +
                "Несохранённые данные в них могут потеряться. Продолжить?",
                "Подтверждение", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        watchCancellation = new CancellationTokenSource();
        startWatchButton.Enabled = false;
        stopWatchButton.Enabled = true;
        watchStatus.Text = "Контроль включен";
        AppendWatchLog("Начали проверять: " + string.Join(", ", names));

        CancellationToken token = watchCancellation.Token;
        _ = Task.Run(async () =>
        {
            // Проверка процессов работает в фоне, поэтому окно не зависает.
            using Process current = Process.GetCurrentProcess();
            int currentSession = current.SessionId;
            while (!token.IsCancellationRequested)
            {
                foreach (string name in names)
                {
                    if (token.IsCancellationRequested) break;
                    try
                    {
                        foreach (Process process in Process.GetProcessesByName(name))
                        {
                            using (process)
                            {
                                // Не завершаем собственный процесс или программы других сеансов.
                                if (process.Id == Environment.ProcessId || process.SessionId != currentSession)
                                    continue;

                                if (token.IsCancellationRequested) break;
                                int id = process.Id;
                                process.Kill();
                                AppendWatchLog($"Закрыта программа: {name} (PID {id})");
                            }
                        }
                    }
                    catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
                    {
                        AppendWatchLog($"Не удалось закрыть {name}: {ex.Message}");
                    }
                }

                try { await Task.Delay(2000, token); }
                catch (OperationCanceledException) { break; }
            }
        }, token);
    }

    private void StopWatching()
    {
        watchCancellation?.Cancel();
        // Не освобождаем CancellationTokenSource, пока фоновая задача ещё выходит из цикла.
        watchCancellation = null;
        startWatchButton.Enabled = true;
        stopWatchButton.Enabled = false;
        watchStatus.Text = "Контроль выключен";
        AppendWatchLog("Контроль остановлен");
    }

    private void AppendWatchLog(string message)
    {
        if (IsDisposed || Disposing) return;

        // Контролы WinForms можно менять только из потока интерфейса.
        if (InvokeRequired)
        {
            try { BeginInvoke((Action)(() => AppendWatchLog(message))); }
            catch (InvalidOperationException) { /* The window is closing. */ }
            return;
        }
        watchLog.AppendText($"{DateTime.Now:HH:mm:ss} {message}\r\n");
    }

    private TabPage CreateChatTab()
    {
        var page = new TabPage("3. Чат (демо)");

        // Сначала поле ввода, ниже история сообщений: их сложно перепутать.
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(16)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.Controls.Add(layout);

        layout.Controls.Add(new Label
        {
            Text = "Учебный чат: сообщения фильтруются по списку слов с первой вкладки. " +
                   "Переписка из других программ не читается.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        layout.Controls.Add(new Label
        {
            Text = "ВВОД СООБЩЕНИЯ (пишите в поле ниже):",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 1);

        // Отдельная большая область ввода в ВЕРХНЕЙ части вкладки.
        var inputRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0)
        };
        inputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        inputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        inputRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(inputRow, 0, 2);

        chatInput.Multiline = true;
        chatInput.ReadOnly = false;
        chatInput.Enabled = true;
        chatInput.TabStop = true;
        chatInput.Dock = DockStyle.Fill;
        chatInput.BackColor = Color.FromArgb(255, 255, 235);
        chatInput.ForeColor = SystemColors.WindowText;
        chatInput.BorderStyle = BorderStyle.FixedSingle;
        chatInput.ScrollBars = ScrollBars.Vertical;
        chatInput.Font = new Font("Segoe UI", 11);
        chatInput.Margin = new Padding(0, 4, 10, 4);
        chatInput.PlaceholderText = "Напишите текст здесь...";
        // Enter отправляет сообщение. Shift+Enter вставляет новую строку.
        chatInput.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.SuppressKeyPress = true;
                SendChatMessage();
            }
        };
        inputRow.Controls.Add(chatInput, 0, 0);

        var sendButton = new Button
        {
            Text = "Отправить",
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 4)
        };
        sendButton.Click += (_, _) => SendChatMessage();
        inputRow.Controls.Add(sendButton, 1, 0);

        layout.Controls.Add(new Label
        {
            Text = "ИСТОРИЯ СООБЩЕНИЙ (только просмотр):",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 3);

        // Историю нельзя редактировать, она заполняется после отправки.
        chatHistory.Multiline = true;
        chatHistory.ReadOnly = true;
        chatHistory.ScrollBars = ScrollBars.Vertical;
        chatHistory.BackColor = SystemColors.Window;
        chatHistory.Dock = DockStyle.Fill;
        chatHistory.TabStop = false;
        layout.Controls.Add(chatHistory, 0, 4);

        page.Enter += (_, _) => chatInput.Focus();
        return page;
    }

    private void SendChatMessage()
    {
        if (string.IsNullOrWhiteSpace(chatInput.Text)) return;

        List<string> words = TextFilter.ParseWords(wordsBox.Text);
        (string safeText, int replacements) = TextFilter.ReplaceWords(chatInput.Text, words);
        chatHistory.AppendText($"Сообщение: {safeText} (замен: {replacements})\r\n");
        chatInput.Clear();
        chatInput.Focus();
    }

    private static Label Label(string text, int x, int y, int width, int height = 26) =>
        new() { Text = text, Left = x, Top = y, Width = width, Height = height };

    private static Button Button(string text, int x, int y, int width) =>
        new() { Text = text, Left = x, Top = y, Width = width, Height = 34 };
}
