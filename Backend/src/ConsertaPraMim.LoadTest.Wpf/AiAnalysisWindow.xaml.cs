using System.Windows;
using ConsertaPraMim.LoadTest.Wpf.Services;

namespace ConsertaPraMim.LoadTest.Wpf;

public partial class AiAnalysisWindow : Window
{
    public AiAnalysisWindow(LoadTestAiAnalysis analysis)
    {
        InitializeComponent();

        SummaryTextBox.Text = analysis.Summary;
        MetaTextBlock.Text = $"Provider: {analysis.Provider} | Model: {analysis.Model} | Gerado em: {ToLocalDateTime(analysis.GeneratedAtUtc)}";
    }

    private void CopyButton_OnClick(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(SummaryTextBox.Text ?? string.Empty);
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private static string ToLocalDateTime(string utcIsoValue)
    {
        if (DateTimeOffset.TryParse(utcIsoValue, out var parsed))
        {
            return parsed.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss");
        }

        return utcIsoValue;
    }
}
