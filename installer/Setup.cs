// Independently written OpenAC per-user installer UI. No vendor or installer-tool code.
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RogControlInstaller {
    internal static class SetupProgram {
        [STAThread] internal static int Main(string[] args) {
            try {
                var payload = InstallerCore.Embedded();
                if (args.Length == 1 && args[0] == "--verify") {
                    InstallerCore.ValidateTarget(InstallerCore.DefaultDirectory, InstallerCore.Programs);
                    WriteVerification(new { ok = true, product = InstallerCore.Product, version = payload.Version, files = payload.Files.Count, bytes = payload.Files.Sum(f => f.Bytes), installed = false, hardwareExecuted = false });
                    return 0;
                }
                bool uninstall = args.Length == 1 && args[0] == "--uninstall";
                int parent = 0;
                bool copied = args.Length == 2 && args[0] == "--uninstall-copy" && Int32.TryParse(args[1], out parent) && parent > 0;
                if (args.Length != 0 && !uninstall && !copied) throw new InvalidDataException("参数无效。支持 --verify 或已安装程序的 --uninstall。");
                string self = InstallerCore.Canonical(Assembly.GetExecutingAssembly().Location);
                if (uninstall && !String.Equals(self, Path.Combine(InstallerCore.DefaultDirectory, InstallerCore.Uninstaller), StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("请从已安装 OpenAC 的卸载入口运行。");
                if (copied) ValidateTemporaryCopy(self);
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new SetupForm(payload, uninstall || copied, copied, parent));
                return 0;
            } catch (Exception error) {
                if (args.Length > 0 && args[0] == "--verify") { WriteVerification(new { ok = false, error = error.Message, installed = false }); return 1; }
                MessageBox.Show(error.Message, "OpenAC 安装器", MessageBoxButtons.OK, MessageBoxIcon.Error); return 1;
            }
        }
        internal static void WriteVerification(object value) {
            // WinExe has no console code-page handle. Write UTF-8 directly to the redirected pipe.
            using (var writer = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false))) { writer.WriteLine(InstallerCore.Json.Serialize(value)); writer.Flush(); }
        }
        internal static void ValidateTemporaryCopy(string self) {
            string temp = InstallerCore.Canonical(Path.GetTempPath());
            string directory = Path.GetDirectoryName(self), name = Path.GetFileName(directory);
            Guid token;
            if (!InstallerCore.Within(self, temp) || Path.GetFileName(self) != InstallerCore.Uninstaller || !name.StartsWith("OpenAC-Uninstall-", StringComparison.Ordinal) || !Guid.TryParseExact(name.Substring("OpenAC-Uninstall-".Length), "N", out token) || !String.Equals(Path.GetDirectoryName(directory), temp, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("临时卸载程序位置不匹配。");
            InstallerCore.AssertNoReparse(self);
        }
        internal static void WaitForOriginal(int pid) {
            try { using (var process = Process.GetProcessById(pid)) { if (!process.WaitForExit(10000)) throw new IOException("原卸载窗口尚未退出，请关闭后重试。"); } }
            catch (ArgumentException) { }
        }
        internal static void LaunchTemporaryCopy() {
            string self = InstallerCore.Canonical(Assembly.GetExecutingAssembly().Location);
            string source = Path.Combine(InstallerCore.DefaultDirectory, InstallerCore.Uninstaller);
            if (!String.Equals(self, source, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("卸载程序位置不匹配。");
            InstallerCore.ReadState(InstallerCore.DefaultDirectory);
            string directory = Path.Combine(Path.GetTempPath(), "OpenAC-Uninstall-" + Guid.NewGuid().ToString("N"));
            InstallerCore.AssertNoReparse(directory); Directory.CreateDirectory(directory);
            string destination = Path.Combine(directory, InstallerCore.Uninstaller);
            try {
                InstallerCore.AssertNoReparse(destination); File.Copy(self, destination, false); InstallerCore.AssertNoReparse(destination);
                if (InstallerCore.HashFile(self) != InstallerCore.HashFile(destination)) throw new IOException("临时卸载程序复制校验失败。");
                Process.Start(new ProcessStartInfo(destination, "--uninstall-copy " + Process.GetCurrentProcess().Id) { UseShellExecute = false, WorkingDirectory = directory });
            } catch {
                if (File.Exists(destination) && InstallerCore.HashFile(destination) == InstallerCore.HashFile(self)) File.Delete(destination);
                if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory, false);
                throw;
            }
        }
    }
    internal sealed class SetupForm : Form {
        readonly Payload payload; readonly bool uninstall, copied; readonly int parent;
        readonly CheckBox accept = new CheckBox(), menu = new CheckBox(), desktop = new CheckBox();
        readonly Button action = new Button(), close = new Button(), launch = new Button();
        readonly TextBox status = new TextBox();
        bool busy, finished;
        internal SetupForm(Payload data, bool remove, bool temporary, int parentPid) {
            payload = data; uninstall = remove; copied = temporary; parent = parentPid;
            Text = uninstall ? "卸载 OpenAC" : "安装 OpenAC"; Font = new Font("Microsoft YaHei UI", 9f); ClientSize = new Size(660, 660); MinimumSize = new Size(620, 600); StartPosition = FormStartPosition.CenterScreen;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), ColumnCount = 1, RowCount = 9 };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); for (int n = 5; n < 9; n++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(new Label { Text = "OpenAC  " + payload.Version + " Preview", AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(0, 0, 0, 14) }, 0, 0);
            layout.Controls.Add(new Label { Text = "非 ASUS 官方软件 · MIT 开源 · 此发行版未签名\r\n设备写入默认关闭；安装不会修改驱动、服务或硬件设置。", AutoSize = true, Margin = new Padding(0, 0, 0, 12) }, 0, 1);
            layout.Controls.Add(new Label { Text = uninstall ? "将移除下列固定目录中内容未修改的发行文件。用户配置保留。" : "仅安装到当前用户的固定目录，无需管理员权限。", AutoSize = true }, 0, 2);
            layout.Controls.Add(new TextBox { Text = InstallerCore.DefaultDirectory, ReadOnly = true, Dock = DockStyle.Top, Margin = new Padding(0, 6, 0, 12) }, 0, 3);
            layout.Controls.Add(new TextBox { Text = payload.License, ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Font = new Font("Consolas", 9f) }, 0, 4);
            var options = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0, 12, 0, 8) };
            accept.Text = "我已阅读 MIT 许可证，安装此预览版"; accept.AutoSize = true; accept.CheckedChanged += (s, e) => UpdateAction();
            menu.Text = "创建开始菜单快捷方式"; menu.Checked = true; menu.AutoSize = true; desktop.Text = "创建桌面快捷方式"; desktop.AutoSize = true;
            if (!uninstall) { options.Controls.Add(accept); options.Controls.Add(menu); options.Controls.Add(desktop); } else options.Controls.Add(new Label { Text = "请先关闭 OpenAC。修改过的文件和未知文件不会被删除。", AutoSize = true });
            layout.Controls.Add(options, 0, 5);
            status.Multiline = true; status.ReadOnly = true; status.ScrollBars = ScrollBars.Vertical; status.Height = 92; status.Dock = DockStyle.Top; status.Text = uninstall ? "点击卸载后会核对本安装器内嵌载荷与安装记录。" : "安装不会自动启动 OpenAC。";
            layout.Controls.Add(status, 0, 6);
            var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            close.Text = "取消"; close.AutoSize = true; close.Click += (s, e) => Close(); action.Text = uninstall ? "卸载" : "安装"; action.AutoSize = true; action.Click += async (s, e) => await Act(); launch.Text = "打开 OpenAC"; launch.AutoSize = true; launch.Visible = false; launch.Click += (s, e) => { try { Process.Start(new ProcessStartInfo(Path.Combine(InstallerCore.DefaultDirectory, "OpenAC.exe")) { UseShellExecute = true }); } catch (Exception error) { status.Text = error.Message; } };
            buttons.Controls.Add(close); buttons.Controls.Add(action); buttons.Controls.Add(launch); layout.Controls.Add(buttons, 0, 7);
            layout.Controls.Add(new Label { Text = "配置继续使用 LocalAppData\\ROG Control；卸载不删除此目录。", AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(0, 10, 0, 0) }, 0, 8);
            Controls.Add(layout); FormClosing += (s, e) => { if (busy) e.Cancel = true; }; UpdateAction();
            if (copied) Shown += async (s, e) => await Act();
        }
        void UpdateAction() { action.Enabled = !busy && !finished && (uninstall || accept.Checked); close.Enabled = !busy; accept.Enabled = !busy; menu.Enabled = !busy; desktop.Enabled = !busy; }
        async Task Act() {
            if (busy || finished || !uninstall && !accept.Checked) return;
            busy = true; UpdateAction(); status.Text = uninstall ? "正在核对并移除发行文件…" : "正在校验并安装…";
            try {
                if (uninstall && !copied) { SetupProgram.LaunchTemporaryCopy(); busy = false; Close(); return; }
                if (uninstall) {
                    var retained = await Task.Run(() => { SetupProgram.WaitForOriginal(parent); return InstallerCore.UninstallFiles(InstallerCore.ReadState(InstallerCore.DefaultDirectory), Assembly.GetExecutingAssembly().Location); });
                    status.Text = "卸载流程已完成，用户配置保留。" + (retained.Count == 0 ? "" : "\r\n保留的修改文件：\r\n" + String.Join("\r\n", retained)) + "\r\n临时卸载副本位于：\r\n" + Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\r\n关闭此窗口后可删除该临时目录。";
                } else {
                    bool start = menu.Checked, desk = desktop.Checked;
                    await Task.Run(() => InstallerCore.Install(payload, InstallerCore.DefaultDirectory, start, desk, Assembly.GetExecutingAssembly().Location));
                    status.Text = "安装完成。设备控制仍默认关闭；可手动打开 OpenAC。"; launch.Visible = true;
                }
                finished = true; close.Text = "关闭";
            } catch (Exception error) { status.Text = "操作未完成：" + error.Message; }
            finally { busy = false; UpdateAction(); }
        }
    }
}

