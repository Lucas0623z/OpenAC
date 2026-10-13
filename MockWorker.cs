using System;
using System.Collections.Generic;
using System.Threading;
using System.Web.Script.Serialization;
class MockWorker {
 static void Main(){var json=new JavaScriptSerializer();string line;while((line=Console.ReadLine())!=null){var request=json.Deserialize<Dictionary<string,object>>(line);string op=(string)request["op"];if(op=="exit")Environment.Exit(7);if(op=="hang")Thread.Sleep(5000);Console.WriteLine("native noise should be ignored");Console.WriteLine(json.Serialize(new {id=0,ok=true}));Console.WriteLine(json.Serialize(new{id=request["id"],ok=true,devices=new object[0],controlled=false}));}}
}
