using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using ConsertaPraMim.LoadTest.Wpf.Models;
using ConsertaPraMim.LoadTest.Wpf.Services;
using Microsoft.Win32;

namespace ConsertaPraMim.LoadTest.Wpf;

public partial class MainWindow : Window
{
    private const string DefaultBaseUrl = "http://187.77.48.150:5193";
    private const string DefaultPublishEmail = "admin@teste.com";
    private const string DefaultPublishPassword = "SeedDev!2026";
    private const string DefaultOpenAiModel = "gpt-4.1-mini";

    private readonly ObservableCollection<string> _logs = [];

    private LoadTestConfig? _config;
    private CancellationTokenSource? _runCts;
    private bool _isRunning;
    private string _lastOutputDirectory = string.Empty;

    public MainWindow()
    {
        InitializeComponent();
        LogListBox.ItemsSource = _logs;
        Loaded += MainWindow_OnLoaded;
    }

    private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        var defaultConfig = FindDefaultConfigPath();
        ConfigPathTextBox.Text = defaultConfig;
        BaseUrlTextBox.Text = DefaultBaseUrl;
        TimeoutTextBox.Text = "20";
        SeedTextBox.Text = "42";
        PublishEmailTextBox.Text = DefaultPublishEmail;
        PublishPasswordBox.Password = DefaultPublishPassword;
        OpenAiApiKeyBox.Password = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? string.Empty;
        TryLoadConfig(showError: true);
    }

    private static string FindDefaultConfigPath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "scripts", "loadtest", "loadtest.config.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return Path.Combine(AppContext.BaseDirectory, "loadtest.config.json");
    }

    private void BrowseConfigButton_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "JSON|*.json|Todos|*.*",
            FileName = Path.GetFileName(ConfigPathTextBox.Text)
        };

        if (dialog.ShowDialog(this) == true)
        {
            ConfigPathTextBox.Text = dialog.FileName;
            TryLoadConfig(showError: true);
        }
    }

    private void ReloadConfigButton_OnClick(object sender, RoutedEventArgs e)
    {
        TryLoadConfig(showError: true);
    }

    private void ScenarioComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_config is null || ScenarioComboBox.SelectedItem is not string scenarioName)
        {
            return;
        }

        if (!_config.Scenarios.TryGetValue(scenarioName, out var scenario))
        {
            return;
        }

        VusTextBox.Text = scenario.Vus.ToString();
        DurationTextBox.Text = scenario.DurationSeconds.ToString();
        RampUpTextBox.Text = scenario.RampUpSeconds.ToString("0.##");
        ThinkMinTextBox.Text = scenario.ThinkTimeMinMs.ToString();
        ThinkMaxTextBox.Text = scenario.ThinkTimeMaxMs.ToString();
        ErrorRateTextBox.Text = scenario.ErrorInjectionRatePercent.ToString("0.##");
    }

    private async void StartButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_isRunning)
        {
            return;
        }

        if (!TryBuildRunOptions(out var options))
        {
            return;
        }

        var engine = new LoadTestEngine(AppendLog);
        _runCts = new CancellationTokenSource();
        SetRunningState(true);
        AppendLog("Execucao iniciada.");

        try
        {
            var progress = new Progress<LoadTestLiveSnapshot>(ApplySnapshot);
            var result = await engine.RunAsync(options, progress, _runCts.Token);

            AppendLog("Execucao finalizada com sucesso.");
            AppendLog($"JSON: {result.JsonPath}");
            AppendLog($"HTML: {result.HtmlPath}");
            AppendLog(result.PublishResult.Succeeded
                ? $"Publicacao no admin: OK ({result.PublishResult.Message})"
                : $"Publicacao no admin: FALHOU ({result.PublishResult.Message})");

            if (result.Report.AiAnalysis is { Summary.Length: > 0 } aiAnalysis)
            {
                AppendLog("Exibindo janela de analise IA.");
                ShowAiAnalysisDialog(aiAnalysis);
            }
            else
            {
                AppendLog("Analise IA nao disponivel para exibicao.");
            }

            _lastOutputDirectory = Path.GetDirectoryName(result.JsonPath) ?? _lastOutputDirectory;
        }
        catch (OperationCanceledException)
        {
            AppendLog("Execucao cancelada.");
            StatusTextBlock.Text = "Status: Canceled";
        }
        catch (Exception ex)
        {
            AppendLog($"Erro: {ex.GetType().Name}: {ex.Message}");
            StatusTextBlock.Text = "Status: Error";
            MessageBox.Show(this, ex.Message, "Load test", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetRunningState(false);
            _runCts?.Dispose();
            _runCts = null;
        }
    }

    private void StopButton_OnClick(object sender, RoutedEventArgs e)
    {
        _runCts?.Cancel();
    }

    private void OpenOutputButton_OnClick(object sender, RoutedEventArgs e)
    {
        var output = _lastOutputDirectory;
        if (string.IsNullOrWhiteSpace(output))
        {
            var configPath = ConfigPathTextBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(configPath))
            {
                output = Path.Combine(Path.GetDirectoryName(configPath) ?? AppContext.BaseDirectory, "output");
            }
        }

        if (string.IsNullOrWhiteSpace(output))
        {
            output = AppContext.BaseDirectory;
        }

        Directory.CreateDirectory(output);
        Process.Start(new ProcessStartInfo
        {
            FileName = output,
            UseShellExecute = true
        });
    }

    private bool TryLoadConfig(bool showError)
    {
        try
        {
            var path = ConfigPathTextBox.Text.Trim();
            _config = LoadTestConfig.LoadFromFile(path);

            BaseUrlTextBox.Text = string.IsNullOrWhiteSpace(_config.BaseUrl)
                ? DefaultBaseUrl
                : _config.BaseUrl;
            ScenarioComboBox.ItemsSource = _config.Scenarios.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
            ScenarioComboBox.SelectedIndex = 0;
            PublishEmailTextBox.Text = string.IsNullOrWhiteSpace(_config.AdminPublish.Email)
                ? DefaultPublishEmail
                : _config.AdminPublish.Email;
            PublishPasswordBox.Password = string.IsNullOrWhiteSpace(_config.AdminPublish.Password)
                ? DefaultPublishPassword
                : _config.AdminPublish.Password;

            _lastOutputDirectory = Path.Combine(Path.GetDirectoryName(path) ?? AppContext.BaseDirectory, "output");
            AppendLog($"Config carregada: {path}");
            return true;
        }
        catch (Exception ex)
        {
            _config = null;
            ScenarioComboBox.ItemsSource = null;
            if (showError)
            {
                MessageBox.Show(this, ex.Message, "Erro ao carregar config", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            return false;
        }
    }

    private bool TryBuildRunOptions(out LoadTestRunOptions options)
    {
        options = null!;

        if (_config is null && !TryLoadConfig(showError: true))
        {
            return false;
        }

        if (_config is null)
        {
            return false;
        }

        if (ScenarioComboBox.SelectedItem is not string scenarioName || string.IsNullOrWhiteSpace(scenarioName))
        {
            MessageBox.Show(this, "Selecione um scenario.", "Load test", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (!_config.Scenarios.TryGetValue(scenarioName, out var scenario))
        {
            MessageBox.Show(this, "Scenario invalido.", "Load test", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (!TryParseInt(VusTextBox.Text, scenario.Vus, out var vus) || vus <= 0 ||
            !TryParseInt(DurationTextBox.Text, scenario.DurationSeconds, out var duration) || duration <= 0 ||
            !TryParseDouble(RampUpTextBox.Text, scenario.RampUpSeconds, out var rampUp) || rampUp < 0 ||
            !TryParseInt(ThinkMinTextBox.Text, scenario.ThinkTimeMinMs, out var thinkMin) || thinkMin < 0 ||
            !TryParseInt(ThinkMaxTextBox.Text, scenario.ThinkTimeMaxMs, out var thinkMax) || thinkMax < thinkMin ||
            !TryParseDouble(ErrorRateTextBox.Text, scenario.ErrorInjectionRatePercent, out var errorRate) || errorRate < 0 ||
            !TryParseDouble(TimeoutTextBox.Text, 20, out var timeout) || timeout <= 0 ||
            !TryParseInt(SeedTextBox.Text, 42, out var seed) || seed <= 0)
        {
            MessageBox.Show(this, "Revise os parametros numericos.", "Load test", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        var baseUrl = BaseUrlTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl) ||
            !Uri.TryCreate(baseUrl, UriKind.Absolute, out var parsedBaseUrl) ||
            (parsedBaseUrl.Scheme != Uri.UriSchemeHttp && parsedBaseUrl.Scheme != Uri.UriSchemeHttps))
        {
            MessageBox.Show(this, "Base URL invalida.", "Load test", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        var configPath = ConfigPathTextBox.Text.Trim();
        var outputDirectory = Path.Combine(Path.GetDirectoryName(configPath) ?? AppContext.BaseDirectory, "output");
        var publishEmail = PublishEmailTextBox.Text.Trim();
        var publishPassword = PublishPasswordBox.Password ?? string.Empty;
        var openAiApiKey = OpenAiApiKeyBox.Password ?? string.Empty;

        options = new LoadTestRunOptions
        {
            ConfigPath = configPath,
            Config = _config,
            ScenarioName = scenarioName,
            Scenario = scenario,
            BaseUrl = parsedBaseUrl.ToString().TrimEnd('/'),
            OutputDirectory = outputDirectory,
            AdminPublish = new AdminPublishConfig
            {
                Enabled = true,
                ImportUrl = _config.AdminPublish.ImportUrl,
                Source = _config.AdminPublish.Source,
                BearerToken = _config.AdminPublish.BearerToken,
                LoginUrl = _config.AdminPublish.LoginUrl,
                TokenField = _config.AdminPublish.TokenField,
                Email = string.IsNullOrWhiteSpace(publishEmail) ? DefaultPublishEmail : publishEmail,
                Password = string.IsNullOrWhiteSpace(publishPassword) ? DefaultPublishPassword : publishPassword
            },
            OpenAiApiKey = openAiApiKey,
            OpenAiModel = DefaultOpenAiModel,
            Vus = vus,
            DurationSeconds = duration,
            RampUpSeconds = rampUp,
            ThinkMinMs = thinkMin,
            ThinkMaxMs = thinkMax,
            ErrorInjectionRatePercent = errorRate,
            TimeoutSeconds = timeout,
            Seed = seed,
            InsecureTls = InsecureTlsCheckBox.IsChecked == true,
            RefreshSeconds = 1
        };

        return true;
    }

    private void ApplySnapshot(LoadTestLiveSnapshot snapshot)
    {
        RunIdTextBlock.Text = $"RunId: {snapshot.RunId}";
        StatusTextBlock.Text = $"Status: {snapshot.Status}";
        ElapsedTextBlock.Text = $"Elapsed: {snapshot.ElapsedSeconds:0.##}s / {snapshot.PlannedDurationSeconds}s";
        RunProgressBar.Value = snapshot.ProgressPercent;

        TotalRequestsTextBlock.Text = snapshot.Summary.TotalRequests.ToString();
        RpsTextBlock.Text = $"{snapshot.Summary.RpsCurrent} / {snapshot.Summary.RpsAvg:0.##} / {snapshot.Summary.RpsPeak}";
        LatencyTextBlock.Text = $"{snapshot.LatencyMs.P95:0.##} / {snapshot.LatencyMs.P99:0.##} ms";
        FailuresTextBlock.Text = $"{snapshot.Summary.FailedRequests} ({snapshot.Summary.ErrorRatePercent:0.##}%)";

        StatusCodesDataGrid.ItemsSource = snapshot.StatusCodes;
        EndpointsDataGrid.ItemsSource = snapshot.TopEndpointsByHits;
    }

    private void SetRunningState(bool running)
    {
        _isRunning = running;
        StartButton.IsEnabled = !running;
        StopButton.IsEnabled = running;
        ReloadConfigButton.IsEnabled = !running;
        BrowseConfigButton.IsEnabled = !running;
        ScenarioComboBox.IsEnabled = !running;
        ConfigPathTextBox.IsEnabled = !running;
        PublishEmailTextBox.IsEnabled = !running;
        PublishPasswordBox.IsEnabled = !running;
        OpenAiApiKeyBox.IsEnabled = !running;
    }

    private void ShowAiAnalysisDialog(LoadTestAiAnalysis aiAnalysis)
    {
        var dialog = new AiAnalysisWindow(aiAnalysis)
        {
            Owner = this
        };

        dialog.ShowDialog();
    }

    private void AppendLog(string message)
    {
        Dispatcher.Invoke(() =>
        {
            _logs.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            while (_logs.Count > 500)
            {
                _logs.RemoveAt(0);
            }

            if (_logs.Count > 0)
            {
                LogListBox.ScrollIntoView(_logs[^1]);
            }
        });
    }

    private static bool TryParseInt(string raw, int fallback, out int value)
    {
        if (int.TryParse(raw, out value))
        {
            return true;
        }

        value = fallback;
        return false;
    }

    private static bool TryParseDouble(string raw, double fallback, out double value)
    {
        if (double.TryParse(raw, out value))
        {
            return true;
        }

        value = fallback;
        return false;
    }
}
