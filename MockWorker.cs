using System;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
class MockWorker {
 static void Main(){Console.OutputEncoding=new UTF8Encoding(false);var json=new JavaScriptSerializer();string line;while((line=OpenAc.WorkerTextProtocol.ReadLine())!=null){if(line.Length==0||line[0]!='{')throw new InvalidDataException("Frame first code point: "+(line.Length==0?"empty":((int)line[0]).ToString("X4")));var request=json.Deserialize<Dictionary<string,object>>(line);string op=(string)request["op"];if(op=="exit")Environment.Exit(7);if(op=="hang")Thread.Sleep(5000);object echoed;request.TryGetValue("text",out echoed);Console.WriteLine("native noise should be ignored");Console.WriteLine(json.Serialize(new {id=0,ok=true}));Console.WriteLine(json.Serialize(new{id=request["id"],ok=true,devices=new object[0],controlled=false,echo=echoed}));}}
}
