using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Data;
using Microsoft.Win32;
using ReactiveUI;
using SatelliteCore.Hosting;
using SatelliteCore.Instances;
using SatelliteCore.Paths;
using SatelliteUI.Configuration;
using SatelliteUI.Tools;
using SatelliteUI.Views;
using Location = Data.Location;

namespace SatelliteUI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private const string StartupRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupRegistryValueName = "SatelliteUI";

    private readonly MainWindow _mainWindow;
    private bool _isInstanceOperationInProgress;

    private readonly ISatelliteCoreHost _host;

    public MainWindowViewModel(MainWindow mainWindow)
    {
        _mainWindow = mainWindow;
        _host = InitHost();
        UIData = _host.AppData.GetOrCreateSection<SatelliteUIData>();
        SetDefaultCurrentInstance();
        _mainWindow.Loaded += (_, _) =>
        {
            _mainWindow.AddInstanceButton.Flyout!.Closed += (_, _) =>
                NewInstancePortText = string.Empty;
        };
    }

    public SatelliteUIData UIData { get; set => this.RaiseAndSetIfChanged(ref field, value); }

    public ObservableCollection<Instance> Instances => _host.AppData.Document.Instances;

    public Instance? CurrentInstance { get; set => this.RaiseAndSetIfChanged(ref field, value); }

    public string NewInstancePortText { get; set => this.RaiseAndSetIfChanged(ref field, value); } = string.Empty;
    public string NewLocationName { get; set => this.RaiseAndSetIfChanged(ref field, value); } = string.Empty;
    public string NewLocationPath { get; set => this.RaiseAndSetIfChanged(ref field, value); } = string.Empty;
    public string WhiteListText { get; set => this.RaiseAndSetIfChanged(ref field, value); } = string.Empty;

    [RelayCommand] public void MinimizeWindow() => Dispatcher.UIThread.Post(() => _mainWindow.WindowState = WindowState.Minimized);
    [RelayCommand] public void CloseWindow() => Dispatcher.UIThread.Post(_mainWindow.Close);
    [RelayCommand] public void ChangeBackground() => Dispatcher.UIThread.Post(_mainWindow.LoadBackground);

    [RelayCommand]
    public void ChangeCurrentInstance(int port)
        => CurrentInstance = Instances.SingleOrDefault(i => i.Port == port);

    [RelayCommand]
    public void OpenDirectory(string path)
    {
        var proc = new ProcessStartInfo { FileName = path, UseShellExecute = true };
        try { Process.Start(proc); }
        catch (Exception e) { SendNotification(e.Message); }
    }

    [RelayCommand]
    public void OpenAppDataFile()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            SendNotification("Opening appdata.json is only supported on Windows.");
            return;
        }
        var filePath = _host.Paths.DataFilePath;
        var proc = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c start \"\" \"{filePath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        try { Process.Start(proc); }
        catch (Exception e) { SendNotification(e.Message); }
    }

    [RelayCommand]
    public void ToggleMinimizeToTray()
    {
        UIData.MinimizeToTray = !UIData.MinimizeToTray;
        SaveUIData();
    }

    [RelayCommand]
    public void ToggleAutoStart()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            SendNotification("Auto start is only supported on Windows.");
            return;
        }
        var targetState = !UIData.IsAutoStart;
        try
        {
            SetWindowsAutoStart(targetState);
            UIData.IsAutoStart = targetState;
            SaveUIData();
        }
        catch (Exception e)
        {
            SendNotification($"Failed to {(targetState ? "enable" : "disable")} auto start: {e.Message}");
        }
    }

    [SupportedOSPlatform("windows")]
    private static void SetWindowsAutoStart(bool enabled)
    {
        using var runKey = Registry.CurrentUser.OpenSubKey(StartupRegistryPath, writable: true)
            ?? throw new InvalidOperationException("Cannot open startup registry key.");
        if (enabled)
            runKey.SetValue(StartupRegistryValueName, BuildStartupCommand());
        else
            runKey.DeleteValue(StartupRegistryValueName, throwOnMissingValue: false);
    }

    private static string BuildStartupCommand()
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
            throw new InvalidOperationException("Cannot resolve process path.");
        return string.Equals(Path.GetExtension(processPath), ".dll", StringComparison.OrdinalIgnoreCase)
            ? $"\"dotnet\" \"{processPath}\""
            : $"\"{processPath}\"";
    }

    [RelayCommand]
    public void RemoveLocation(Location location)
    {
        var result = _host.RemoveLocation(CurrentInstance!.Port, location);
        if (!result.Success) SendNotification(result.Message);
    }

    [RelayCommand]
    public async Task RemoveCurrentInstance(Button btn)
    {
        if (CurrentInstance is null)
        {
            btn.Flyout!.Hide();
            return;
        }

        if (CurrentInstance.IsRunning)
        {
            using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                await _host.StopInstanceAsync(CurrentInstance.Port, stopTimeout.Token);
            }
            catch (OperationCanceledException)
            {
                SendNotification("Stop timed out after 10 seconds; remove aborted.");
                btn.Flyout!.Hide();
                return;
            }
            catch (Exception ex)
            {
                SendNotification($"Stop failed: {ex.Message}; remove aborted.");
                btn.Flyout!.Hide();
                return;
            }
        }

        var result = _host.RemoveInstance(CurrentInstance.Port);
        if (!result.Success)
        {
            SendNotification(result.Message);
            btn.Flyout!.Hide();
            return;
        }

        SetDefaultCurrentInstance();
        btn.Flyout!.Hide();
    }

    [RelayCommand]
    public void AddInstance()
    {
        int port;
        try { port = Convert.ToInt32(NewInstancePortText); }
        catch { SendNotification("The port is not a number."); return; }

        if (port is < 0 or > 65535)
        {
            SendNotification("The port must be within the range of 0 to 65535.");
            return;
        }
        var instance = new Instance(port, false, []);
        var result = _host.AddInstance(instance);
        if (!result.Success) { SendNotification(result.Message); return; }
        _mainWindow.AddInstanceButton.Flyout!.Hide();
        CurrentInstance ??= instance;
    }

    [GeneratedRegex(@"^[a-zA-Z0-9]+$")]
    private static partial Regex PasswordRegex();

    [RelayCommand]
    public void InitWhiteListText()
        => WhiteListText = CurrentInstance is null
            ? string.Empty
            : string.Join(';' + Environment.NewLine, CurrentInstance.WhiteList);

    [RelayCommand]
    public void SaveWhiteList(Button btn)
    {
        if (CurrentInstance is null) return;
        var ipTexts = WhiteListText.Replace("\r", "").Replace("\n", "")
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => !string.IsNullOrEmpty(t))
            .ToList();
        var duplicates = ipTexts.GroupBy(t => t).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicates.Count > 0) { SendNotification($"Duplicate IPs: {string.Join(", ", duplicates)}"); return; }
        var invalidIps = ipTexts.Where(t => !IPAddress.TryParse(t, out _)).ToList();
        if (invalidIps.Count > 0) { SendNotification($"Invalid IPs: {string.Join(", ", invalidIps)}"); return; }
        CurrentInstance.WhiteList = new ObservableCollection<string>(ipTexts);
        _ = _host.AppData.SaveAsync();
        btn.Flyout!.Hide();
    }

    [RelayCommand]
    public void ChangeLocked()
    {
        if (!PasswordRegex().IsMatch(CurrentInstance!.Password))
        {
            SendNotification("Password is required and must be alphanumeric.");
            return;
        }
        CurrentInstance!.IsLocked = !CurrentInstance.IsLocked;
        _ = _host.AppData.SaveAsync();
    }

    [RelayCommand] public void SaveAppData() => SaveUIData();

    [RelayCommand]
    public void ChangePanelBlur()
    {
        var blurArgs = MainWindow.CreateBlurArgs(UIData.PanelBlur);
        _mainWindow.SetPanelControlsBgImage(blurArgs);
    }

    [RelayCommand]
    public void StartOrStopInstance()
    {
        if (CurrentInstance is null) return;
        if (_isInstanceOperationInProgress) { SendNotification("Instance operation is already in progress."); return; }

        var target = CurrentInstance;
        _isInstanceOperationInProgress = true;

        _ = Task.Run(async () =>
        {
            OperationResult result;
            try
            {
                if (target.IsRunning)
                    await _host.StopInstanceAsync(target.Port);
                else
                    await _host.StartInstanceAsync(target.Port);
                result = OperationResult.Ok;
            }
            catch (Exception ex) { result = OperationResult.Fail(ex.Message); }

            Dispatcher.UIThread.Post(() =>
            {
                _isInstanceOperationInProgress = false;
                this.RaisePropertyChanged(nameof(CurrentInstance));
                if (!result.Success) SendNotification(result.Message);
            });
        });
    }

    [RelayCommand]
    public void SelectLocation()
    {
        var folder = _mainWindow.StorageProvider
            .OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                AllowMultiple = false,
                SuggestedStartLocation = _mainWindow.StorageProvider
                    .TryGetWellKnownFolderAsync(WellKnownFolder.Desktop).Result,
            })
            .Result.FirstOrDefault();
        if (folder is null) return;
        NewLocationPath = folder.Path.LocalPath;
    }

    [RelayCommand]
    public void AddLocation()
    {
        var result = _host.AddLocation(CurrentInstance!.Port, new Location(NewLocationName, NewLocationPath));
        if (result.Success) { NewLocationName = string.Empty; NewLocationPath = string.Empty; }
        else SendNotification(result.Message);
    }

    public void SetDefaultCurrentInstance()
        => CurrentInstance = Instances.Count > 0 ? Instances[0] : null;

    private ISatelliteCoreHost InitHost()
    {
        try
        {
            var host = new SatelliteCoreHost(SatelliteRuntimeRoot.GetRootDirectory());
            _ = host.InitializeAsync();
            return host;
        }
        catch
        {
            SendNotification("Can't load file 'appdata.json'");
            throw;
        }
    }

    private void SaveUIData()
    {
        _ = _host.AppData.SetAndSaveSection(UIData);
    }

    private void SendNotification(string text)
    {
        if (WindowsNotificationTool.TryShow("SatelliteUI", text)) return;
        _mainWindow.NotificationManager?.Show(text, NotificationType.Information);
    }
}
