// Owned, bounded reader for ASUS public firmware-tool manifests. No flashing or downloads.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;

internal static class FirmwareCatalog
{
    internal static Dictionary<string,object> Read(string kind)
    {
        int pid=kind=="mouse"?7548:kind=="keyboard"?7588:kind=="headset"?7489:0;
        if(pid==0)throw new ArgumentException("No verified firmware catalog exists for this device kind.");
        var uri=new Uri("https://gearlink.asus.com/firmware-update-tools/"+pid+"/windows/manifest.json");
        // Legacy .NET's HTTPS stack fails negotiation on this machine. Windows curl
        // verifies certificates and receives only this fixed official URL.
        string executable=Path.Combine(Environment.SystemDirectory,"curl.exe");if(!File.Exists(executable))throw new IOException("Windows HTTPS reader is unavailable.");
        var start=new ProcessStartInfo(executable,"--disable --silent --show-error --fail --proto =https --max-time 8 --connect-timeout 3 --max-filesize 65536 --dump-header - "+uri.AbsoluteUri){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
        string output;using(var process=Process.Start(start)) {
            var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
            if(!process.WaitForExit(9500)){try{process.Kill();}catch{}throw new TimeoutException("Official firmware manifest timed out.");}
            output=stdout.GetAwaiter().GetResult();stderr.GetAwaiter().GetResult();if(process.ExitCode!=0)throw new IOException("Official firmware manifest could not be read (HTTPS reader code "+process.ExitCode+").");
        }
        if(output.Length>131072)throw new IOException("Official firmware manifest response is too large.");
        int bodyOffset=0;string headers="";
        do {int end=output.IndexOf("\r\n\r\n",bodyOffset,StringComparison.Ordinal);if(end<0)throw new IOException("Unexpected official HTTPS response headers.");headers=output.Substring(bodyOffset,end-bodyOffset);bodyOffset=end+4;}while(output.IndexOf("HTTP/",bodyOffset,StringComparison.Ordinal)==bodyOffset);
        string status=headers.Split(new string[]{"\r\n"},StringSplitOptions.None)[0];if(!status.StartsWith("HTTP/",StringComparison.Ordinal)||status.Split(' ').Length<2||status.Split(' ')[1]!="200")throw new IOException("Official firmware manifest did not return HTTP 200.");
        string body=output.Substring(bodyOffset);byte[] bytes=Encoding.UTF8.GetBytes(body);if(bytes.Length>65536)throw new IOException("Official firmware manifest is too large.");
        string hash;using(var digest=SHA256.Create())hash=BitConverter.ToString(digest.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
        var manifest=GearLinkWorker.Json.Deserialize<Dictionary<string,object>>(body);
        if(GearLinkWorker.Text(manifest,"pid")!=pid.ToString())throw new IOException("Firmware manifest model does not match the requested device.");
        var toolUri=ToolUri(uri,pid,GearLinkWorker.Text(manifest,"toolPath"));
        DateTime lastModified;object modified=DateTime.TryParse(Header(headers,"Last-Modified"),out lastModified)?(object)lastModified.ToUniversalTime().ToString("o"):null;
        var result=GearLinkWorker.Obj("kind",kind,"model",GearLinkWorker.Model(kind),"pid",pid,"sourceUrl",uri.AbsoluteUri,"fetchedAtUtc",DateTime.UtcNow.ToString("o"),"lastModifiedUtc",modified,"etag",Header(headers,"ETag"),"sha256",hash,"toolUrl",toolUri.AbsoluteUri,"signatureVerification","manifest_does_not_provide_signature","updateToolDownloaded",false,"hardwareAccess",false);
        foreach(string key in new string[]{"toolVersion","deviceFWVersion","dongleFWVersion","boosterFWVersion"}){string value=GearLinkWorker.Text(manifest,key);if(value.Length>64)throw new IOException("Unexpected firmware version field.");result[key]=value.Length==0?null:(object)value;}
        return result;
    }
    internal static Uri ToolUri(Uri origin,int pid,string path)
    {
        if(!path.StartsWith("./",StringComparison.Ordinal)||path.Contains("..")||path.Contains("\\"))throw new IOException("Unexpected update-tool path.");
        var toolUri=new Uri(origin,path);if(toolUri.Scheme!="https"||toolUri.Host!="gearlink.asus.com"||!toolUri.AbsolutePath.StartsWith("/firmware-update-tools/"+pid+"/windows/",StringComparison.Ordinal)||!toolUri.AbsolutePath.EndsWith(".zip",StringComparison.OrdinalIgnoreCase))throw new IOException("Update-tool URL is outside the verified official origin.");return toolUri;
    }
    private static string Header(string headers,string name){foreach(string line in headers.Split(new string[]{"\r\n"},StringSplitOptions.None)){int colon=line.IndexOf(':');if(colon>0&&line.Substring(0,colon).Equals(name,StringComparison.OrdinalIgnoreCase))return line.Substring(colon+1).Trim();}return null;}
}
