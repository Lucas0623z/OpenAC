// Pure installer validation tests. Never invokes installation, uninstallation,
// registry, shortcuts, hardware, or payload executable entry points.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using RogControlInstaller;

internal static class InstallerTests {
    private const string TestPrograms = @"C:\Users\InstallerTests\AppData\Local\Programs";
    private static readonly string TestTarget = Path.Combine(TestPrograms, "OpenAC");
    private static int passed, failed;

    private sealed class Entry {
        internal string Name;
        internal byte[] Data;
    }
    private sealed class Fixture {
        internal string Metadata;
        internal List<Entry> Entries;
        internal Payload Trusted;
        internal PayloadFile Self;
    }

    public static int Main() {
        Test("public product identity", delegate {
            Check(InstallerCore.Product == "org.openac.community.preview", "Unexpected product identity.");
            Check(InstallerCore.RuntimeFiles.Contains("OpenAC.exe") && InstallerCore.RuntimeFiles.Contains("OpenAC.exe.config"), "Missing public runtime executable names.");
            Check(!InstallerCore.RuntimeFiles.Contains("RogControl.exe"), "Legacy executable is still whitelisted.");
            Check(InstallerCore.DefaultDirectory == Path.Combine(InstallerCore.Programs, "OpenAC"), "Unexpected default directory.");
        });
        TestPaths();
        Fixture fixture = CreateFixture();
        TestPayload(fixture);
        TestState(fixture);
        Console.WriteLine("Installer pure validation tests: {0} passed, {1} failed. All payload and ownership-state data stayed in memory.", passed, failed);
        return failed == 0 ? 0 : 1;
    }

    private static void Test(string label, Action action) {
        try { action(); passed++; }
        catch (Exception error) { failed++; Console.Error.WriteLine("FAIL {0}: {1}: {2}", label, error.GetType().Name, error.Message); }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Reject(Action action) {
        try { action(); }
        catch (InvalidDataException) { return; }
        throw new Exception("Expected a validation rejection.");
    }
    private static void RejectPath(Action action) {
        try { action(); }
        catch (InvalidDataException) { return; }
        catch (ArgumentException) { return; } // Framework path APIs reject illegal filename characters.
        throw new Exception("Expected a path validation rejection.");
    }
    private static Dictionary<string, object> Decode(string json) { return (Dictionary<string, object>)InstallerCore.Json.DeserializeObject(json); }
    private static object[] Records(Dictionary<string, object> metadata) { return (object[])metadata["files"]; }
    private static Dictionary<string, object> Record(Dictionary<string, object> metadata, int index) { return (Dictionary<string, object>)Records(metadata)[index]; }

    private static void TestPaths() {
        Test("relative nested whitelist path", delegate { Check(InstallerCore.Relative("assets/products/manifest.json") == "assets/products/manifest.json", "Relative path changed."); });
        string[] badRelative = {
            "", "/OpenAC.exe", "../OpenAC.exe", "assets/../OpenAC.exe", "assets/./OpenAC.exe",
            "assets//OpenAC.exe", "assets/", @"assets\OpenAC.exe", "OpenAC.exe:stream", "C:/OpenAC.exe",
            "OpenAC.exe.", "OpenAC.exe ", "assets./OpenAC.exe", "assets /OpenAC.exe",
            "OpenAC\n.exe", "OpenAC\t.exe", "bad<name", "bad>name", "bad|name", "bad?name", "bad*name", "bad\"name",
            "CON", "NUL.txt", "aux.exe", "PRN", "COM1.exe", "LPT9.txt", "assets/NUL/file.json",
            new string('a', 181)
        };
        foreach (string value in badRelative) {
            string path = value;
            Test("reject relative path " + Escape(path), delegate { Reject(delegate { InstallerCore.Relative(path); }); });
        }
        Test("reject null relative path", delegate { Reject(delegate { InstallerCore.Relative(null); }); });

        Test("canonical absolute directory", delegate { Check(InstallerCore.Canonical(TestTarget) == TestTarget, "Canonical path changed."); });
        Test("target exactly OpenAC", delegate { Check(InstallerCore.ValidateTarget(TestTarget, TestPrograms) == TestTarget, "Target changed."); });
        Test("target case insensitive Windows comparison", delegate { Check(string.Equals(InstallerCore.ValidateTarget(TestTarget.ToUpperInvariant(), TestPrograms), TestTarget, StringComparison.OrdinalIgnoreCase), "Target case handling failed."); });
        Test("target normalizes final separator", delegate { Check(InstallerCore.ValidateTarget(TestTarget + "\\", TestPrograms) == TestTarget, "Final separator not normalized."); });
        string[] badCanonical = {
            "OpenAC", @"C:OpenAC", "C:/Programs/OpenAC", @"\\server\share\OpenAC", @"\\?\C:\Programs\OpenAC",
            TestPrograms + @"\..\OpenAC", TestPrograms + @"\.\OpenAC", TestPrograms + @"\\OpenAC",
            TestTarget + ".", TestTarget + " ", TestTarget + ":stream", TestTarget + "\"", TestTarget + "\n",
            TestPrograms + @"\CON", TestPrograms + @"\nul.txt", TestPrograms + @"\LPT1", TestPrograms + @"\bad*name",
            "C:\\" + new string('a', 221)
        };
        foreach (string value in badCanonical) {
            string path = value;
            Test("reject noncanonical directory " + Escape(path), delegate { RejectPath(delegate { InstallerCore.Canonical(path); }); });
        }
        string[] wrongTargets = {
            TestPrograms, Path.Combine(TestPrograms, "Other"), Path.Combine(TestPrograms, "ROG Control"),
            Path.Combine(TestTarget, "child"), TestPrograms + @"Suffix\OpenAC", @"D:\Programs\OpenAC", @"C:\Users\OpenAC"
        };
        foreach (string value in wrongTargets) {
            string path = value;
            Test("reject nondefault target " + Escape(path), delegate { Reject(delegate { InstallerCore.ValidateTarget(path, TestPrograms); }); });
        }
        Test("programs argument also canonical", delegate { Reject(delegate { InstallerCore.ValidateTarget(TestTarget, TestPrograms + @"\."); }); });
        Test("within rejects prefix collision", delegate { Check(!InstallerCore.Within(TestPrograms + @"Suffix\OpenAC", TestPrograms), "Prefix collision counted as a child."); });
    }

    private static Fixture CreateFixture() {
        var entries = InstallerCore.RuntimeFiles.Select(path => new Entry {
            Name = path,
            Data = Encoding.UTF8.GetBytes(path == "LICENSE" ? "MIT License\nSynthetic test fixture only.\n" : "fixture:" + path)
        }).ToList();
        var files = entries.Select(entry => new PayloadFile { Path = entry.Name, Bytes = entry.Data.LongLength, Sha256 = InstallerCore.Hash(entry.Data) }).ToList();
        var fixture = new Fixture {
            Entries = entries,
            Metadata = InstallerCore.Json.Serialize(new { schemaVersion = 1, product = InstallerCore.Product, version = "0.1.0", files = InstallerCore.FileRecords(files) }),
            Self = new PayloadFile { Path = InstallerCore.Uninstaller, Data = Encoding.UTF8.GetBytes("trusted synthetic setup bytes") }
        };
        fixture.Self.Bytes = fixture.Self.Data.LongLength;
        fixture.Self.Sha256 = InstallerCore.Hash(fixture.Self.Data);
        fixture.Trusted = ReadFixture(fixture, null, null);
        return fixture;
    }

    private static Payload ReadFixture(Fixture fixture, Action<Dictionary<string, object>> changeMetadata, Action<List<Entry>> changeArchive) {
        var metadata = Decode(fixture.Metadata);
        if (changeMetadata != null) changeMetadata(metadata);
        var entries = fixture.Entries.Select(entry => new Entry { Name = entry.Name, Data = (byte[])entry.Data.Clone() }).ToList();
        if (changeArchive != null) changeArchive(entries);
        using (var archive = new MemoryStream()) {
            using (var zip = new ZipArchive(archive, ZipArchiveMode.Create, true)) {
                foreach (var entry in entries) {
                    using (var output = zip.CreateEntry(entry.Name).Open()) output.Write(entry.Data, 0, entry.Data.Length);
                }
            }
            archive.Position = 0;
            using (var source = new MemoryStream(Encoding.UTF8.GetBytes(InstallerCore.Json.Serialize(metadata)))) return InstallerCore.ReadPayload(source, archive);
        }
    }
    private static void RejectPayload(Fixture fixture, Action<Dictionary<string, object>> changeMetadata, Action<List<Entry>> changeArchive) { Reject(delegate { ReadFixture(fixture, changeMetadata, changeArchive); }); }

    private static void TestPayload(Fixture fixture) {
        Test("valid complete memory ZIP", delegate {
            Payload payload = ReadFixture(fixture, null, null);
            Check(payload.Version == "0.1.0" && payload.Files.Count == InstallerCore.RuntimeFiles.Length, "Payload identity or count differs.");
            foreach (PayloadFile file in payload.Files) Check(file.Data.SequenceEqual(fixture.Entries.Single(entry => entry.Name == file.Path).Data), "Payload bytes differ: " + file.Path);
            Check(payload.License.Contains("MIT License"), "License was not loaded.");
        });
        Test("archive order independent", delegate { Check(ReadFixture(fixture, null, delegate(List<Entry> entries) { entries.Reverse(); }).Files.Count == fixture.Entries.Count, "Reverse archive failed."); });
        Test("manifest order independent", delegate { Check(ReadFixture(fixture, delegate(Dictionary<string, object> metadata) { metadata["files"] = Records(metadata).Reverse().ToArray(); }, null).Files.Count == fixture.Entries.Count, "Reverse manifest failed."); });
        Test("reject wrong product", delegate { RejectPayload(fixture, delegate(Dictionary<string, object> metadata) { metadata["product"] = "org.example.other"; }, null); });
        Test("reject legacy product", delegate { RejectPayload(fixture, delegate(Dictionary<string, object> metadata) { metadata["product"] = "org.rogcontrol.community.preview"; }, null); });
        Test("reject unsupported schema", delegate { RejectPayload(fixture, delegate(Dictionary<string, object> metadata) { metadata["schemaVersion"] = 2; }, null); });
        Test("reject schema string", delegate { RejectPayload(fixture, delegate(Dictionary<string, object> metadata) { metadata["schemaVersion"] = "1"; }, null); });
        Test("reject extra metadata field", delegate { RejectPayload(fixture, delegate(Dictionary<string, object> metadata) { metadata["extra"] = true; }, null); });
        Test("reject missing metadata field", delegate { RejectPayload(fixture, delegate(Dictionary<string, object> metadata) { metadata.Remove("version"); }, null); });
        Test("reject incomplete version", delegate { RejectPayload(fixture, delegate(Dictionary<string, object> metadata) { metadata["version"] = "0.1"; }, null); });
        Test("reject malformed version", delegate { RejectPayload(fixture, delegate(Dictionary<string, object> metadata) { metadata["version"] = "preview"; }, null); });
        Test("reject missing whitelist item", delegate { RejectPayload(fixture, delegate(Dictionary<string, object> metadata) { metadata["files"] = Records(metadata).Skip(1).ToArray(); }, null); });
        Test("reject extra whitelist item", delegate { RejectPayload(fixture, delegate(Dictionary<string, object> metadata) { metadata["files"] = Records(metadata).Concat(new[] { Records(metadata)[0] }).ToArray(); }, null); });
        Test("reject duplicate whitelist item at exact count", delegate { RejectPayload(fixture, delegate(Dictionary<string, object> metadata) { Records(metadata)[1] = Records(metadata)[0]; }, null); });
        Test("reject unknown manifest path", delegate { RejectPayload(fixture, delegate(Dictionary<string, object> metadata) { Record(metadata, 0)["path"] = "unknown.exe"; }, null); });
        Test("reject case variant manifest path", delegate { RejectPayload(fixture, delegate(Dictionary<string, object> metadata) { Record(metadata, 0)["path"] = ((string)Record(metadata, 0)["path"]).ToUpperInvariant(); }, null); });
        Test("reject uninstaller in runtime manifest", delegate { RejectPayload(fixture, delegate(Dictionary<string, object> metadata) { Record(metadata, 0)["path"] = InstallerCore.Uninstaller; }, null); });
        Test("reject extra file record field", delegate { RejectPayload(fixture, delegate(Dictionary<string, object> metadata) { Record(metadata, 0)["extra"] = 1; }, null); });
        Test("reject missing file record field", delegate { RejectPayload(fixture, delegate(Dictionary<string, object> metadata) { Record(metadata, 0).Remove("sha256"); }, null); });
        Test("reject negative file length", delegate { RejectPayload(fixture, delegate(Dictionary<string, object> metadata) { Record(metadata, 0)["bytes"] = -1; }, null); });
        Test("reject excessive file length", delegate { RejectPayload(fixture, delegate(Dictionary<string, object> metadata) { Record(metadata, 0)["bytes"] = (long)InstallerCore.MaximumBytes + 1; }, null); });
        Test("reject fractional file length", delegate { RejectPayload(fixture, delegate(Dictionary<string, object> metadata) { Record(metadata, 0)["bytes"] = 1.25; }, null); });
        Test("reject file length string", delegate { RejectPayload(fixture, delegate(Dictionary<string, object> metadata) { Record(metadata, 0)["bytes"] = "1"; }, null); });
        Test("reject short hash", delegate { RejectPayload(fixture, delegate(Dictionary<string, object> metadata) { Record(metadata, 0)["sha256"] = new string('a', 63); }, null); });
        Test("reject nonhex hash", delegate { RejectPayload(fixture, delegate(Dictionary<string, object> metadata) { Record(metadata, 0)["sha256"] = new string('z', 64); }, null); });
        Test("reject uppercase hash", delegate { RejectPayload(fixture, delegate(Dictionary<string, object> metadata) { Record(metadata, 0)["sha256"] = new string('A', 64); }, null); });
        Test("reject total payload bound", delegate { RejectPayload(fixture, delegate(Dictionary<string, object> metadata) { Record(metadata, 0)["bytes"] = InstallerCore.MaximumBytes; Record(metadata, 1)["bytes"] = 1; }, null); });
        Test("reject missing archive entry", delegate { RejectPayload(fixture, null, delegate(List<Entry> entries) { entries.RemoveAt(0); }); });
        Test("reject extra archive entry", delegate { RejectPayload(fixture, null, delegate(List<Entry> entries) { entries.Add(new Entry { Name = "extra.exe", Data = new byte[0] }); }); });
        Test("reject duplicate archive entry at exact count", delegate { RejectPayload(fixture, null, delegate(List<Entry> entries) { entries[1] = entries[0]; }); });
        Test("reject unknown archive entry at exact count", delegate { RejectPayload(fixture, null, delegate(List<Entry> entries) { entries[0].Name = "unknown.exe"; }); });
        Test("reject archive filename case change", delegate { RejectPayload(fixture, null, delegate(List<Entry> entries) { entries[0].Name = entries[0].Name.ToUpperInvariant(); }); });
        string[] archiveNames = { "../OpenAC.exe", "assets/../OpenAC.exe", "/OpenAC.exe", @"assets\OpenAC.exe", "OpenAC.exe:stream", "assets/" };
        foreach (string value in archiveNames) {
            string path = value;
            Test("reject archive path " + Escape(path), delegate { RejectPayload(fixture, null, delegate(List<Entry> entries) { entries[0].Name = path; }); });
        }
        Test("reject declared archive length mismatch", delegate { RejectPayload(fixture, delegate(Dictionary<string, object> metadata) { Record(metadata, 0)["bytes"] = Convert.ToInt64(Record(metadata, 0)["bytes"]) + 1; }, null); });
        Test("reject payload bytes changed at same length", delegate { RejectPayload(fixture, null, delegate(List<Entry> entries) { entries[0].Data[0] ^= 1; }); });
        Test("reject declared hash mismatch", delegate { RejectPayload(fixture, delegate(Dictionary<string, object> metadata) { Record(metadata, 0)["sha256"] = new string('0', 64); }, null); });
        Test("reject missing reviewed license with matching hash", delegate {
            byte[] license = Encoding.UTF8.GetBytes("Unreviewed license fixture");
            RejectPayload(fixture, delegate(Dictionary<string, object> metadata) {
                var item = Records(metadata).Cast<Dictionary<string, object>>().Single(record => (string)record["path"] == "LICENSE");
                item["bytes"] = license.Length; item["sha256"] = InstallerCore.Hash(license);
            }, delegate(List<Entry> entries) { entries.Single(entry => entry.Name == "LICENSE").Data = license; });
        });
    }

    private static string State(Fixture fixture, Action<Dictionary<string, object>> change) {
        var files = fixture.Trusted.Files.Select(file => new PayloadFile { Path = file.Path, Bytes = file.Bytes, Sha256 = file.Sha256 }).ToList();
        files.Add(new PayloadFile { Path = fixture.Self.Path, Bytes = fixture.Self.Bytes, Sha256 = fixture.Self.Sha256 });
        string raw = InstallerCore.SerializeState(new Installation { Directory = TestTarget, Version = fixture.Trusted.Version, Files = files, StartMenu = true, Desktop = false });
        var metadata = Decode(raw);
        if (change != null) change(metadata);
        return InstallerCore.Json.Serialize(metadata);
    }
    private static Installation ParseState(Fixture fixture, Action<Dictionary<string, object>> change) { return InstallerCore.ParseState(State(fixture, change), TestTarget, TestPrograms, fixture.Trusted, fixture.Self); }
    private static void RejectState(Fixture fixture, Action<Dictionary<string, object>> change) { Reject(delegate { ParseState(fixture, change); }); }

    private static void TestState(Fixture fixture) {
        Test("valid state binds runtime and self metadata", delegate {
            Installation state = ParseState(fixture, null);
            Check(state.Directory == TestTarget && state.Version == fixture.Trusted.Version && state.StartMenu && !state.Desktop, "State values differ.");
            Check(state.Files.Count == fixture.Trusted.Files.Count + 1, "Owned count differs.");
        });
        Test("state file record order independent", delegate { Check(ParseState(fixture, delegate(Dictionary<string, object> metadata) { metadata["files"] = Records(metadata).Reverse().ToArray(); }).Files.Count == fixture.Trusted.Files.Count + 1, "Reordered valid records rejected."); });
        Test("state install directory case comparison", delegate { Check(string.Equals(ParseState(fixture, delegate(Dictionary<string, object> metadata) { metadata["installDirectory"] = TestTarget.ToUpperInvariant(); }).Directory, TestTarget, StringComparison.OrdinalIgnoreCase), "State directory case comparison failed."); });
        Test("reject state legacy identity", delegate { RejectState(fixture, delegate(Dictionary<string, object> metadata) { metadata["product"] = "org.rogcontrol.community.preview"; }); });
        Test("reject state schema", delegate { RejectState(fixture, delegate(Dictionary<string, object> metadata) { metadata["schemaVersion"] = 2; }); });
        Test("reject state version differs from trusted payload", delegate { RejectState(fixture, delegate(Dictionary<string, object> metadata) { metadata["version"] = "0.2.0"; }); });
        Test("reject state extra field", delegate { RejectState(fixture, delegate(Dictionary<string, object> metadata) { metadata["extra"] = true; }); });
        Test("reject state missing field", delegate { RejectState(fixture, delegate(Dictionary<string, object> metadata) { metadata.Remove("desktop"); }); });
        Test("reject state shortcut flag string", delegate { RejectState(fixture, delegate(Dictionary<string, object> metadata) { metadata["startMenu"] = "true"; }); });
        Test("reject state shortcut flag number", delegate { RejectState(fixture, delegate(Dictionary<string, object> metadata) { metadata["desktop"] = 1; }); });
        Test("reject state nondefault child directory", delegate { RejectState(fixture, delegate(Dictionary<string, object> metadata) { metadata["installDirectory"] = Path.Combine(TestTarget, "child"); }); });
        Test("reject state nondefault sibling directory", delegate { RejectState(fixture, delegate(Dictionary<string, object> metadata) { metadata["installDirectory"] = Path.Combine(TestPrograms, "Other"); }); });
        Test("reject parser caller nondefault directory", delegate { Reject(delegate { InstallerCore.ParseState(State(fixture, null), Path.Combine(TestPrograms, "Other"), TestPrograms, fixture.Trusted, fixture.Self); }); });
        Test("reject missing owned runtime file", delegate { RejectState(fixture, delegate(Dictionary<string, object> metadata) { metadata["files"] = Records(metadata).Skip(1).ToArray(); }); });
        Test("reject missing owned uninstaller", delegate { RejectState(fixture, delegate(Dictionary<string, object> metadata) { metadata["files"] = Records(metadata).Take(fixture.Trusted.Files.Count).ToArray(); }); });
        Test("reject duplicate owned path at exact count", delegate { RejectState(fixture, delegate(Dictionary<string, object> metadata) { Records(metadata)[1] = Records(metadata)[0]; }); });
        Test("reject unknown owned path", delegate { RejectState(fixture, delegate(Dictionary<string, object> metadata) { Record(metadata, 0)["path"] = "user-settings.json"; }); });
        Test("reject changed runtime hash despite plausible state", delegate { RejectState(fixture, delegate(Dictionary<string, object> metadata) { Record(metadata, 0)["sha256"] = InstallerCore.Hash(Encoding.UTF8.GetBytes("attacker-chosen replacement")); }); });
        Test("reject changed runtime length despite trusted hash", delegate { RejectState(fixture, delegate(Dictionary<string, object> metadata) { Record(metadata, 0)["bytes"] = Convert.ToInt64(Record(metadata, 0)["bytes"]) + 1; }); });
        Test("reject swapped valid runtime hashes", delegate { RejectState(fixture, delegate(Dictionary<string, object> metadata) { object first = Record(metadata, 0)["sha256"]; Record(metadata, 0)["sha256"] = Record(metadata, 1)["sha256"]; Record(metadata, 1)["sha256"] = first; }); });
        Test("reject altered uninstaller hash", delegate { RejectState(fixture, delegate(Dictionary<string, object> metadata) { Record(metadata, fixture.Trusted.Files.Count)["sha256"] = InstallerCore.Hash(Encoding.UTF8.GetBytes("untrusted setup")); }); });
        Test("reject altered uninstaller length", delegate { RejectState(fixture, delegate(Dictionary<string, object> metadata) { Record(metadata, fixture.Trusted.Files.Count)["bytes"] = fixture.Self.Bytes + 1; }); });
        Test("reject traversal in owned path", delegate { RejectState(fixture, delegate(Dictionary<string, object> metadata) { Record(metadata, 0)["path"] = "../OpenAC.exe"; }); });
        Test("reject ownership of state file itself", delegate { RejectState(fixture, delegate(Dictionary<string, object> metadata) { Record(metadata, 0)["path"] = InstallerCore.StateName; }); });
    }

    private static string Escape(string value) { return value.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t"); }
}
