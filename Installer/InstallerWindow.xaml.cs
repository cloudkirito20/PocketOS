using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace LazyTech.PocketOS.Installer;

public partial class InstallerWindow : Window
{
    private readonly InstallerEngine _engine = new();
    private readonly DispatcherTimer _loadingTimer = new() { Interval = TimeSpan.FromMilliseconds(140) };
    private int _loadingFrame;

    private static readonly string[] LoadingFrames =
    {
        "[#         ] INSTALLING",
        "[##        ] INSTALLING",
        "[###       ] INSTALLING",
        "[####      ] INSTALLING",
        "[#####     ] INSTALLING",
        "[######    ] INSTALLING",
        "[#######   ] INSTALLING",
        "[########  ] INSTALLING",
        "[######### ] INSTALLING",
        "[##########] INSTALLING"
    };

    public InstallerWindow()
    {
        InitializeComponent();

        InstallPathTextBox.Text = _engine.InstallPath;
        AppendLog("Retro installer ready.");
        AppendLog("Install folder: " + _engine.InstallPath);

        _loadingTimer.Tick += (_, _) =>
        {
            LoadingTextBlock.Text = LoadingFrames[_loadingFrame];
            _loadingFrame = (_loadingFrame + 1) % LoadingFrames.Length;
        };
    }

    private async void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        InstallButton.IsEnabled = false;
        CloseButton.IsEnabled = false;
        InstallProgressBar.Visibility = Visibility.Visible;
        InstallProgressBar.IsIndeterminate = true;
        LoadingTextBlock.Visibility = Visibility.Visible;
        StatusTextBlock.Text = "CONNECTING TO RELEASE SERVER...";
        _loadingFrame = 0;
        _loadingTimer.Start();

        var progress = new Progress<string>(message =>
        {
            StatusTextBlock.Text = message;
            AppendLog(message);
        });

        try
        {
            await _engine.InstallAsync(progress);

            _loadingTimer.Stop();
            LoadingTextBlock.Visibility = Visibility.Collapsed;
            InstallProgressBar.IsIndeterminate = false;
            InstallProgressBar.Value = 100;
            StatusTextBlock.Text = "INSTALL COMPLETE. POCKETOS IS READY.";
            AppendLog("Installation completed successfully.");

            MessageBox.Show(
                "PocketOS installation completed.",
                "LazyTech PocketOS",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            var managerPath = _engine.GetManagerPath();
            if (File.Exists(managerPath))
            {
                AppendLog("Launching PocketOS Manager...");
                Process.Start(new ProcessStartInfo
                {
                    FileName = managerPath,
                    UseShellExecute = true
                });
            }
            else
            {
                AppendLog("PocketOS Manager was not found after install: " + managerPath);
            }
        }
        catch (Exception ex)
        {
            _loadingTimer.Stop();
            LoadingTextBlock.Visibility = Visibility.Collapsed;
            InstallProgressBar.IsIndeterminate = false;
            InstallProgressBar.Value = 0;
            StatusTextBlock.Text = "INSTALL FAILED.";
            AppendLog("ERROR: " + ex.Message);

            MessageBox.Show(
                ex.ToString(),
                "PocketOS Installer Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            InstallButton.IsEnabled = true;
            CloseButton.IsEnabled = true;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void AppendLog(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";
        LogTextBox.AppendText(line);
        LogTextBox.ScrollToEnd();
    }

    protected override void OnClosed(EventArgs e)
    {
        _loadingTimer.Stop();
        base.OnClosed(e);
    }
}
