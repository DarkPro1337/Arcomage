#!/usr/bin/env dotnet

var sheets = new (string File, string Url)[]
{
   ("Interface.csv", "https://docs.google.com/spreadsheets/d/e/2PACX-1vQypyad7a5Yp2Yv54YsFIbJJfxc02OduroayIK16Qt-pqiELdC1sq9v04VY8bANPsqajRV6SB9jTDqR/pub?gid=1374382602&single=true&output=csv"),
   ("Cards.csv", "https://docs.google.com/spreadsheets/d/e/2PACX-1vQypyad7a5Yp2Yv54YsFIbJJfxc02OduroayIK16Qt-pqiELdC1sq9v04VY8bANPsqajRV6SB9jTDqR/pub?gid=885174373&single=true&output=csv"),
   ("Online.csv", "https://docs.google.com/spreadsheets/d/e/2PACX-1vQypyad7a5Yp2Yv54YsFIbJJfxc02OduroayIK16Qt-pqiELdC1sq9v04VY8bANPsqajRV6SB9jTDqR/pub?gid=1061693558&single=true&output=csv"),
};

var localesDir = FindLocalesDirectory();
Console.WriteLine($"Updating localization CSVs in {localesDir}");

using var http = new HttpClient();
http.Timeout = TimeSpan.FromSeconds(30);
http.DefaultRequestHeaders.UserAgent.ParseAdd("Arcomage-UpdateLocales");

var failed = 0;
foreach (var (file, url) in sheets)
{
   var path = Path.Combine(localesDir, file);
   try
   {
      var csv = (await http.GetStringAsync(url)).TrimStart('\uFEFF').Replace("\r\n", "\n").Replace('\r', '\n');
      if (!csv.EndsWith('\n'))
         csv += '\n';

      var header = csv.Split('\n', 2, StringSplitOptions.RemoveEmptyEntries);
      if (header.Length == 0 || !header[0].StartsWith("id", StringComparison.OrdinalIgnoreCase))
      {
         Console.Error.WriteLine($"  {file} ... not a CSV (is the sheet published?)");
         failed++;
         continue;
      }

      await File.WriteAllTextAsync(path, csv);
      Console.WriteLine($"  {file} ... {csv.Length} bytes");
   }
   catch (Exception ex)
   {
      Console.Error.WriteLine($"  {file} ... {ex.Message}");
      failed++;
   }
}

return failed == 0 ? 0 : 1;

static string FindLocalesDirectory()
{
   var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
   while (dir != null)
   {
      var locales = Path.Combine(dir.FullName, "src", "Locales");
      if (Directory.Exists(locales))
         return locales;

      dir = dir.Parent;
   }

   throw new DirectoryNotFoundException("Could not find src/Locales. Run from the Arcomage repo.");
}
