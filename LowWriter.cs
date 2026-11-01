using System.Reflection;
using System.Collections;
using System.Globalization;
using System.Collections.Generic;
using System.Data;
using System;
using System.Linq;

namespace LowYaml;

public class LowWriter
{
    private readonly InfoSerializer _info;

    public LowWriter(InfoSerializer info)
    {
        _info = info;
    }

    public string Write(object ob, int lvl = 0, Type t = null) =>
        string.Join("\n", WriteLines(ob, lvl, t));

    private static string FormatScalar(object value)
    {
        return value switch
        {
            null => "null",
            string s => s,
            bool b => b ? "true" : "false",
            char c => c.ToString(),
            DateTime dt => dt.ToString("o", CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToString("o", CultureInfo.InvariantCulture),
            DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            TimeOnly t => t.ToString("HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture),
            TimeSpan ts => ts.ToString("c", CultureInfo.InvariantCulture),
            Guid g => g.ToString("D"),
            Uri u => u.ToString(),
            Version v => v.ToString(),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? ""
        };
    }

    private List<string> WriteLines(object ob, int lvl = 0, Type t = null)
    {
        if (ob == null)
            return new List<string>();

        t ??= ob.GetType();

        if (t.GetCustomAttribute<LowIgnore>() != null)
            return new List<string>();

        string ind = new string(' ', lvl * 2);

        // detección de circularidad por IDENTIDAD de referencia (no por GetHashCode)
        if (!_info.ReferenceSet.Add(ob))
            return new List<string> { ind + "null" };

        try
        {
            // LowParse nativo
            if (ob is LowParse lp)
                return new List<string> { ind + lp.ToLow() };

            // LowParse externo
            var externalParser = _info.GetExternalParser(t);
            if (externalParser != null)
            {
                var method = externalParser.GetType().GetMethod("ToLow");
                string low = (string)method.Invoke(externalParser, new[] { ob });
                return new List<string> { ind + low };
            }

            if (ob is char c && c == '\0')
                return new List<string> { ind + "null" };

            // STRING (vacío -> totalmente vacío; multilínea soportado)
            if (t == typeof(string))
            {
                string ss = ((string)ob).Replace("\t", "  ");
                if (ss == "")
                    return new List<string> { ind };
                if (!ss.Contains('\n'))
                    return new List<string> { ind + ss };

                var r = new List<string> { ind + "|" };
                foreach (var line in ss.Replace("\r", "").Split('\n'))
                    r.Add(ind + "  " + line);
                return r;
            }

            if (InfoSerializer.IsPrimitiveOrSpecial(t))
                return new List<string> { ind + FormatScalar(ob) };

            // Array
            if (t.IsArray)
            {
                if (t.GetArrayRank() != 1)
                    throw new Exception("Array multidimensional no soportado");

                var items = ((IEnumerable)ob).Cast<object>().ToList();
                return WriteList(items, t.GetElementType()).AñadirInit(ind);
            }

            // IEnumerable no-string
            if (ob is IEnumerable enume && !(ob is string))
            {
                var itemType = GetEnumerableItemType(enume);
                return WriteList(enume.Cast<object>().ToList(), itemType).AñadirInit(ind);
            }

            // OBJETO
            var result = new List<string>();

            foreach (var comment in t.GetCustomAttributes<LowComment>())
                result.Add($"{ind}# {comment.Msg}");

            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (f.GetCustomAttribute<LowIgnore>() != null)
                    continue;

                string fieldName = f.Name;
                var rename = f.GetCustomAttribute<LowRename>();
                if (rename != null) fieldName = rename.Nombre;

                foreach (var comment in f.GetCustomAttributes<LowComment>())
                    result.Add($"{ind}# {comment.Msg}");

                var ft = f.FieldType;
                var val = f.GetValue(ob);
                bool polymorfismo = val != null && ft != val.GetType();
                Type realType = val?.GetType() ?? ft;

                // NULL
                if (val == null || (ft == typeof(char) && (char)val == '\0'))
                {
                    if (ft == typeof(string))
                        result.Add($"{ind}{fieldName}:");
                    else if (_info.IncludeNulls)
                        result.Add($"{ind}{fieldName}: null");
                    continue;
                }

                // Polimorfismo con primitivos
                if (polymorfismo && InfoSerializer.IsPrimitiveOrSpecial(realType))
                {
                    result.Add($"{ind}{fieldName}: !{realType.FullName} {FormatScalar(val)}");
                }
                // String
                else if (ft == typeof(string))
                {
                    result.AddRange(WriteStringLines(fieldName, (string)val, lvl));
                }
                // Primitivo/especial
                else if (InfoSerializer.IsPrimitiveOrSpecial(ft))
                {
                    result.Add($"{ind}{fieldName}: {FormatScalar(val)}");
                }
                // Array
                else if (ft.IsArray)
                {
                    if (ft.GetArrayRank() != 1)
                        throw new Exception($"Array multidimensional no soportado {f.Name}");

                    var arrayItems = ((IEnumerable)val).Cast<object>().ToList();
                    if (arrayItems.Count == 0 && !_info.IncludeNulls)
                        continue;

                    result.Add($"{ind}{fieldName}:");
                    result.AddRange(WriteList(arrayItems, ft.GetElementType()).AñadirInit(ind + "  "));
                }
                // IEnumerable no-string
                else if (val is IEnumerable enumerable && !(val is string))
                {
                    var listItems = enumerable.Cast<object>().ToList();
                    if (listItems.Count == 0 && !_info.IncludeNulls)
                        continue;

                    var declaredItemType = GetEnumerableItemType(enumerable);
                    string first = $"{ind}{fieldName}:";
                    if (polymorfismo) first += " !" + realType.FullName;
                    result.Add(first);
                    result.AddRange(WriteList(listItems, declaredItemType).AñadirInit(ind + "  "));
                }
                // LowParse en campo
                else if (val is LowParse lpField)
                {
                    var cont = lpField.ToLow();
                    var lines = cont.Split('\n').ToList();
                    string first = $"{ind}{fieldName}:";
                    if (polymorfismo) first += " !" + realType.FullName;

                    if (lines.Count == 1)
                    {
                        result.Add(first + " " + cont);
                    }
                    else
                    {
                        result.Add(first + " |");
                        result.AddRange(lines.AñadirInit(ind + "  "));
                    }
                }
                // Parser externo en campo
                else if (_info.GetExternalParser(realType) != null)
                {
                    var externalFieldParser = _info.GetExternalParser(realType);
                    var method = externalFieldParser.GetType().GetMethod("ToLow");
                    string low = (string)method.Invoke(externalFieldParser, new[] { val });
                    var lines = low.Split('\n').ToList();
                    string first = $"{ind}{fieldName}:";
                    if (polymorfismo) first += " !" + realType.FullName;

                    if (lines.Count == 1)
                    {
                        result.Add(first + " " + low);
                    }
                    else
                    {
                        result.Add(first + " |");
                        result.AddRange(lines.AñadirInit(ind + "  "));
                    }
                }
                // Objeto/struct anidado
                else if ((realType.IsValueType && !realType.IsPrimitive && !realType.IsEnum) || realType.IsClass)
                {
                    string first = $"{ind}{fieldName}:";
                    if (polymorfismo) first += " !" + realType.FullName;

                    var childLines = WriteLines(val, lvl + 1, null);

                    // si el hijo es un único "null" → colapsar a "campo: null"
                    if (childLines.Count == 1 && childLines[0].Trim() == "null")
                    {
                        result.Add(first + " null");
                    }
                    else
                    {
                        result.Add(first);
                        result.AddRange(childLines);
                    }
                }
            }
            return result;
        }
        finally
        {
            _info.ReferenceSet.Remove(ob);
        }
    }

    public static Type GetEnumerableItemType(IEnumerable enumerable)
    {
        if (enumerable == null) return null;

        Type enumerableType = enumerable.GetType();

        if (enumerableType.IsArray)
            return enumerableType.GetElementType();

        foreach (var i in enumerableType.GetInterfaces())
        {
            if (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                return i.GetGenericArguments()[0];
        }

        return typeof(object);
    }

    private List<string> WriteList(List<object> items, Type type_esperado = null)
    {
        var result = new List<string>();

        for (int idx = 0; idx < items.Count; idx++)
        {
            var item = items[idx];

            if (item == null)
            {
                if (type_esperado == typeof(string))
                    result.Add("-");
                else if (_info.IncludeNulls)
                    result.Add("- null");
                continue;
            }

            Type itemType = item.GetType();

            // circularidad por identidad
            if (_info.ReferenceSet.Contains(item))
            {
                if (_info.IncludeNulls)
                    result.Add("- null");
                continue;
            }

            // polimorfismo correcto — solo si es subtipo real del esperado
            bool polimorfismo =
                type_esperado != null
                && type_esperado != typeof(object)
                && type_esperado != itemType
                && type_esperado.IsAssignableFrom(itemType);

            // String vacío dentro de lista
            if (item is string emptyStr && emptyStr == "")
            {
                result.Add(polimorfismo ? $"- !{itemType.FullName}" : "-");
                continue;
            }

            // String multilínea
            if (item is string s && s.Contains('\n'))
            {
                result.Add(polimorfismo ? $"- !{itemType.FullName} |" : "- |");
                foreach (var line in s.Replace("\r", "").Split('\n'))
                    result.Add("  " + line);
                continue;
            }

            var itemLines = WriteLines(item, 0, null);

            // colapsar hijo "null"
            if (itemLines.Count == 1 && itemLines[0].Trim() == "null")
            {
                result.Add(polimorfismo ? $"- !{itemType.FullName} null" : "- null");
                continue;
            }

            if (itemLines.Count == 1)
            {
                string line = itemLines[0];
                result.Add(polimorfismo
                    ? $"- !{itemType.FullName} {line}"
                    : $"- {line}");
            }
            else
            {
                result.Add(polimorfismo ? $"- !{itemType.FullName}" : "-");
                foreach (var line in itemLines)
                    result.Add("  " + line);
            }
        }
        return result;
    }

    private List<string> WriteStringLines(string name, string val, int lvl)
    {
        string ind = new string(' ', lvl * 2);
        string normalized = (val ?? "").Replace("\t", "  ");

        if (normalized == "")
            return new List<string> { $"{ind}{name}:" };
        if (!normalized.Contains('\n'))
            return new List<string> { $"{ind}{name}: {normalized}" };

        var res = new List<string> { $"{ind}{name}: |" };
        foreach (var line in normalized.Replace("\r", "").Split('\n'))
            res.Add(ind + "  " + line);
        return res;
    }
}