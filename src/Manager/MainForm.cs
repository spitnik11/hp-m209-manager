using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HpM209Manager
{
    internal sealed class MainForm : Form
    {
        private const string PrinterName = "HP LaserJet M209dw";
        private readonly Label status = new Label();
        private readonly Label details = new Label();
        private readonly ProgressBar progress = new ProgressBar();
        private readonly Button connect = new Button();
        private readonly Button test = new Button();
        private readonly Button web = new Button();
        private readonly Button refresh = new Button();
        private readonly ListBox activity = new ListBox();
        private readonly Timer timer = new Timer();
        private string printerIp;
        private bool busy;

        private string SettingsFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HP M209 Manager");
        private string SettingsPath => Path.Combine(SettingsFolder, "printer-ip.txt");
        private string ConnectorPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "connector", "HpBleProbe.exe");

        public MainForm()
        {
            Text = "HP M209 Manager";
            ClientSize = new Size(700, 520);
            MinimumSize = new Size(620, 470);
            Font = new Font("Segoe UI", 10F);
            BackColor = Color.FromArgb(247, 248, 250);
            StartPosition = FormStartPosition.CenterScreen;

            var title = new Label { Text = "HP LaserJet M209dw", Font = new Font("Segoe UI Semibold", 22F), AutoSize = true, Location = new Point(28, 22) };
            status.Text = "Checking printer…";
            status.Font = new Font("Segoe UI Semibold", 13F);
            status.AutoSize = true;
            status.Location = new Point(31, 76);
            details.AutoSize = true;
            details.ForeColor = Color.DimGray;
            details.Location = new Point(32, 108);

            connect.Text = "Connect / reconnect";
            test.Text = "Print test page";
            web.Text = "Open printer settings";
            refresh.Text = "Refresh status";
            var buttons = new[] { connect, test, web, refresh };
            for (int i = 0; i < buttons.Length; i++)
            {
                buttons[i].Size = new Size(150, 42);
                buttons[i].Location = new Point(32 + (i % 2) * 166, 155 + (i / 2) * 54);
                buttons[i].FlatStyle = FlatStyle.System;
            }
            connect.Size = new Size(150, 42);
            web.Size = new Size(150, 42);

            progress.Location = new Point(32, 272);
            progress.Size = new Size(632, 5);
            progress.Style = ProgressBarStyle.Marquee;
            progress.Visible = false;

            var activityTitle = new Label { Text = "Activity", Font = new Font("Segoe UI Semibold", 11F), AutoSize = true, Location = new Point(29, 298) };
            activity.Location = new Point(32, 328);
            activity.Size = new Size(632, 154);
            activity.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            activity.BorderStyle = BorderStyle.FixedSingle;

            Controls.AddRange(new Control[] { title, status, details, connect, test, web, refresh, progress, activityTitle, activity });
            connect.Click += async (_, __) => await ConnectPrinter();
            test.Click += (_, __) => PrintTestPage();
            web.Click += (_, __) => OpenWebPage();
            refresh.Click += async (_, __) => await RefreshStatus(true);
            Shown += async (_, __) => await RefreshStatus(false);
            timer.Interval = 15000;
            timer.Tick += async (_, __) => await RefreshStatus(false);
            timer.Start();
        }

        private void Log(string message)
        {
            activity.Items.Insert(0, DateTime.Now.ToString("h:mm:ss tt") + "  " + message);
        }

        private void SetBusy(bool value)
        {
            busy = value;
            progress.Visible = value;
            connect.Enabled = refresh.Enabled = !value;
        }

        private async Task RefreshStatus(bool announce)
        {
            if (busy) return;
            SetBusy(true);
            try
            {
                if (string.IsNullOrWhiteSpace(printerIp) && File.Exists(SettingsPath)) printerIp = File.ReadAllText(SettingsPath).Trim();
                if (!await IsPrinter(printerIp)) printerIp = await FindPrinter();

                if (!string.IsNullOrWhiteSpace(printerIp))
                {
                    Directory.CreateDirectory(SettingsFolder);
                    File.WriteAllText(SettingsPath, printerIp);
                    EnsureWindowsPrinter(printerIp);
                    status.Text = "● Online and ready";
                    status.ForeColor = Color.FromArgb(18, 128, 72);
                    details.Text = "Network address: " + printerIp + "   •   Windows printer: " + PrinterName;
                    test.Enabled = web.Enabled = true;
                    if (announce) Log("Printer is online at " + printerIp + ".");
                }
                else
                {
                    status.Text = "● Printer not found";
                    status.ForeColor = Color.FromArgb(180, 75, 45);
                    details.Text = "Use Connect / reconnect to send the current Wi-Fi network over Bluetooth.";
                    test.Enabled = web.Enabled = false;
                    if (announce) Log("Printer was not found on the current network.");
                }
            }
            catch (Exception ex) { Log("Status check failed: " + ex.Message); }
            finally { SetBusy(false); }
        }

        private async Task ConnectPrinter()
        {
            if (!File.Exists(ConnectorPath))
            {
                MessageBox.Show("The Bluetooth connector is missing. Reinstall this application.", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            var ssid = ReadCommand("netsh", "wlan show interfaces", @"(?m)^\s*SSID\s*:\s*(.+)$");
            if (string.IsNullOrWhiteSpace(ssid))
            {
                MessageBox.Show("Connect this PC to the Wi-Fi network you want the printer to use, then try again.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var key = ReadCommand("netsh", "wlan show profile name=\"" + ssid + "\" key=clear", @"(?m)^\s*Key Content\s*:\s*(.+)$");
            if (string.IsNullOrWhiteSpace(key))
            {
                MessageBox.Show("Windows could not read the saved password for " + ssid + ".", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (MessageBox.Show("Make sure the printer light is pulsing purple.\n\nConnect it to “" + ssid + "” now?", Text, MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK) return;

            SetBusy(true);
            status.Text = "Connecting over Bluetooth…";
            status.ForeColor = Color.FromArgb(45, 82, 160);
            Log("Searching for the printer over Bluetooth.");
            try
            {
                var psi = new ProcessStartInfo(ConnectorPath, "--configure \"" + ssid.Replace("\"", "") + "\"")
                {
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(ConnectorPath)
                };
                using (var process = Process.Start(psi))
                {
                    process.StandardInput.WriteLine(key);
                    process.StandardInput.Close();
                    key = null;
                    while (!process.StandardOutput.EndOfStream)
                    {
                        var line = await process.StandardOutput.ReadLineAsync();
                        if (line == null) continue;
                        if (line.StartsWith("Found:")) Log(line);
                        if (line.StartsWith("SETTINGS_SENT:"))
                        {
                            Log("Waiting for confirmation on the printer.");
                            BeginInvoke((Action)(() => MessageBox.Show("Press Information + Continue together once on the printer, then release them.", "Confirm on printer", MessageBoxButtons.OK, MessageBoxIcon.Information)));
                        }
                        if (line.StartsWith("Printer status:")) Log(line);
                    }
                    await Task.Run(() => process.WaitForExit());
                    if (process.ExitCode != 0) throw new InvalidOperationException("Bluetooth setup ended with code " + process.ExitCode + ". Put the printer in purple setup mode and retry.");
                }
                Log("Wi-Fi setup succeeded. Finding the printer on the network…");
                printerIp = null;
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); Log(ex.Message); }
            finally { SetBusy(false); }
            await RefreshStatus(true);
        }

        private void PrintTestPage()
        {
            try
            {
                EnsureWindowsPrinter(printerIp);
                Process.Start(new ProcessStartInfo("rundll32.exe", "printui.dll,PrintUIEntry /k /n \"" + PrinterName + "\"") { UseShellExecute = false, CreateNoWindow = true });
                Log("Windows test page sent.");
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void OpenWebPage()
        {
            if (!string.IsNullOrWhiteSpace(printerIp)) Process.Start("http://" + printerIp + "/");
        }

        private static string ReadCommand(string file, string args, string pattern)
        {
            var psi = new ProcessStartInfo(file, args) { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true };
            using (var p = Process.Start(psi))
            {
                var output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                var match = Regex.Match(output, pattern);
                return match.Success ? match.Groups[1].Value.Trim() : null;
            }
        }

        private static void EnsureWindowsPrinter(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip)) return;
            var script = "$p='" + ip + "'; if(-not(Get-PrinterPort -Name $p -ErrorAction SilentlyContinue)){Add-PrinterPort -Name $p -PrinterHostAddress $p -PortNumber 9100}; " +
                         "if(-not(Get-Printer -Name '" + PrinterName + "' -ErrorAction SilentlyContinue)){Add-Printer -Name '" + PrinterName + "' -DriverName 'HP LaserJet M207-M212 PCLm-S' -PortName $p}else{Set-Printer -Name '" + PrinterName + "' -PortName $p}";
            using (var p = Process.Start(new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -Command \"" + script + "\"") { UseShellExecute = false, CreateNoWindow = true })) p.WaitForExit();
        }

        private static async Task<bool> IsPrinter(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip)) return false;
            try
            {
                using (var tcp = new TcpClient())
                {
                    var connect = tcp.ConnectAsync(ip, 9100);
                    return await Task.WhenAny(connect, Task.Delay(500)) == connect && tcp.Connected;
                }
            }
            catch { return false; }
        }

        private static async Task<string> FindPrinter()
        {
            var local = Dns.GetHostAddresses(Dns.GetHostName()).FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a));
            if (local == null) return null;
            var octets = local.ToString().Split('.');
            var prefix = octets[0] + "." + octets[1] + "." + octets[2] + ".";
            var tasks = Enumerable.Range(1, 254).Select(async i =>
            {
                var ip = prefix + i;
                return await IsPrinter(ip) ? ip : null;
            }).ToArray();
            var results = await Task.WhenAll(tasks);
            return results.FirstOrDefault(x => x != null);
        }
    }
}
