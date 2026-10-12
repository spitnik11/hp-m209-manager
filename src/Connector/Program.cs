using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HP.Jarvis.Discovery.Ble;
using HP.Jarvis.Logger;
using HP.Jarvis.SysUtils;

internal static class Program
{
    private static readonly TaskCompletionSource<IHpBleDevice> Found =
        new TaskCompletionSource<IHpBleDevice>();

    private static int Main(string[] args)
    {
        try { return Run(args).GetAwaiter().GetResult(); }
        catch (Exception ex)
        {
            Console.Error.WriteLine("ERROR: " + ex.GetType().Name + ": " + ex.Message);
            return 1;
        }
    }

    private static async Task<int> Run(string[] args)
    {
        Console.WriteLine("HP Bluetooth provisioning probe");
        var logger = new DiagnosticLogger();
        var sys = new HPSystemUtilities();
        var provider = new BleProvider(sys, null, logger)
        {
            IsEnabled = true,
            IncludeNonWifiSetup = true
        };

        using (var watcher = provider.StartWatch())
        {
            if (watcher == null || !watcher.Start(OnFound))
            {
                Console.Error.WriteLine("Bluetooth scanner could not start.");
                return 2;
            }

            Console.WriteLine("Scanning for an HP printer for 30 seconds...");
            var completed = await Task.WhenAny(Found.Task, Task.Delay(TimeSpan.FromSeconds(30)));
            if (completed != Found.Task)
            {
                Console.Error.WriteLine("No setup-ready HP printer was found.");
                return 3;
            }

            var printer = await Found.Task;
            using (var refreshCts = new CancellationTokenSource(TimeSpan.FromSeconds(12)))
            {
                await printer.RefreshCharacteristicsAsync(
                    new[] {
                        HpBleGuids.WifiSetup20PropertiesCharacteristicUuid,
                        HpBleGuids.WifiSetup20StatusCharacteristicUuid,
                        HpBleGuids.WifiInfraStructureCharacteristicUuid,
                        HpBleGuids.WifiConnectionStateCharacteristicUuid
                    },
                    null,
                    refreshCts.Token);
            }
            Console.WriteLine("Found: " + printer.Name);
            Console.WriteLine("Setup Wi-Fi: " + printer.MicroAPSSID);
            Console.WriteLine("Network Wi-Fi: " + printer.WifiConnectedSSID);
            Console.WriteLine("Network address: " + printer.WifiIpv4Address);
            Console.WriteLine("Secure setup: " + printer.IsSecureWifiConfig);
            Console.WriteLine("Push-button authentication: " + printer.IsPushButtonAuthenticationSupported);
            Console.WriteLine("Push-button panel type: " + printer.PushButtonUiType);
            Console.WriteLine("PIN authentication: " + printer.IsPinAuthenticationSupported);
            Console.WriteLine("Password authentication: " + printer.IsPasswordAuthenticationSupported);
            Console.WriteLine("Ready for network config: " + printer.IsReadyForNetworkConfig);

            if (args.Length == 2 && args[0] == "--configure")
            {
                string ssid = args[1];
                Console.Write(Console.IsInputRedirected ? "Wi-Fi password loaded from Windows profile. " : "Wi-Fi password: ");
                string password = Console.ReadLine();
                using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3)))
                {
                    Console.WriteLine("Sending encrypted Wi-Fi settings...");
                    bool wrote = false;
                    var session = Guid.NewGuid();
                    for (int attempt = 1; attempt <= 20 && !wrote; attempt++)
                    {
                        wrote = await printer.WriteNetworkConfig(
                            Encoding.UTF8.GetBytes(ssid),
                            Encoding.UTF8.GetBytes(password ?? string.Empty),
                            cts.Token,
                            sessionUuid: session,
                            pbTimeout: 120,
                            configTimeout: 120,
                            validateCert: false,
                            resetFragments: attempt == 1);
                        if (!wrote) await Task.Delay(1000, cts.Token);
                    }
                    if (!wrote)
                    {
                        Console.WriteLine("Printer rejected the Bluetooth write.");
                        return 4;
                    }

                    Console.WriteLine("SETTINGS_SENT: Press Information + Continue together on the printer now.");
                    var previous = printer.Status;
                    Console.WriteLine("Printer status: " + previous);
                    for (int i = 0; i < 120 && !cts.IsCancellationRequested; i++)
                    {
                        await printer.RefreshCharacteristicsAsync(
                            new[] { HpBleGuids.WifiSetup20StatusCharacteristicUuid }, null, cts.Token);
                        if (!printer.Status.Equals(previous))
                        {
                            previous = printer.Status;
                            Console.WriteLine("Printer status: " + previous);
                        }
                        if (previous.ToString() == "Success") return 0;
                        if (previous.ToString().IndexOf("Error", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            previous.ToString().IndexOf("Invalid", StringComparison.OrdinalIgnoreCase) >= 0)
                            return 5;
                        await Task.Delay(1000, cts.Token);
                    }
                    return 6;
                }
            }
        }
        return 0;
    }

    private static void OnFound(IHpBleDevice printer)
    {
        if (printer != null && printer.Name != null && printer.Name.IndexOf("M209", StringComparison.OrdinalIgnoreCase) >= 0)
            Found.TrySetResult(printer);
    }
}

internal sealed class DiagnosticLogger : ILogHelper
{
    public bool IsLoggingEnabled { get { return false; } }
    public bool IsOobeLoggingEnabled { get { return false; } }
    public int MaxLogLevel { get { return 10; } }
    public string LogFileName { get; set; }
    public string OobeLogFileName { get; set; }
    public string OobeLogFolder { get; set; }
    public long LogFileMaxBytes { get; set; }
    public Task InitializeLog() { return Task.CompletedTask; }
    public void LogEnter(string m, bool o=false, string f=null, string n=null) { }
    public void LogEnter(Func<string> m, bool o=false, string f=null, string n=null) { }
    public void LogExit(string m, bool o=false, string f=null, string n=null) { }
    public void LogExit(Func<string> m, bool o=false, string f=null, string n=null) { }
    public void LogError(string m, bool o=false, string f=null, string n=null) { Console.Error.WriteLine("HP error in " + n); }
    public void LogError(Func<string> m, bool o=false, string f=null, string n=null) { Console.Error.WriteLine("HP error in " + n); }
    public void LogException(string m, Exception e, bool o=false, string f=null, string n=null) { Console.Error.WriteLine("HP exception in " + n + ": " + e.GetType().Name + ": " + e.Message); }
    public void LogException(Func<string> m, Exception e, bool o=false, string f=null, string n=null) { Console.Error.WriteLine("HP exception in " + n + ": " + e.GetType().Name + ": " + e.Message); }
    public void LogInfo(LogLevel l, string m, bool o=false, string f=null, string n=null) { }
    public void LogInfo(LogLevel l, Func<string> m, bool o=false, string f=null, string n=null) { }
    public void LogTiming(string m, long s, bool o=false, string f=null, string n=null) { }
    public void LogTiming(Func<string> m, long s, bool o=false, string f=null, string n=null) { }
    public Task ForceSaveLog(bool o=false) { return Task.CompletedTask; }
    public Task ClearLog(bool o=false) { return Task.CompletedTask; }
    public Task DisplayLog(Action<string,string> a) { return Task.CompletedTask; }
}
