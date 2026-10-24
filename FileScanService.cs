using System.Text;

namespace LocalGuardJunior;

internal sealed record ScanSummary(int CheckedFiles, int Replacements, string ReportPath);
internal sealed record ScanMatch(string FilePath, int Count);

// Читаем файлы в выбранной папке и сохраняем исправленные КОПИИ в Документы.
internal static class FileScanService
{
    private static readonly string[] SupportedExtensions = [".txt", ".log", ".md", ".csv"];

    public static async Task<ScanSummary> ScanAsync(
        string folder, List<string> words, IProgress<int> progress, CancellationToken token)
    {
        // Заходим в подпапки, но не следуем по символическим ссылкам.
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        string[] files = Directory.EnumerateFiles(folder, "*", options)
            .Where(path => SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .ToArray();

        string outputFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "LocalGuardResults", DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(outputFolder);

        int processed = 0; // Общий счётчик, который будут менять до пяти работников.
        var found = new List<ScanMatch>();
        var errors = new List<string>();
        object listLock = new(); // Объект блокировки защищает общие списки.

        // Одновременно выполняется не больше пяти задач.
        await Parallel.ForEachAsync(files, new ParallelOptions
        {
            MaxDegreeOfParallelism = 5,
            CancellationToken = token
        }, async (file, ct) =>
        {
            try
            {
                // Для учебного примера не читаем слишком большие файлы в память.
                if (new FileInfo(file).Length > 2_000_000)
                    return;

                string original = await File.ReadAllTextAsync(file, ct);
                (string safeText, int matches) = TextFilter.ReplaceWords(original, words);

                if (matches > 0)
                {
                    // Оригиналы не трогаем: сохраняем копию, сохраняя структуру подпапок.
                    string relativePath = Path.GetRelativePath(folder, file);
                    string destination = Path.Combine(outputFolder, relativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    await File.WriteAllTextAsync(destination, safeText, ct);

                    lock (listLock)
                    {
                        found.Add(new ScanMatch(file, matches));
                    }
                }
            }
            catch (IOException ex)
            {
                lock (listLock) errors.Add(file + ": " + ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                lock (listLock) errors.Add(file + ": " + ex.Message);
            }
            finally
            {
                // Атомарно увеличиваем счётчик: прибавление не потеряется из-за гонки потоков.
                int completed = Interlocked.Increment(ref processed);
                int percent = files.Length == 0 ? 100 : completed * 100 / files.Length;
                progress.Report(percent);
            }
        });

        token.ThrowIfCancellationRequested();
        progress.Report(100);

        // Формируем отчёт только после завершения всех задач.
        int total = found.Sum(item => item.Count);
        string reportPath = Path.Combine(outputFolder, "report.txt");
        var report = new StringBuilder();
        report.AppendLine("LocalGuardJunior - file scan report");
        report.AppendLine("Source folder: " + folder);
        report.AppendLine("Checked files: " + files.Length);
        report.AppendLine("Word replacements: " + total);
        report.AppendLine("Original files were not modified.");
        report.AppendLine();

        foreach (ScanMatch item in found.OrderBy(item => item.FilePath))
            report.AppendLine($"{item.FilePath} | matches: {item.Count}");

        if (errors.Count > 0)
        {
            report.AppendLine();
            report.AppendLine("Files that could not be read:");
            foreach (string error in errors)
                report.AppendLine(error);
        }

        await File.WriteAllTextAsync(reportPath, report.ToString(), token);
        return new ScanSummary(files.Length, total, reportPath);
    }
}
