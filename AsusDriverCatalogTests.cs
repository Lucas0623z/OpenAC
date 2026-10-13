// Synthetic catalog/schema/security checks. Default mode performs no network or WMI reads.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using RogControl;

internal static class AsusDriverCatalogTests {
 static readonly JavaScriptSerializer Json=new JavaScriptSerializer{MaxJsonLength=4194304};
 static int checks;
 static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
 static void Reject(Action action,string message){bool rejected=false;try{action();}catch(ArgumentException){rejected=true;}catch(InvalidDataException){rejected=true;}Check(rejected,message);}
 static Dictionary<string,object> Obj(params object[] pairs){var row=new Dictionary<string,object>();for(int i=0;i<pairs.Length;i+=2)row.Add((string)pairs[i],pairs[i+1]);return row;}
 static string Body(object result){return Json.Serialize(Obj("Status","SUCCESS","Message","SUCCESS","Result",result));}
 static Dictionary<string,object> File(string version,string link){return Obj("Version",version,"Title","","Description","First line<br/>Second &amp; line","FileSize","10 MB","ReleaseDate","2026/09/22","IsRelease","1","DownloadUrl",Obj("Global",link),"sha256",new string('A',64),"Sign","","HardwareInfoList",new object[0]);}
 static string Catalog(string model,params object[] files){return Body(Obj("Model",model,"Obj",new object[]{Obj("Name","BIOS","Files",files)}));}
 static int Main(string[] args){try{
  if(args.Length==1&&args[0]=="--read-local"){Console.WriteLine(Json.Serialize(AsusDriverCatalog.ReadLocal()));return 0;}
  if(args.Length==2&&args[0]=="--read-model"){Console.WriteLine(Json.Serialize(AsusDriverCatalog.Read(new AsusCatalogIdentity{Model=args[1],ModelSource="explicitModel",Platform="unverifiedPhysicalDevice"})));return 0;}
  if(args.Length!=0&&!(args.Length==1&&args[0]=="--self-test"))throw new ArgumentException("Unrecognized test option.");
  Run();Console.WriteLine(Json.Serialize(Obj("ok",true,"suite","asus_public_catalog","checks",checks,"networkRequests",0,"wmiQueries",0,"hardwareWrites",0)));return 0;
 }catch(Exception ex){Console.Error.WriteLine(ex.GetBaseException().Message);return 1;}}
 static void Run(){
  string model="SYNTHETIC ASUS MODEL";var request=AsusDriverCatalog.ApiUri("ProductV2/GetPDBIOS",model,123,0);
  Check(request.Host=="rog.asus.com"&&request.Scheme=="https","Official catalog origin");
  Check(request.AbsolutePath=="/support/webapi/ProductV2/GetPDBIOS","Current v2 path");
  Check(request.Query.Contains("model=SYNTHETIC%20ASUS%20MODEL"),"Exact model escaped");
  Check(request.Query.Contains("pdid=123")&&request.Query.Contains("cpu="),"Observed catalog parameters");
  Check(!request.Query.Contains("token")&&!request.Query.Contains("Authorization"),"No credential parameter");
  Check(AsusDriverCatalog.ApiUri("ProductV2/GetPDDrivers",model,123,52).Query.Contains("osid=52"),"Explicit OS id");
  Reject(()=>AsusDriverCatalog.ApiUri("product/Install",model,1,52),"Unknown operation rejected");
  foreach(string invalid in new[]{"",null,"System Product Name","Default string","Unknown","model&cpu=OTHER","model\nOTHER","model\"OTHER",new string('A',161)})Reject(()=>AsusDriverCatalog.ExactModel(invalid),"Invalid model rejected");
  Check(AsusDriverCatalog.ExactModel(" GA402RK ")=="GA402RK","Only surrounding whitespace normalized");
  Check(AsusDriverCatalog.ExactModel("GA402RK_GA402RK")=="GA402RK_GA402RK","No SKU stripping");
  string resolve=Body(Obj("Obj",Obj("model",model,"productId",123)));
  Check(AsusDriverCatalog.ResolveProduct(resolve,model)==123,"Exact product resolution");
  Check(AsusDriverCatalog.ResolveProduct(resolve,model.ToLowerInvariant())==123,"Case-only model equivalence");
  Reject(()=>AsusDriverCatalog.ResolveProduct(resolve,"OTHER MODEL"),"Fuzzy model rejected");
  Reject(()=>AsusDriverCatalog.ResolveProduct(Body(Obj("Obj",Obj("model",model,"productId",0))),model),"No product id rejected");
  Reject(()=>AsusDriverCatalog.ResolveProduct("{\"Status\":\"FAIL\",\"Result\":null}",model),"HTTP success is not API success");
  Reject(()=>AsusDriverCatalog.ResolveProduct(Body(Obj("Obj",null)),model),"Missing identity rejected");
  var os=AsusDriverCatalog.ParseOs(Body(Obj("Obj",new object[]{Obj("Id","52","Name","Windows 11 64-bit"),Obj("Id","45","Name","Windows 10 64-bit")})));
  Check(os.Length==2&&os[0].Id==52&&os[0].Name=="Windows 11 64-bit","OS schema parsed");
  Reject(()=>AsusDriverCatalog.ParseOs(Body(Obj("Obj",new object[]{Obj("Id","52","Name","Windows 11"),Obj("Id","52","Name","Other")}))),"Duplicate OS id rejected");
  Reject(()=>AsusDriverCatalog.ParseOs(Body(Obj("Obj",new object[]{Obj("Id","0","Name","Unknown")}))),"Invalid OS id rejected");
  Reject(()=>AsusDriverCatalog.ParseOs(Body(Obj("Obj",null))),"Incomplete OS list rejected");
  Check(AsusDriverCatalog.DownloadUri("/pub/ASUS/mb/BIOS/SYNTHETIC-2503.zip").AbsoluteUri=="https://dlcdnets.asus.com/pub/ASUS/mb/BIOS/SYNTHETIC-2503.zip","Official relative link resolved");
  Check(AsusDriverCatalog.DownloadUri("https://dlcdn.asus.com/pub/ASUS/nb/GA402RK.zip").Host=="dlcdn.asus.com","Official absolute package link");
  foreach(string invalid in new[]{"https://evil.invalid/pub/ASUS/a.zip","http://dlcdnets.asus.com/pub/ASUS/a.zip","https://dlcdnets.asus.com.evil.invalid/pub/ASUS/a.zip","https://user@dlcdnets.asus.com/pub/ASUS/a.zip","https://dlcdnets.asus.com:8443/pub/ASUS/a.zip","//evil.invalid/a.zip","/pub/ASUS/../other/a.zip","/pub/ASUS/%2e%2e/a.zip","/pub/ASUS/a.zip?token=x","/pub/ASUS/a.zip#fragment","/pub/ASUS/a.ps1","/pub/ASUS/a b.zip","/pub/ASUS/a\\b.zip"})Reject(()=>AsusDriverCatalog.DownloadUri(invalid),"Unaccepted package link rejected");
  var warning=new List<string>();var files=AsusDriverCatalog.ParseFiles(Catalog(model,File("2503","/pub/ASUS/mb/BIOS/SYNTHETIC-2503.zip")),model,true,"2402",warning);
  Check(files.Length==1&&files[0].IsSystemBios,"BIOS catalog structure");
  Check(files[0].Title=="BIOS 2503","Empty BIOS title gets factual label");
  Check(files[0].ExpectedSha256==new string('a',64),"Catalog digest normalized");
  Check(!files[0].DownloadVerified&&!files[0].CatalogSignaturePresent,"Expected digest is not verification");
  Check(files[0].Comparison=="newerAvailable"&&files[0].InstalledVersion=="2402","Numeric BIOS comparison");
  Check(files[0].Description=="First line\nSecond & line","HTML description becomes plain text");
  Check(files[0].HardwareIds.Length==0,"No invented device applicability");
  Check(AsusDriverCatalog.CompareBios("GA402RK","319","GA402RK.318")=="newerAvailable","Exact notebook revision prefix");
  Check(AsusDriverCatalog.CompareBios("RC71L","340","RC71L.340")=="sameVersion","Exact handheld revision prefix");
  Check(AsusDriverCatalog.CompareBios("GA402RK","319","GA402RJ.318")=="unknown","Other model revision not compared");
  Check(AsusDriverCatalog.CompareBios(model,"2402","2503")=="olderThanInstalled","Older catalog version explicit");
  Check(AsusDriverCatalog.CompareBios(model,"0109","0109")=="sameVersion","Leading zeros accepted");
  Check(AsusDriverCatalog.CompareBios(model,"1.00.09","1.00.08")=="unknown","Different version grammar not guessed");
  Check(AsusDriverCatalog.CompareBios(model,"2503",null)=="unknown","No local BIOS is unknown");
  Check(AsusDriverCatalog.CompareBios(model,"2503 beta","2402")=="unknown","Non-numeric revision unknown");
  Reject(()=>AsusDriverCatalog.ParseFiles(Catalog("OTHER MODEL",File("2503","/pub/ASUS/a.zip")),model,true,"2402",new List<string>()),"Catalog model echo must match");
  var broken=File("2503","/pub/ASUS/a.zip");broken["sha256"]="wrong";Reject(()=>AsusDriverCatalog.ParseFiles(Catalog(model,broken),model,true,"2402",new List<string>()),"Malformed expected hash rejected");
  var offsite=File("2503","https://evil.invalid/a.zip");var unsafeWarnings=new List<string>();var unlinked=AsusDriverCatalog.ParseFiles(Catalog(model,offsite),model,true,"2402",unsafeWarnings);
  Check(unlinked[0].DownloadUrl==null&&unsafeWarnings.Count==1,"Unsafe link disabled with explicit warning");
  var driver=AsusDriverCatalog.ParseFiles(Catalog(model,File("2503","/pub/ASUS/a.zip")),model,false,"2402",new List<string>());
  Check(driver[0].Comparison=="installedVersionUnknown"&&driver[0].InstalledVersion==null,"Driver latest state is not invented");
  var identity=AsusDriverCatalog.SelectIdentity("ASUSTeK COMPUTER INC.",model,"ASUS","System Product Name","2402");
  Check(identity.Model==model&&identity.Platform=="motherboard"&&identity.ModelSource=="Win32_BaseBoard.Product","Desktop exact board identity");
  Check(AsusDriverCatalog.SelectIdentity("ASUSTeK COMPUTER INC.","GA402RK","ASUS","ROG Zephyrus G14 GA402RK_GA402RK","GA402RK.319").Model=="GA402RK","Notebook exact board model avoids marketing guess");
  Check(AsusDriverCatalog.SelectIdentity("ASUSTeK COMPUTER INC.","RC71L","ASUS","ROG Ally RC71L_RC71L","RC71L.340").Model=="RC71L","Handheld exact board model");
  Check(AsusDriverCatalog.SelectIdentity("Other","Default string","ASUS","GA402RK","319").ModelSource=="Win32_ComputerSystem.Model","Exact system identity fallback");
  Reject(()=>AsusDriverCatalog.SelectIdentity("Other",model,"Other",model,"2402"),"Non-ASUS hardware rejected");
  Reject(()=>AsusDriverCatalog.SelectIdentity("ASUS","Default string","ASUS","System Product Name","2402"),"Only placeholders rejected");
 }
}
