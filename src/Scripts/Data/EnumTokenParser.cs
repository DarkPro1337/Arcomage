using System.Threading;

namespace Arcomage.Data;

public static class EnumTokenParser
{
   private static readonly Dictionary<Type, Dictionary<string, object>> _cache = new();
   private static readonly Lock _cacheLock = new();

   public static bool TryParseToken<TEnum>(string token, out TEnum value) where TEnum : struct, Enum
   {
      value = default;
      if (string.IsNullOrWhiteSpace(token))
         return false;

      var map = GetTokenMap(typeof(TEnum));
      var key = token.ToLowerInvariant();
      if (!map.TryGetValue(key, out var raw))
      {
         if (!TryGetPluralFallback(map, key, out raw))
            return false;
      }

      value = (TEnum)raw;
      return true;
   }

   private static Dictionary<string, object> GetTokenMap(Type enumType)
   {
      lock (_cacheLock)
      {
         if (_cache.TryGetValue(enumType, out var map))
            return map;

         map = BuildTokenMap(enumType);
         _cache[enumType] = map;
         return map;
      }
   }

   private static Dictionary<string, object> BuildTokenMap(Type enumType)
   {
      var map = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
      var names = Enum.GetNames(enumType);
      foreach (var name in names)
      {
         var value = Enum.Parse(enumType, name);
         map[name.ToLowerInvariant()] = value;
      }

      return map;
   }

   private static bool TryGetPluralFallback(Dictionary<string, object> map, string key, out object value)
   {
      value = null;
      if (key.Length > 1 && key.EndsWith('s'))
      {
         var singular = key[..^1];
         if (map.TryGetValue(singular, out value))
            return true;
      }
      else
      {
         var plural = key + 's';
         if (map.TryGetValue(plural, out value))
            return true;
      }

      return false;
   }
}
