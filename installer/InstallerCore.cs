// Original installer implementation. Windows/.NET interface facts only; see README.md.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace RogControlInstaller {
    internal sealed class PayloadFile { public string Path; public long Bytes; public string Sha256; public byte[] Data; }
    internal sealed class Payload {
        internal string Version;
        internal List<PayloadFile> Files;
        internal string License { get { return Encoding.UTF8.GetString(Files.Single(f => f.Path == "LICENSE").Data); } }
    }
    internal sealed class Installation {
        internal string Directory;
        internal string Version;
        internal List<PayloadFile> Files;
        internal bool StartMenu, Desktop;
    }
    internal static class InstallerCore {
        internal const string Product = "org.openac.community.preview";
        internal const string StateName = "INSTALLER-OWNERSHIP.json";
        internal const string Uninstaller = "Uninstall.exe";
        internal const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\OpenACCommunityPreview";
        internal const int MaximumBytes = 32 * 1024 * 1024;
        internal const string MutationMutexName = @"Local\OpenAC-Installer";
        sealed class MutationGuard : IDisposable {
            Mutex mutex; bool owns;
            internal MutationGuard() {
                mutex = new Mutex(false, MutationMutexName);
                try { try { owns = mutex.WaitOne(0); } catch (AbandonedMutexException) { owns = true; } if (!owns) throw new IOException("另一个 OpenAC 安装或卸载操作正在运行，请稍后重试。"); }
                catch { mutex.Dispose(); mutex = null; throw; }
            }
            public void Dispose() { if (mutex == null) return; if (owns) mutex.ReleaseMutex(); mutex.Dispose(); mutex = null; }
        }
        internal static readonly string[] RuntimeFiles = {
            "OpenAC.exe", "OpenAC.exe.config", "DirectAuraWorker.exe", "GearLinkWorker.exe", "SDKWorker.exe",
            "StorageWorker.exe", "AudioEndpointWorker.exe", "DiscoveryWorker.exe", "NotebookWorker.exe", "LICENSE", "THIRD-PARTY-NOTICES.md",
            "PROVENANCE.md", "DirectAuraWorker-LICENSE.txt", "README.md", "PUBLIC-RELEASE.md", "assets/products/manifest.json", "assets/products/SOURCES.md"
        };
        internal static string Programs { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"); } }
        internal static string DefaultDirectory { get { return Path.Combine(Programs, "OpenAC"); } }
        internal static string StartMenuDirectory { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "OpenAC"); } }
        internal static string DesktopLink { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "OpenAC.lnk"); } }
        internal static string StartAppLink { get { return Path.Combine(StartMenuDirectory, "OpenAC.lnk"); } }
        internal static string StartUninstallLink { get { return Path.Combine(StartMenuDirectory, "Uninstall OpenAC.lnk"); } }
        internal static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 1024 * 1024, RecursionLimit = 20 };
        internal static string Hash(byte[] data) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant(); }
        internal static string HashFile(string path) { using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "").ToLowerInvariant(); }
        internal static Dictionary<string, object> Object(object value, params string[] keys) {
            var d = value as Dictionary<string, object>;
            if (d == null || d.Count != keys.Length || keys.Any(k => !d.ContainsKey(k))) throw new InvalidDataException("对象字段与安装清单不一致。");
            return d;
        }
        internal static string Text(object value, int maximum) {
            string s = value as string;
            if (s == null || s.Length == 0 || s.Length > maximum || s.Any(c => char.IsControl(c))) throw new InvalidDataException("清单字符串无效。");
            return s;
        }
        internal static long Number(object value) {
            if (!(value is int) && !(value is long)) throw new InvalidDataException("清单整数无效。");
            return Convert.ToInt64(value);
        }
        internal static bool Flag(object value) { if (!(value is bool)) throw new InvalidDataException("清单开关无效。"); return (bool)value; }
        internal static string Version(object value) {
            string s = Text(value, 40); Version parsed;
            if (!System.Version.TryParse(s, out parsed) || parsed.Major < 0 || parsed.Build < 0) throw new InvalidDataException("版本号无效。");
            return s;
        }
        internal static string Relative(object value) {
            string s = Text(value, 180);
            if (s.Contains("\\") || s.Contains(":") || s.StartsWith("/") || s.EndsWith("/") || s.Split('/').Any(InvalidSegment)) throw new InvalidDataException("清单路径无效。");
            return s;
        }
        internal static bool InvalidSegment(string segment) {
            if (segment.Length == 0 || segment == "." || segment == ".." || segment.EndsWith(" ") || segment.EndsWith(".") || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return true;
            string stem = segment.Split('.')[0].ToUpperInvariant();
            return stem == "CON" || stem == "PRN" || stem == "AUX" || stem == "NUL" || stem == "CONIN$" || stem == "CONOUT$" || (stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && stem[3] >= '1' && stem[3] <= '9');
        }
        internal static PayloadFile ParseFile(object value, bool allowUninstaller) {
            var d = Object(value, "path", "bytes", "sha256");
            string path = Relative(d["path"]);
            if (!RuntimeFiles.Contains(path, StringComparer.Ordinal) && !(allowUninstaller && path == Uninstaller)) throw new InvalidDataException("清单含有白名单以外的文件：" + path);
            long bytes = Number(d["bytes"]);
            string hash = Text(d["sha256"], 64);
            if (bytes < 0 || bytes > MaximumBytes || hash.Length != 64 || hash.Any(c => !(c >= '0' && c <= '9' || c >= 'a' && c <= 'f'))) throw new InvalidDataException("文件长度或 SHA256 无效。");
            return new PayloadFile { Path = path, Bytes = bytes, Sha256 = hash };
        }
        internal static List<PayloadFile> ParseFiles(object value, bool owned) {
            var array = value as object[];
            if (array == null || array.Length != RuntimeFiles.Length + (owned ? 1 : 0)) throw new InvalidDataException("安装清单数量无效。");
            var files = array.Select(v => ParseFile(v, owned)).ToList();
            if (files.Select(f => f.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != files.Count || RuntimeFiles.Any(p => !files.Any(f => f.Path == p)) || owned && !files.Any(f => f.Path == Uninstaller)) throw new InvalidDataException("安装清单缺项或重名。");
            if (files.Sum(f => f.Bytes) > MaximumBytes) throw new InvalidDataException("安装载荷过大。");
            return files;
        }
        internal static Payload ReadPayload(Stream metadata, Stream archive) {
            string raw;
            using (var reader = new StreamReader(metadata, Encoding.UTF8, true, 1024, true)) {
                var text = new StringBuilder(); var characters = new char[4096]; int count;
                while ((count = reader.Read(characters, 0, characters.Length)) > 0) { if (text.Length + count > 1024 * 1024) throw new InvalidDataException("安装载荷清单过大。"); text.Append(characters, 0, count); }
                raw = text.ToString();
            }
            var d = Object(Json.DeserializeObject(raw), "schemaVersion", "product", "version", "files");
            if (Number(d["schemaVersion"]) != 1 || Text(d["product"], 100) != Product) throw new InvalidDataException("安装载荷身份无效。");
            var payload = new Payload { Version = Version(d["version"]), Files = ParseFiles(d["files"], false) };
            using (var zip = new ZipArchive(archive, ZipArchiveMode.Read, true)) {
                if (zip.Entries.Count != payload.Files.Count) throw new InvalidDataException("ZIP 与清单数量不一致。");
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in zip.Entries) {
                    string path = Relative(entry.FullName);
                    var file = payload.Files.SingleOrDefault(f => f.Path == path);
                    if (!seen.Add(path) || file == null || file.Bytes != entry.Length) throw new InvalidDataException("ZIP 含有重复或不一致的文件。");
                    using (var input = entry.Open()) using (var output = new MemoryStream()) {
                        var buffer = new byte[32768]; int read;
                        while ((read = input.Read(buffer, 0, buffer.Length)) > 0) { if (output.Length + read > file.Bytes) throw new InvalidDataException("ZIP 文件超过声明长度。"); output.Write(buffer, 0, read); }
                        file.Data = output.ToArray();
                    }
                    if (file.Data.LongLength != file.Bytes || Hash(file.Data) != file.Sha256) throw new InvalidDataException("安装载荷校验失败：" + path);
                }
            }
            if (!payload.License.Contains("MIT License")) throw new InvalidDataException("本发行版缺少已审查的 MIT 许可证。");
            return payload;
        }
        internal static Payload Embedded() {
            var assembly = Assembly.GetExecutingAssembly();
            using (var metadata = assembly.GetManifestResourceStream("InstallerPayload.json")) using (var archive = assembly.GetManifestResourceStream("InstallerPayload.zip")) {
                if (metadata == null || archive == null) throw new InvalidDataException("缺少内嵌安装载荷。");
                return ReadPayload(metadata, archive);
            }
        }
        internal static string Canonical(string path) {
            if (string.IsNullOrWhiteSpace(path) || path.Length > 220 || path.Any(char.IsControl) || path.Contains("\"")) throw new InvalidDataException("目录无效。");
            if (path.Length < 4 || !char.IsLetter(path[0]) || path[1] != ':' || path[2] != '\\' || path.Substring(2).Contains(':') || path.Contains('/') || path.StartsWith(@"\\")) throw new InvalidDataException("请使用本机绝对目录。");
            string full = Path.GetFullPath(path).TrimEnd('\\');
            if (path.TrimEnd('\\') != full || full.Split('\\').Skip(1).Any(InvalidSegment)) throw new InvalidDataException("请使用规范目录，不要包含相对路径或尾部空格。");
            return full;
        }
        internal static bool Within(string full, string root) { return full.StartsWith(root.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase); }
        internal static string ValidateTarget(string path, string programs) {
            string full = Canonical(path), root = Canonical(programs);
            if (!string.Equals(full, Path.Combine(root, "OpenAC"), StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("安装目录必须为当前用户的 LocalAppData\\Programs\\OpenAC。");
            return full;
        }
        internal static void AssertNoReparse(string path) {
            string full = Canonical(path);
            while (!string.IsNullOrEmpty(full)) {
                try { if ((File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("目录或文件包含链接、junction 或 reparse point，操作已停止。"); }
                catch (FileNotFoundException) { } catch (DirectoryNotFoundException) { }
                string parent = Path.GetDirectoryName(full); if (parent == full) break; full = parent;
            }
        }
        internal static string Destination(string root, string relative) {
            Relative(relative);
            string path = Path.GetFullPath(Path.Combine(root, relative.Replace('/', '\\')));
            if (!Within(path, root)) throw new InvalidDataException("安装文件越界。");
            AssertNoReparse(path);
            return path;
        }
        internal static void MakeDirectories(string path, List<string> made) {
            if (Directory.Exists(path)) { AssertNoReparse(path); return; }
            string parent = Path.GetDirectoryName(path);
            if (!Directory.Exists(parent)) MakeDirectories(parent, made);
            AssertNoReparse(parent); Directory.CreateDirectory(path); made.Add(path); AssertNoReparse(path);
        }
        internal static object[] FileRecords(IEnumerable<PayloadFile> files) { return files.Select(f => (object)new { path = f.Path, bytes = f.Bytes, sha256 = f.Sha256 }).ToArray(); }
        internal static string SerializeState(Installation state) { return Json.Serialize(new { schemaVersion = 1, product = Product, version = state.Version, installDirectory = state.Directory, startMenu = state.StartMenu, desktop = state.Desktop, files = FileRecords(state.Files) }); }
        internal static Installation ParseState(string raw, string directory, string programs, Payload trusted, PayloadFile trustedSelf) {
            string root = ValidateTarget(directory, programs);
            if (raw == null || raw.Length > 1024 * 1024 || trusted == null || trusted.Files == null || trustedSelf == null || trustedSelf.Path != Uninstaller) throw new InvalidDataException("安装记录或可信载荷缺失。");
            var d = Object(Json.DeserializeObject(raw), "schemaVersion", "product", "version", "installDirectory", "startMenu", "desktop", "files");
            if (Number(d["schemaVersion"]) != 1 || Text(d["product"], 100) != Product || !string.Equals(ValidateTarget(Text(d["installDirectory"], 220), programs), root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("安装记录位置或身份不一致。");
            string version = Version(d["version"]);
            var files = ParseFiles(d["files"], true);
            if (version != trusted.Version || trusted.Files.Count != RuntimeFiles.Length || trusted.Files.Select(f => f.Path).Distinct(StringComparer.Ordinal).Count() != RuntimeFiles.Length || trusted.Files.Any(f => !RuntimeFiles.Contains(f.Path, StringComparer.Ordinal))) throw new InvalidDataException("安装记录与内嵌发行版本不一致。");
            var expected = trusted.Files.Concat(new[] { trustedSelf }).ToDictionary(f => f.Path, StringComparer.Ordinal);
            foreach (var file in files) {
                PayloadFile original;
                if (!expected.TryGetValue(file.Path, out original) || file.Bytes != original.Bytes || file.Sha256 != original.Sha256) throw new InvalidDataException("安装记录不匹配内嵌原始载荷：" + file.Path);
            }
            return new Installation { Directory = root, Version = version, StartMenu = Flag(d["startMenu"]), Desktop = Flag(d["desktop"]), Files = files };
        }
        internal static PayloadFile SelfMetadata(string path) {
            AssertNoReparse(path);
            var length = new FileInfo(path).Length;
            if (length <= 0 || length > MaximumBytes) throw new InvalidDataException("安装器文件长度无效。");
            return new PayloadFile { Path = Uninstaller, Bytes = length, Sha256 = HashFile(path) };
        }
        internal static Installation ReadState(string directory) {
            string root = ValidateTarget(directory, Programs); AssertNoReparse(root);
            string file = Destination(root, StateName);
            if (new FileInfo(file).Length > 1024 * 1024) throw new InvalidDataException("安装记录过大。");
            return ParseState(File.ReadAllText(file, Encoding.UTF8), root, Programs, Embedded(), SelfMetadata(Assembly.GetExecutingAssembly().Location));
        }
        internal static void WriteNew(string path, byte[] data, List<string> made) {
            AssertNoReparse(path);
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { made.Add(path); stream.Write(data, 0, data.Length); stream.Flush(true); }
            AssertNoReparse(path);
            if (HashFile(path) != Hash(data)) throw new IOException("落盘校验失败。");
        }
        internal static bool RegistryExists() { using (var key = Registry.CurrentUser.OpenSubKey(RegistryPath)) return key != null; }
        internal static bool RegistryMatches(string root) {
            using (var key = Registry.CurrentUser.OpenSubKey(RegistryPath)) return key != null && string.Equals(key.GetValue("InstallLocation") as string, root, StringComparison.OrdinalIgnoreCase) && (key.GetValue("OpenACProduct") as string) == Product;
        }
        internal static void Shortcut(string path, string target, string args) {
            AssertNoReparse(path);
            object shell = null, link = null;
            try {
                shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell", true));
                link = shell.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { path });
                Action<string, object> set = (name, value) => link.GetType().InvokeMember(name, BindingFlags.SetProperty, null, link, new object[] { value });
                set("TargetPath", target); set("Arguments", args); set("WorkingDirectory", Path.GetDirectoryName(target)); set("Description", "OpenAC — 非 ASUS 官方软件");
                link.GetType().InvokeMember("Save", BindingFlags.InvokeMethod, null, link, null);
            } finally { if (link != null && Marshal.IsComObject(link)) Marshal.FinalReleaseComObject(link); if (shell != null && Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell); }
        }
        internal static bool ShortcutMatches(string path, string target, string args) {
            AssertNoReparse(path); if (!File.Exists(path)) return true;
            object shell = null, link = null;
            try {
                shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell", true)); link = shell.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { path });
                var type = link.GetType();
                return string.Equals(type.InvokeMember("TargetPath", BindingFlags.GetProperty, null, link, null) as string, target, StringComparison.OrdinalIgnoreCase) && string.Equals(type.InvokeMember("Arguments", BindingFlags.GetProperty, null, link, null) as string, args, StringComparison.Ordinal);
            } finally { if (link != null && Marshal.IsComObject(link)) Marshal.FinalReleaseComObject(link); if (shell != null && Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell); }
        }
        internal static void Install(Payload payload, string selected, bool startMenu, bool desktop, string setupPath) {
            using (var guard = new MutationGuard()) InstallInternal(payload, selected, startMenu, desktop, setupPath);
        }
        static void InstallInternal(Payload payload, string selected, bool startMenu, bool desktop, string setupPath) {
            string root = ValidateTarget(selected, Programs); AssertNoReparse(root); AssertNoReparse(setupPath);
            var sourceSelf = SelfMetadata(setupPath);
            if (payload.Files.Sum(f => f.Bytes) + sourceSelf.Bytes > MaximumBytes) throw new IOException("程序及卸载载荷合计过大。");
            if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any()) throw new IOException("安装目录必须为空。已有版本请先卸载；卸载会保留用户配置。");
            if (File.Exists(root) || RegistryExists()) throw new IOException("发现已有安装记录。请先通过原卸载程序卸载；本安装器不会覆盖旧版本。");
            var links = new List<string>();
            if (startMenu) { links.Add(StartAppLink); links.Add(StartUninstallLink); }
            if (desktop) links.Add(DesktopLink);
            foreach (string link in links) { AssertNoReparse(link); if (File.Exists(link) || Directory.Exists(link)) throw new IOException("快捷方式已存在，安装器不会覆盖：" + link); }
            var files = new List<string>(); var directories = new List<string>(); bool registryCreated = false;
            try {
                MakeDirectories(root, directories);
                foreach (var file in payload.Files) { string target = Destination(root, file.Path); MakeDirectories(Path.GetDirectoryName(target), directories); WriteNew(target, file.Data, files); }
                byte[] self = File.ReadAllBytes(setupPath); if (self.LongLength != sourceSelf.Bytes || Hash(self) != sourceSelf.Sha256) throw new IOException("安装器在复制前已改变。");
                WriteNew(Destination(root, Uninstaller), self, files);
                var stateFiles = payload.Files.Select(f => new PayloadFile { Path = f.Path, Bytes = f.Bytes, Sha256 = f.Sha256 }).ToList();
                stateFiles.Add(new PayloadFile { Path = Uninstaller, Bytes = self.LongLength, Sha256 = Hash(self) });
                var state = new Installation { Directory = root, Version = payload.Version, Files = stateFiles, StartMenu = startMenu, Desktop = desktop };
                WriteNew(Destination(root, StateName), Encoding.UTF8.GetBytes(SerializeState(state)), files);
                if (startMenu) MakeDirectories(StartMenuDirectory, directories);
                foreach (string link in links) {
                    if (File.Exists(link) || Directory.Exists(link)) throw new IOException("快捷方式在安装期间出现，操作停止。");
                    AssertNoReparse(link); files.Add(link);
                    Shortcut(link, Path.Combine(root, link == StartUninstallLink ? Uninstaller : "OpenAC.exe"), link == StartUninstallLink ? "--uninstall" : "");
                }
                AssertNoReparse(root);
                if (RegistryExists()) throw new IOException("安装记录在安装期间出现，操作停止。");
                using (var key = Registry.CurrentUser.CreateSubKey(RegistryPath)) {
                    registryCreated = true; key.SetValue("OpenACProduct", Product); key.SetValue("DisplayName", "OpenAC (Unofficial Preview)"); key.SetValue("DisplayVersion", payload.Version);
                    key.SetValue("Publisher", "OpenAC contributors"); key.SetValue("InstallLocation", root); key.SetValue("DisplayIcon", Path.Combine(root, "OpenAC.exe")); key.SetValue("UninstallString", "\"" + Path.Combine(root, Uninstaller) + "\" --uninstall");
                    key.SetValue("NoModify", 1, RegistryValueKind.DWord); key.SetValue("NoRepair", 1, RegistryValueKind.DWord); key.SetValue("EstimatedSize", (int)Math.Ceiling((payload.Files.Sum(f => f.Bytes) + self.LongLength) / 1024.0), RegistryValueKind.DWord);
                }
            } catch (Exception error) {
                var failures = new List<string>();
                if (registryCreated && RegistryMatches(root)) try { Registry.CurrentUser.DeleteSubKeyTree(RegistryPath, false); } catch { failures.Add("卸载记录"); }
                foreach (string file in files.AsEnumerable().Reverse()) try { AssertNoReparse(file); if (File.Exists(file)) File.Delete(file); } catch { failures.Add(file); }
                foreach (string dir in directories.AsEnumerable().Reverse()) try { AssertNoReparse(dir); if (Directory.Exists(dir)) Directory.Delete(dir, false); } catch { failures.Add(dir); }
                throw new IOException("安装未完成：" + error.Message + (failures.Count == 0 ? "\r\n本次新建文件已清理。" : "\r\n以下本次新建项目未能清理，请检查：\r\n" + string.Join("\r\n", failures)), error);
            }
        }
        internal static List<string> UninstallFiles(Installation state, string runningCopy) {
            using (var guard = new MutationGuard()) return UninstallFilesInternal(state, runningCopy);
        }
        static List<string> UninstallFilesInternal(Installation state, string runningCopy) {
            var fresh = ReadState(state.Directory);
            if (SerializeState(fresh) != SerializeState(state)) throw new IOException("安装记录已改变，请重新打开卸载程序。");
            var selfEntry = state.Files.Single(f => f.Path == Uninstaller);
            var actualSelf = SelfMetadata(Assembly.GetExecutingAssembly().Location);
            if (SelfMetadata(runningCopy).Sha256 != actualSelf.Sha256 || actualSelf.Sha256 != selfEntry.Sha256 || actualSelf.Bytes != selfEntry.Bytes) throw new IOException("卸载程序与内嵌安装记录不一致。");
            AssertNoReparse(state.Directory);
            var retained = new List<string>();
            // Preflight every path before the first deletion. Unknown/modified files are retained.
            var removable = new List<string>();
            foreach (var file in state.Files) {
                string target = Destination(state.Directory, file.Path);
                if (!File.Exists(target)) continue;
                if (new FileInfo(target).Length != file.Bytes || HashFile(target) != file.Sha256) { retained.Add(target + "（内容已修改）"); continue; }
                using (new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.None)) { }
                removable.Add(target);
            }
            if (state.StartMenu) { if (!ShortcutMatches(StartAppLink, Path.Combine(state.Directory, "OpenAC.exe"), "")) retained.Add(StartAppLink + "（已修改）"); if (!ShortcutMatches(StartUninstallLink, Path.Combine(state.Directory, Uninstaller), "--uninstall")) retained.Add(StartUninstallLink + "（已修改）"); }
            if (state.Desktop && !ShortcutMatches(DesktopLink, Path.Combine(state.Directory, "OpenAC.exe"), "")) retained.Add(DesktopLink + "（已修改）");
            foreach (string target in removable) try { AssertNoReparse(target); File.Delete(target); } catch (Exception e) { retained.Add(target + "（" + e.Message + "）"); }
            bool allOwnedRemoved = state.Files.All(f => !File.Exists(Destination(state.Directory, f.Path)));
            if (allOwnedRemoved) {
                if (state.StartMenu) { DeleteMatchingShortcut(StartAppLink, Path.Combine(state.Directory, "OpenAC.exe"), "", retained); DeleteMatchingShortcut(StartUninstallLink, Path.Combine(state.Directory, Uninstaller), "--uninstall", retained); }
                if (state.Desktop) DeleteMatchingShortcut(DesktopLink, Path.Combine(state.Directory, "OpenAC.exe"), "", retained);
                if (RegistryMatches(state.Directory)) Registry.CurrentUser.DeleteSubKeyTree(RegistryPath, false);
                AssertNoReparse(Destination(state.Directory, StateName)); File.Delete(Destination(state.Directory, StateName));
            }
            // Only known empty directories, never recursive deletion.
            var knownDirectories = new List<string> { Path.Combine(state.Directory, "assets", "products"), Path.Combine(state.Directory, "assets"), state.Directory };
            if (state.StartMenu) knownDirectories.Add(StartMenuDirectory);
            foreach (string dir in knownDirectories) {
                AssertNoReparse(dir); if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any()) Directory.Delete(dir, false);
            }
            return retained;
        }
        internal static void DeleteMatchingShortcut(string path, string target, string args, List<string> retained) {
            try { if (File.Exists(path) && ShortcutMatches(path, target, args)) { AssertNoReparse(path); File.Delete(path); } }
            catch (Exception e) { retained.Add(path + "（" + e.Message + "）"); }
        }
    }
}
