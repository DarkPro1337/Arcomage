using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Arcomage.Scripts.Core;
using Arcomage.Scripts.Logging;
using Godot;

namespace Arcomage.Scripts.Managers;

public record LocaleInfo(string Name, string DisplayName);

public class TranslationManager
{
   private static readonly Logger _Logger = Logger.GetOrCreateLogger("TranslationManager");

   public List<LocaleInfo> LoadedLocales { get; } = [];

   public event Action TranslationChanged;

   public TranslationManager() => UpdateLoadedLocales();

   public void UpdateLoadedLocales()
   {
      LoadedLocales.Clear();
      var loadedLocales = TranslationServer.GetLoadedLocales();
      foreach (var loadedLocale in loadedLocales)
      {
         var culture = new CultureInfo(loadedLocale);
         LoadedLocales.Add(new LocaleInfo(loadedLocale, culture.NativeName.Capitalize()));
      }

      TranslationChanged?.Invoke();
   }

   public int GetLoadedLocaleIndex() => GetLocaleIndex(Config.Settings.CurrentLocale);

   public int GetLocaleIndex(string locale)
   {
      for (var i = 0; i < LoadedLocales.Count; i++)
      {
         if (LoadedLocales[i].Name == locale)
            return i;
      }

      return -1;
   }

   public static IReadOnlyList<Translation> LoadTranslationsFromCsv(string csvText)
   {
      var lines = csvText.Split((char[])['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
      if (lines.Length < 2)
      {
         _Logger.Error("CSV file must contain at least one line.");
         return null;
      }

      var header = lines[0].Split(',').Select(h => h.Trim()).ToArray();
      if (header.Length < 2 || !header[0].Equals("id", StringComparison.OrdinalIgnoreCase))
      {
         GD.PrintErr("CSV header must start with 'id' and contain at least one language column.");
         return null;
      }

      var languages = header.Skip(1).ToArray();
      var translations = languages.ToDictionary(lang => lang, lang => new Translation { Locale = lang });

      foreach (var line in lines.Skip(1))
      {
         var columns = line.Split(',').Select(c => c.Trim()).ToArray();
         if (columns.Length < header.Length)
            continue;
         var key = columns[0];
         for (var i = 1; i < header.Length; i++)
            translations[header[i]].AddMessage(key, columns[i]);
      }

      return translations.Values.ToArray();
   }
}