using System;
using System.Collections.Generic;
using System.IO;
using RogControl;

class LocalUpdateCatalogTests {
 static int Main(){try{
  if(LocalUpdateCatalog.CompareMinimum("3.11.08","3.10.12")!="meets_cached_minimum")throw new Exception("Version comparison failed.");
  if(LocalUpdateCatalog.CompareMinimum("3.9","3.10")!="below_cached_minimum")throw new Exception("Numeric version comparison failed.");
  if(LocalUpdateCatalog.CompareMinimum("1.2","1.2.0")!="meets_cached_minimum")throw new Exception("Zero suffix comparison failed.");
  if(LocalUpdateCatalog.CompareMinimum(null,"1.0")!="unknown"||LocalUpdateCatalog.CompareMinimum("invalid","1.0")!="unknown")throw new Exception("Unknown version was accepted.");
  var catalog=LocalUpdateCatalog.ParseCatalog("<root><Name key='AURA Service'><item key='SDK'><LocalVersion>3.11.08</LocalVersion><ServerVersion>3.10.12</ServerVersion></item></Name><Name key='unknown-model'><item><LocalVersion>99</LocalVersion></item></Name></root>");
  var groups=(object[])catalog["items"];if(groups.Length!=1)throw new Exception("Unknown model leaked into compatibility claims.");
  var rows=(object[])((Dictionary<string,object>)groups[0])["items"];if(rows.Length!=1||Convert.ToString(((Dictionary<string,object>)rows[0])["latestStatus"])!="unknown")throw new Exception("Cache was treated as latest.");
  bool dtdDenied=false;try{LocalUpdateCatalog.ParseCatalog("<!DOCTYPE root [<!ENTITY x SYSTEM 'file:///ignored'>]><root>&x;</root>");}catch{dtdDenied=true;}if(!dtdDenied)throw new Exception("External XML entity was accepted.");
  Console.WriteLine("Local update catalog: 7 synthetic checks passed. No file, registry, network or installation access.");return 0;
 }catch(Exception ex){Console.WriteLine("FAIL: "+ex.Message);return 1;}}
}
