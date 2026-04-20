using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Data;
using Microsoft.Win32;
using ReactiveUI;
using Satellite.Service;
using Satellite.Tools;
using Satellite.Views;
using Location = Data.Location;

namespace Satellite.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private const string StartupRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupRegistryValueName = "Satellite";

    private readonly MainWindow _mainWindow;
    private bool _isInstanceOperationInProgress;

    public MainWindowViewModel(MainWindow mainWindow)
    {
        _mainWindow = mainWindow;

        ServiceController = InitServiceController();
        AppData = ServiceController.AppData;

        SetDefaultCurrentInstance();

        _mainWindow.Loaded += (_, _) =>
        {
            _mainWindow.AddInstanceButton.Flyout!.Closed += (_, _) =>
                NewInstancePortText = string.Empty;
        };
    }

    private ServiceController ServiceController
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public AppData AppData
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public Instance? CurrentInstance
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string NewInstancePortText
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public string NewLocationName
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public string NewLocationPath
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public string WhiteListText
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    [RelayCommand]
    public void MinimizeWindow()
    {
        Dispatcher.UIThread.Post(() =>
        {
            _mainWindow.WindowState = WindowState.Minimized;
        });
    }

    [RelayCommand]
    public void CloseWindow()
    {
        Dispatcher.UIThread.Post(_mainWindow.Close);
    }

    [RelayCommand]
    public void ChangeBackground()
    {
        Dispatcher.UIThread.Post(_mainWindow.LoadBackground);
    }

    [RelayCommand]
    public void ChangeCurrentInstance(int port)
    {
        CurrentInstance = AppData.Instances.SingleOrDefault(i => i.Port == port);
    }

    [RelayCommand]
    public void OpenDirectory(string path)
    {
        var proc = new ProcessStartInfo { FileName = path, UseShellExecute = true };
        try
        {
            Process.Start(proc);
        }
        catch (Exception e)
        {
            SendNotification(e.Message);
        }
    }

    [RelayCommand]
    public void OpenAppDataFile()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            SendNotification("Opening appdata.json is only supported on Windows.");
            return;
        }

        var filePath = Path.GetFullPath("appdata.json");
        var proc = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c start \"\" \"{filePath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        try
        {
            Process.Start(proc);
        }
        catch (Exception e)
        {
            SendNotification(e.Message);
        }
    }

    [RelayCommand]
    public void ToggleMinimizeToTray()
    {
        AppData.MinimizeToTray = !AppData.MinimizeToTray;
        ServiceController.SaveChange();
    }

    [RelayCommand]
    public void ToggleAutoStart()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            SendNotification("Auto start is only supported on Windows.");
            return;
        }

        var targetState = !AppData.IsAutoStart;
        try
        {
            SetWindowsAutoStart(targetState);
            AppData.IsAutoStart = targetState;
            ServiceController.SaveChange();
        }
        catch (Exception e)
        {
            var action = targetState ? "enable" : "disable";
            SendNotification($"Failed to {action} auto start: {e.Message}");
        }
    }

    [SupportedOSPlatform("windows")]
    private static void SetWindowsAutoStart(bool enabled)
    {
        using var runKey = Registry.CurrentUser.OpenSubKey(StartupRegistryPath, writable: true);
        if (runKey is null)
        {
            throw new InvalidOperationException("Cannot open startup registry key.");
        }

        if (enabled)
        {
            runKey.SetValue(StartupRegistryValueName, BuildStartupCommand());
            return;
        }

        runKey.DeleteValue(StartupRegistryValueName, throwOnMissingValue: false);
    }

    private static string BuildStartupCommand()
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            throw new InvalidOperationException("Cannot resolve process path.");
        }

        if (
            string.Equals(
                Path.GetExtension(processPath),
                ".dll",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return $"\"dotnet\" \"{processPath}\"";
        }

        return $"\"{processPath}\"";
    }

    [RelayCommand]
    public void RemoveLocation(Location location)
    {
        ServiceController.RemoveLocation(CurrentInstance!.Port, location);
    }

    [RelayCommand]
    public void RemoveCurrentInstance(Button btn)
    {
        if (CurrentInstance!.IsRunning)
        {
            SendNotification("The instance is running, please close it first.");
        }
        else
        {
            ServiceController.RemoveInstance(CurrentInstance);
            SetDefaultCurrentInstance();
        }

        btn.Flyout!.Hide();
    }

    [RelayCommand]
    public void AddInstance()
    {
        int port;
        try
        {
            port = Convert.ToInt32(NewInstancePortText);
        }
        catch
        {
            SendNotification("The port is not a number.");
            return;
        }

        if (port is < 0 or > 65535)
        {
            SendNotification("The port must be within the range of 0 to 65535.");
            return;
        }

        var instance = new Instance(port, false, []);
        var result = ServiceController.AddInstance(instance);
        if (!result.Success)
        {
            SendNotification(result.Message);
            return;
        }

        _mainWindow.AddInstanceButton.Flyout!.Hide();
        CurrentInstance ??= instance;
    }

    [GeneratedRegex(@"^[a-zA-Z0-9]+$")]
    private static partial Regex PasswordRegex();

    [RelayCommand]
    public void InitWhiteListText()
    {
        WhiteListText = CurrentInstance is null
            ? string.Empty
            : string.Join(';' + Environment.NewLine, CurrentInstance.WhiteList);
    }

    [RelayCommand]
    public void SaveWhiteList(Button btn)
    {
        if (CurrentInstance is null)
            return;

        var ipTexts = WhiteListText.Replace("\r", "").Replace("\n", "")
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(ipText => ipText.Trim())
            .Where(ipText => !string.IsNullOrEmpty(ipText))
            .ToList();

        var duplicates = ipTexts
            .GroupBy(ipText => ipText)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicates.Count > 0)
        {
            SendNotification($"Duplicate IPs: {string.Join(", ", duplicates)}");
            return;
        }
        
        var invalidIps = ipTexts.Where(ipText => !IPAddress.TryParse(ipText, out _)).ToList();
        if (invalidIps.Count > 0)
        {
            SendNotification($"Invalid IPs: {string.Join(", ", invalidIps)}");
            return;
        }

        CurrentInstance.WhiteList = new ObservableCollection<string>(ipTexts);
        ServiceController.SaveChange();
        btn.Flyout!.Hide();
    }

    [RelayCommand]
    public void ChangeLocked()
    {
        bool isPasswordValid = PasswordRegex().IsMatch(CurrentInstance!.Password);
        if (!isPasswordValid)
        {
            SendNotification("Password is required and must be alphanumeric.");
            return;
        }

        CurrentInstance!.IsLocked = !CurrentInstance.IsLocked;
        ServiceController.SaveChange();
    }

    [RelayCommand]
    public void SaveAppData()
    {
        ServiceController.SaveChange();
    }

    [RelayCommand]
    public void ChangePanelBlur()
    {
        var blurArgs = MainWindow.CreateBlurArgs(AppData.PanelBlur);
        _mainWindow.SetPanelControlsBgImage(blurArgs);
    }

    [RelayCommand]
    public void StartOrStopInstance()
    {
        if (CurrentInstance is null)
        {
            return;
        }

        if (_isInstanceOperationInProgress)
        {
            SendNotification("Instance operation is already in progress.");
            return;
        }

        var target = CurrentInstance;
        _isInstanceOperationInProgress = true;

        void OnCompleted(OperationResult result)
        {
            Dispatcher.UIThread.Post(() =>
            {
                _isInstanceOperationInProgress = false;
                this.RaisePropertyChanged(nameof(CurrentInstance));
                if (!result.Success)
                {
                    SendNotification(result.Message);
                }
            });
        }

        Action<Instance, Action<OperationResult>?> instanceOperation = target.IsRunning
            ? ServiceController.StopInstance
            : ServiceController.StartInstance;
        instanceOperation(target, OnCompleted);
    }

    [RelayCommand]
    public void SelectLocation()
    {
        var folder = _mainWindow
            .StorageProvider.OpenFolderPickerAsync(
                new FolderPickerOpenOptions
                {
                    AllowMultiple = false,
                    SuggestedStartLocation = _mainWindow
                        .StorageProvider.TryGetWellKnownFolderAsync(WellKnownFolder.Desktop)
                        .Result,
                }
            )
            .Result.FirstOrDefault();
        if (folder is null)
            return;

        NewLocationPath = folder.Path.LocalPath;
    }

    [RelayCommand]
    public void AddLocation()
    {
        var result = ServiceController.AddLocation(
            CurrentInstance!.Port,
            new Location(NewLocationName, NewLocationPath)
        );
        if (result.Success)
        {
            NewLocationName = string.Empty;
            NewLocationPath = string.Empty;
        }
        else
        {
            SendNotification(result.Message);
        }
    }

    public void SetDefaultCurrentInstance()
    {
        CurrentInstance = AppData.Instances.Count > 0 ? AppData.Instances[0] : null;
    }

    public ServiceController InitServiceController()
    {
        try
        {
            return new ServiceController();
        }
        catch
        {
            SendNotification("Can't load file 'appdata.json'");
            throw;
        }
    }

    private void SendNotification(string text)
    {
        if (WindowsNotificationTool.TryShow("Satellite", text))
        {
            return;
        }

        var notificationManager = _mainWindow.NotificationManager;
        if (notificationManager is not null)
        {
            notificationManager.Show(text, NotificationType.Information);
            return;
        }
    }
}
