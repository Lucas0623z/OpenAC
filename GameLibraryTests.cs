using System;
using System.Collections.Generic;
using System.Linq;
using RogControl;
public static class GameLibraryTests {
 static int checks; static void Check(bool condition,string name){checks++;if(!condition)throw new Exception(name);}
 static GameEntry Entry(string id){return new GameEntry{Id=id,Name="  Game  ",Executable=@"C:\Games\game.exe"};}
 static void Reject(Action action,string name){bool fail=false;try{action();}catch(System.IO.InvalidDataException){fail=true;}Check(fail,name);}
 public static void Main(){
  foreach(var invalid in new[]{null,"","C:game.exe",@"\game.exe","https://example.com/game.exe","cmd.cmd",@"C:\Games\game.exe "})Check(!GameLibraryRules.AbsoluteExecutable(invalid),"absolute rejects "+invalid);
  Check(GameLibraryRules.AbsoluteExecutable(@"C:\Games\Game.EXE"),"absolute drive");Check(GameLibraryRules.AbsoluteExecutable(@"\\server\share\game.exe"),"absolute UNC");
  var clean=GameLibraryRules.Loaded(new[]{null,Entry("id"),Entry("ID"),new GameEntry{Id="bad"}});Check(clean.Count==1,"null and duplicate load");Check(clean[0].Name=="Game"&&clean[0].Category=="未分类"&&clean[0].Source=="本地","default normalization");Check(GameLibraryRules.Loaded(null).Count==0,"null library");
  var existing=new List<GameEntry>{Entry("old")};var fresh=GameLibraryRules.MergeImport(existing,new List<GameEntry>{Entry("new")});Check(fresh.Count==2&&existing.Count==1,"merge isolated");fresh[0].Name="Changed";Check(existing[0].Name=="  Game  ","merge deep copy");
  Reject(()=>GameLibraryRules.MergeImport(existing,null),"null import");Reject(()=>GameLibraryRules.MergeImport(existing,new List<GameEntry>{Entry("same"),Entry("SAME")}),"case duplicate import");
  foreach(var mutation in new Action<GameEntry>[] {g=>g.Name=new string('x',121),g=>g.Category=new string('x',61),g=>g.Source=new string('x',41),g=>g.Id=new string('x',129),g=>g.SteamId="1;run",g=>g.Executable="C:game.exe",g=>{g.Executable=null;g.SteamId=null;}}){var bad=Entry("bad");mutation(bad);Reject(()=>GameLibraryRules.MergeImport(existing,new List<GameEntry>{bad}),"invalid entry");}
  var many=Enumerable.Range(0,2000).Select(i=>Entry("id"+i)).ToList();Reject(()=>GameLibraryRules.MergeImport(many,new List<GameEntry>{Entry("extra")}),"merged cap");Check(many.Count==2000,"failed import unchanged");Check(GameLibraryRules.MergeImport(many,new List<GameEntry>{Entry("ID0")}).Count==2000,"known duplicate skipped");
  var steam=Entry("steam:123");steam.Executable=null;steam.SteamId="123";Check(GameLibraryRules.Valid(steam),"Steam endpoint allowed");
  Console.WriteLine("Game library: "+checks+" checks passed. No discovery, file dialogs or game launch.");
 }
}
