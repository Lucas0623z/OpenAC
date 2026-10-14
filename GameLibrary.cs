using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Microsoft.Win32;

namespace RogControl {
 public sealed class GameEntry {
  public string Id {get;set;} public string Name {get;set;} public string Source {get;set;}
  public string Executable {get;set;} public string SteamId {get;set;} public string Category {get;set;}
  public bool Favorite {get;set;} public DateTime LastPlayed {get;set;}
 }
 public static class GameLibraryRules {
  public const int MaxEntries=2000;
  public static bool AbsoluteExecutable(string path) {
   if(String.IsNullOrWhiteSpace(path)||path.Length>32767||!path.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)||!Regex.IsMatch(path,@"^(?:[A-Za-z]:[\\/]|\\\\[^\\/]+[\\/][^\\/]+[\\/])"))return false;
   try{return Path.IsPathRooted(path)&&Path.GetFullPath(path).EndsWith(".exe",StringComparison.OrdinalIgnoreCase);}catch{return false;}
  }
  public static bool Valid(GameEntry game) {
   return game!=null&&!String.IsNullOrWhiteSpace(game.Id)&&game.Id.Length<=128&&!String.IsNullOrWhiteSpace(game.Name)&&game.Name.Length<=120&&(game.Category==null||game.Category.Length<=60)&&(game.Source==null||game.Source.Length<=40)&&(String.IsNullOrEmpty(game.SteamId)||Regex.IsMatch(game.SteamId,"^[0-9]{1,12}$"))&&(String.IsNullOrEmpty(game.Executable)||AbsoluteExecutable(game.Executable))&&(!String.IsNullOrEmpty(game.SteamId)||!String.IsNullOrEmpty(game.Executable));
  }
  static GameEntry Copy(GameEntry g){return new GameEntry{Id=g.Id,Name=g.Name.Trim(),Source=String.IsNullOrWhiteSpace(g.Source)?"本地":g.Source,Executable=g.Executable,SteamId=g.SteamId,Category=String.IsNullOrWhiteSpace(g.Category)?"未分类":g.Category,Favorite=g.Favorite,LastPlayed=g.LastPlayed};}
  public static List<GameEntry> Loaded(IEnumerable<GameEntry> entries){return (entries??Enumerable.Empty<GameEntry>()).Where(Valid).GroupBy(g=>g.Id,StringComparer.OrdinalIgnoreCase).Select(g=>Copy(g.First())).Take(MaxEntries).ToList();}
  public static List<GameEntry> MergeImport(List<GameEntry> existing,List<GameEntry> imported) {
   if(imported==null||imported.Count>MaxEntries||imported.Any(g=>!Valid(g))||imported.Select(g=>g.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=imported.Count)throw new InvalidDataException("游戏库格式无效：检查条目、名称长度及绝对程序路径。");
   var result=Loaded(existing);var ids=new HashSet<string>(result.Select(g=>g.Id),StringComparer.OrdinalIgnoreCase);foreach(var game in imported)if(ids.Add(game.Id))result.Add(Copy(game));if(result.Count>MaxEntries)throw new InvalidDataException("合并后的游戏库超过 2000 个条目。");return result;
  }
 }
 public static class LocalFeatureStore {
  public static readonly string Folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ROG Control");
  public static T Load<T>(string name,T fallback) {
   string path=Path.Combine(Folder,name);
   try {if(!File.Exists(path)||new FileInfo(path).Length>2097152)return fallback;return new JavaScriptSerializer{MaxJsonLength=2097152}.Deserialize<T>(File.ReadAllText(path,Encoding.UTF8));}
   catch {return fallback;}
  }
  public static void Save<T>(string name,T value) {
   Directory.CreateDirectory(Folder);string path=Path.Combine(Folder,name),temporary=path+".new";
   File.WriteAllText(temporary,new JavaScriptSerializer{MaxJsonLength=2097152}.Serialize(value),new UTF8Encoding(false));
   if(File.Exists(path))File.Replace(temporary,path,path+".bak");else File.Move(temporary,path);
  }
 }
 public static class GameDiscovery {
  public static List<GameEntry> Scan() {
   var found=new List<GameEntry>();var libraries=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   foreach(var view in new[]{RegistryView.Registry32,RegistryView.Registry64}) {
    try {using(var root=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,view))using(var key=root.OpenSubKey(@"SOFTWARE\Valve\Steam")){if(key!=null)AddLibrary(libraries,Convert.ToString(key.GetValue("InstallPath")));}}catch{}
   }
   try {using(var key=Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Valve\Steam")){if(key!=null)AddLibrary(libraries,Convert.ToString(key.GetValue("SteamPath")));}}catch{}
   foreach(string root in libraries.ToArray()) {
    string vdf=Path.Combine(root,@"steamapps\libraryfolders.vdf");
    try {if(File.Exists(vdf)&&new FileInfo(vdf).Length<1048576)foreach(Match match in Regex.Matches(File.ReadAllText(vdf),"\"path\"\\s+\"([^\"]+)\""))AddLibrary(libraries,match.Groups[1].Value.Replace("\\\\","\\"));}catch{}
   }
   foreach(string root in libraries) {
    try {foreach(string manifest in Directory.GetFiles(Path.Combine(root,"steamapps"),"appmanifest_*.acf").Take(1000)) {try {
     if(new FileInfo(manifest).Length>1048576)continue;string vdf=File.ReadAllText(manifest);string id=Vdf(vdf,"appid"),title=Vdf(vdf,"name"),folder=Vdf(vdf,"installdir");
     if(!Regex.IsMatch(id??"","^[0-9]{1,12}$")||String.IsNullOrWhiteSpace(title)||String.IsNullOrWhiteSpace(folder))continue;
     string common=Path.GetFullPath(Path.Combine(root,@"steamapps\common"))+Path.DirectorySeparatorChar;
     string installed=Path.GetFullPath(Path.Combine(common,folder));if(!installed.StartsWith(common,StringComparison.OrdinalIgnoreCase)||!Directory.Exists(installed))continue;
     found.Add(new GameEntry{Id="steam:"+id,Name=title,Source="Steam",SteamId=id,Category="未分类"});
    }catch{}}}catch{}
   }
   string epic=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),@"Epic\EpicGamesLauncher\Data\Manifests");
   try {foreach(string path in Directory.GetFiles(epic,"*.item").Take(1000)) {try {
    if(new FileInfo(path).Length>1048576)continue;var json=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(path));
    string title=Value(json,"DisplayName"),folder=Value(json,"InstallLocation"),exe=Value(json,"LaunchExecutable"),id=Value(json,"AppName");
    if(String.IsNullOrWhiteSpace(id)||String.IsNullOrWhiteSpace(title)||String.IsNullOrWhiteSpace(folder)||String.IsNullOrWhiteSpace(exe))continue;
    string full=Path.GetFullPath(Path.Combine(folder,exe)),root=Path.GetFullPath(folder)+Path.DirectorySeparatorChar;
    if(!full.StartsWith(root,StringComparison.OrdinalIgnoreCase)||!File.Exists(full)||!full.EndsWith(".exe",StringComparison.OrdinalIgnoreCase))continue;
    found.Add(new GameEntry{Id="epic:"+id,Name=title,Source="Epic Games",Executable=full,Category="未分类"});
   }catch{}}}catch{}
   foreach(var game in found)if(game.Name!=null&&game.Name.Length>120)game.Name=game.Name.Substring(0,120);
   return GameLibraryRules.Loaded(found).OrderBy(g=>g.Name).ToList();
  }
  static string Value(IDictionary<string,object> source,string name){object v;return source!=null&&source.TryGetValue(name,out v)?Convert.ToString(v):null;}
  static void AddLibrary(HashSet<string> paths,string path){try{if(!String.IsNullOrWhiteSpace(path)&&Directory.Exists(path))paths.Add(Path.GetFullPath(path));}catch{}}
  static string Vdf(string input,string key){var m=Regex.Match(input,"\""+Regex.Escape(key)+"\"\\s+\"([^\"]*)\"");return m.Success?m.Groups[1].Value:null;}
 }
 public partial class MainWindow {
  List<GameEntry> games=GameLibraryRules.Loaded(LocalFeatureStore.Load("games.json",new List<GameEntry>()));
  string gameSearch="",gameCategory="全部",gameSort="最近玩过的游戏";UniformGrid gameCards;TextBlock gameCount;bool gameCardResizeHooked;string gameCoverRoot;bool gameCoverRootRead;
  void OriginalGameLibraryPage() {
   Header("游戏库","");var controls=new Grid{Margin=new Thickness(34,24,34,28)};controls.ColumnDefinitions.Add(new ColumnDefinition());controls.ColumnDefinitions.Add(new ColumnDefinition());controls.ColumnDefinitions.Add(new ColumnDefinition());controls.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   var categories=new[]{"全部","收藏","未分类"}.Concat(games.Select(g=>g.Category).Where(s=>!String.IsNullOrWhiteSpace(s))).Distinct().ToArray();
   if(!categories.Contains(gameCategory))gameCategory="全部";
   var category=MousePerformanceCombo(categories,Math.Max(0,Array.IndexOf(categories,gameCategory)),!busy,Double.NaN);category.SelectionChanged+=(s,e)=>{gameCategory=Convert.ToString(category.SelectedItem);UpdateGameCards();};
   var sort=MousePerformanceCombo(new[]{"最近玩过的游戏","名称","来源"},Math.Max(0,Array.IndexOf(new[]{"最近玩过的游戏","名称","来源"},gameSort)),!busy,Double.NaN);sort.SelectionChanged+=(s,e)=>{gameSort=Convert.ToString(sort.SelectedItem);UpdateGameCards();};
   var search=FeatureText(gameSearch);search.MaxLength=120;search.ToolTip="搜索游戏";search.TextChanged+=(s,e)=>{gameSearch=search.Text;UpdateGameCards();};
   controls.Children.Add(GameLibraryFilter("类别",category));var b=GameLibraryFilter("排序方式",sort);Grid.SetColumn(b,1);controls.Children.Add(b);search.Margin=new Thickness(12,0,14,0);search.Height=34;search.ToolTip="搜索";Grid.SetColumn(search,2);controls.Children.Add(search);var menu=Button("⋮",OpenGameLibraryMenu);menu.Width=26;menu.Height=34;menu.FontSize=23;menu.Padding=new Thickness(0);menu.Margin=new Thickness(0);menu.BorderThickness=new Thickness(0);menu.Background=Brushes.Transparent;Grid.SetColumn(menu,3);controls.Children.Add(menu);content.Children.Add(controls);PinPageHeader(3,"game-library");
   gameCount=Label("",12,muted);gameCount.Visibility=Visibility.Collapsed;content.Children.Add(gameCount);gameCards=new UniformGrid{Columns=3,MaxWidth=1644,Margin=new Thickness(34,0,34,0),HorizontalAlignment=HorizontalAlignment.Left};content.Children.Add(gameCards);if(!gameCardResizeHooked){gameCardResizeHooked=true;content.SizeChanged+=(s,e)=>{if(page=="游戏库"&&gameCards!=null){double width=Math.Min(1644,Math.Max(300,content.ActualWidth-68));if(Math.Abs(gameCards.Width-width)>0.1||Double.IsNaN(gameCards.Width)){gameCards.Width=width;ResizeGameCards();}}};}UpdateGameCards();
  }
  UIElement GameLibraryFilter(string title,ComboBox combo){var row=new Grid{Margin=new Thickness(0,0,12,0)};row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});row.ColumnDefinitions.Add(new ColumnDefinition());var label=Label(title,15,text);label.Margin=new Thickness(0,0,12,0);label.VerticalAlignment=VerticalAlignment.Center;row.Children.Add(label);combo.HorizontalAlignment=HorizontalAlignment.Stretch;Grid.SetColumn(combo,1);row.Children.Add(combo);return row;}
  void OpenGameLibraryMenu(){if(render||closing||busy)return;var menu=new ContextMenu();foreach(string command in new[]{"搜索并列出游戏","手动添加","导入","导出"}){string selected=command;var item=new MenuItem{Header=command,IsEnabled=!busy&&!closing&&!render};item.Click+=async(s,e)=>{if(render||busy||closing)return;if(selected=="搜索并列出游戏")await ScanOriginalGames();else if(selected=="手动添加")AddOriginalGame();else if(selected=="导入")ImportOriginalGames();else ExportOriginalGames();};menu.Items.Add(item);}menu.IsOpen=true;}
  void ResizeGameCards(){if(gameCards==null)return;double width=Double.IsNaN(gameCards.Width)?Math.Min(1644,Math.Max(300,content.ActualWidth-68)):gameCards.Width;foreach(Border card in gameCards.Children.OfType<Border>())card.Height=Math.Max(100,(width/3-12)*9/16);}
  string GameLibraryCover(GameEntry game){if(render||game==null||!Regex.IsMatch(game.SteamId??"","^[0-9]{1,12}$"))return "";try{if(!gameCoverRootRead){gameCoverRootRead=true;string steam=null;using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))if(key!=null)steam=key.GetValue("SteamPath") as string;if(!String.IsNullOrWhiteSpace(steam)&&Path.IsPathRooted(steam))gameCoverRoot=Path.Combine(Path.GetFullPath(steam),"appcache","librarycache");}if(String.IsNullOrEmpty(gameCoverRoot))return "";string cache=gameCoverRoot;foreach(string suffix in new[]{"_header.jpg","_library_hero.jpg"}){string path=Path.Combine(cache,game.SteamId+suffix);if(File.Exists(path))return path;}}catch{}return "";}
  ComboBox FeatureCombo(IEnumerable<string> values,string selectedValue) {var box=new ComboBox{Foreground=Brush("#151515"),Background=Brush("#F1F1F1"),Padding=new Thickness(9,7,9,7),Margin=new Thickness(0,0,14,0),MinWidth=130};foreach(string value in values)box.Items.Add(value);box.SelectedItem=selectedValue;if(box.SelectedIndex<0&&box.Items.Count>0)box.SelectedIndex=0;return box;}
  TextBox FeatureText(string value) {return new TextBox{Text=value??"",Foreground=text,Background=panel,BorderBrush=border,Padding=new Thickness(9,7,9,7),Margin=new Thickness(0,0,14,0),FontSize=13};}
  StackPanel FeatureField(string title,UIElement element) {var stack=new StackPanel();stack.Children.Add(Label(title,12,muted));stack.Children.Add(element);return stack;}
  void UpdateGameCards() {
   if(gameCards==null)return;gameCards.Children.Clear();IEnumerable<GameEntry> list=games.Where(g=>g!=null&&(g.Name??"").IndexOf(gameSearch??"",StringComparison.OrdinalIgnoreCase)>=0);
   if(gameCategory=="收藏")list=list.Where(g=>g.Favorite);else if(gameCategory!="全部")list=list.Where(g=>g.Category==gameCategory);
   list=gameSort=="名称"?list.OrderBy(g=>g.Name):gameSort=="来源"?list.OrderBy(g=>g.Source).ThenBy(g=>g.Name):list.OrderByDescending(g=>g.LastPlayed).ThenBy(g=>g.Name);var results=list.ToList();gameCount.Text=results.Count+" 个游戏";
   foreach(var entry in results) {
    var game=entry;var art=new Grid{Background=Brush("#151515")};string cover=GameLibraryCover(game);if(cover.Length>0){var image=OriginalInstalledImage(cover,540,304);image.Height=Double.NaN;image.VerticalAlignment=VerticalAlignment.Stretch;art.Children.Add(image);}else{var name=Label(game.Name,22,muted,FontWeights.SemiBold);name.TextWrapping=TextWrapping.Wrap;name.TextAlignment=TextAlignment.Center;name.VerticalAlignment=VerticalAlignment.Center;name.Margin=new Thickness(20);art.Children.Add(name);}var overlay=new Grid{Visibility=Visibility.Collapsed,Background=Brush("#99000000")};var launch=Button("进入游戏",()=>{if(render||closing)return;LaunchOriginalGame(game);});launch.HorizontalAlignment=HorizontalAlignment.Center;launch.VerticalAlignment=VerticalAlignment.Center;launch.IsEnabled=!render&&!busy;overlay.Children.Add(launch);var actions=Row();actions.HorizontalAlignment=HorizontalAlignment.Right;actions.VerticalAlignment=VerticalAlignment.Bottom;var favorite=Button(game.Favorite?"★":"☆",()=>{if(render||busy)return;game.Favorite=!game.Favorite;SaveOriginalGames();UpdateGameCards();});favorite.IsEnabled=!render&&!busy;var edit=Button("⋮",()=>{if(!render&&!busy)EditOriginalGame(game);});edit.IsEnabled=!render&&!busy;actions.Children.Add(favorite);actions.Children.Add(edit);overlay.Children.Add(actions);art.Children.Add(overlay);var caption=new TextBlock{Text=game.Source+"  "+game.Name,Foreground=text,FontSize=14,TextTrimming=TextTrimming.CharacterEllipsis,Margin=new Thickness(12,0,12,9),VerticalAlignment=VerticalAlignment.Bottom};art.Children.Add(caption);var frame=new Border{BorderBrush=border,BorderThickness=new Thickness(0,0,0,1),CornerRadius=new CornerRadius(5,5,0,0),ClipToBounds=true,Margin=new Thickness(0,0,12,12),Child=art,Tag="GameLibrary.Card"};frame.MouseEnter+=(s,e)=>{overlay.Visibility=Visibility.Visible;caption.VerticalAlignment=VerticalAlignment.Top;caption.Margin=new Thickness(12,12,12,0);};frame.MouseLeave+=(s,e)=>{overlay.Visibility=Visibility.Collapsed;caption.VerticalAlignment=VerticalAlignment.Bottom;caption.Margin=new Thickness(12,0,12,9);};gameCards.Children.Add(frame);
   }
   if(results.Count==0){var empty=new StackPanel{Margin=new Thickness(24,60,24,60)};empty.Children.Add(Label("您的游戏库",19,text));var scan=Button("搜索并列出游戏",async()=>await ScanOriginalGames());scan.IsEnabled=!render&&!busy&&!closing;empty.Children.Add(scan);gameCards.Children.Add(empty);}ResizeGameCards();
  }
  void SaveOriginalGames(){try{LocalFeatureStore.Save("games.json",games);}catch(Exception ex){Log("游戏库保存失败："+ex.Message);}}
  async Task ScanOriginalGames() {
   if(busy||closing)return;busy=true;status.Text="正在读取已安装游戏…";
   try {var results=await Task.Run(()=>GameDiscovery.Scan());if(closing)return;int count=0;foreach(var game in results){var existing=games.FirstOrDefault(g=>String.Equals(g.Id,game.Id,StringComparison.OrdinalIgnoreCase));if(existing==null){if(games.Count>=GameLibraryRules.MaxEntries)break;games.Add(game);count++;}else{existing.Source=game.Source;existing.Executable=game.Executable;existing.SteamId=game.SteamId;}}SaveOriginalGames();Log("扫描完成 · 新增 "+count+" 个游戏。");}
   catch(Exception ex){Log("扫描失败："+ex.Message);}finally{busy=false;if(!closing)ShowPage(page);}
  }
  void AddOriginalGame() {
   var dialog=new OpenFileDialog{Filter="游戏程序 (*.exe)|*.exe",Title="选择游戏程序"};if(dialog.ShowDialog(this)!=true)return;
   if(games.Count>=GameLibraryRules.MaxEntries){Log("游戏库最多 2000 个条目。");return;}
   string path=Path.GetFullPath(dialog.FileName);if(games.Any(g=>String.Equals(g.Executable,path,StringComparison.OrdinalIgnoreCase))){Log("此游戏已经在游戏库中。");return;}
   string name=Path.GetFileNameWithoutExtension(path);if(name.Length>120)name=name.Substring(0,120);
   var game=new GameEntry{Id="local:"+Guid.NewGuid().ToString("N"),Name=name,Executable=path,Source="本地",Category="未分类"};games.Add(game);SaveOriginalGames();EditOriginalGame(game);ShowPage(page);
  }
  void EditOriginalGame(GameEntry game) {
   var dialog=new Window{Title="编辑游戏",Owner=this,Width=430,Height=260,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=bg,Foreground=text,ResizeMode=ResizeMode.NoResize};
   var body=new StackPanel{Margin=new Thickness(24)};TextBox name=FeatureText(game.Name),category=FeatureText(game.Category);body.Children.Add(FeatureField("游戏名称",name));body.Children.Add(new Border{Height=12});body.Children.Add(FeatureField("类别",category));
   var row=Row();row.Children.Add(Button("保存",()=>{if(String.IsNullOrWhiteSpace(name.Text))return;game.Name=name.Text.Trim().Substring(0,Math.Min(name.Text.Trim().Length,120));game.Category=String.IsNullOrWhiteSpace(category.Text)?"未分类":category.Text.Trim().Substring(0,Math.Min(category.Text.Trim().Length,60));SaveOriginalGames();dialog.Close();ShowPage(page);},true));row.Children.Add(Button("从游戏库移除",()=>{games.Remove(game);SaveOriginalGames();dialog.Close();ShowPage(page);}));body.Children.Add(row);dialog.Content=body;dialog.ShowDialog();
  }
  void LaunchOriginalGame(GameEntry game) {
   try {if(!String.IsNullOrEmpty(game.SteamId)&&Regex.IsMatch(game.SteamId,"^[0-9]{1,12}$"))Process.Start(new ProcessStartInfo("steam://rungameid/"+game.SteamId){UseShellExecute=true});else{if(!GameLibraryRules.AbsoluteExecutable(game.Executable)||!File.Exists(game.Executable))throw new InvalidOperationException("游戏程序不存在，请重新添加。");Process.Start(new ProcessStartInfo(game.Executable){UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(game.Executable)});}game.LastPlayed=DateTime.Now;SaveOriginalGames();UpdateGameCards();Log("已启动 "+game.Name);}
   catch(Exception ex){Log("启动失败："+ex.Message);}
  }
  void ExportOriginalGames(){var dialog=new SaveFileDialog{Filter="游戏库 (*.json)|*.json",FileName="游戏库.json"};if(dialog.ShowDialog(this)!=true)return;try{File.WriteAllText(dialog.FileName,new JavaScriptSerializer().Serialize(games),new UTF8Encoding(false));Log("游戏库已导出。");}catch(Exception ex){Log(ex.Message);}}
  void ImportOriginalGames(){var dialog=new OpenFileDialog{Filter="游戏库 (*.json)|*.json"};if(dialog.ShowDialog(this)!=true)return;try{if(new FileInfo(dialog.FileName).Length>2097152)throw new InvalidOperationException("文件过大。");var imported=new JavaScriptSerializer{MaxJsonLength=2097152}.Deserialize<List<GameEntry>>(File.ReadAllText(dialog.FileName));var merged=GameLibraryRules.MergeImport(games,imported);LocalFeatureStore.Save("games.json",merged);games=merged;ShowPage(page);Log("游戏库已导入。");}catch(Exception ex){Log("导入失败："+ex.Message);}}
 }
}
