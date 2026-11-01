using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace LowYaml;


public class LowReader
{
    private readonly InfoSerializer _info;
    private readonly Dictionary<string, Type> _typeCache = new();

    public LowReader(InfoSerializer info)
    {
        _info = info;

        var special = new[] {
            typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(short), typeof(ushort),
            typeof(byte), typeof(sbyte), typeof(float), typeof(double), typeof(decimal),
            typeof(string), typeof(bool), typeof(char), typeof(object),
            typeof(DateTime), typeof(DateTimeOffset), typeof(TimeSpan), typeof(Guid),
            typeof(Uri), typeof(Version), typeof(DateOnly), typeof(TimeOnly)
        };

        var fromAssemblies = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a =>
        {
            try { return a.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { return ex.Types.Where(x => x != null)!; }
            catch { return Array.Empty<Type>(); }
        });

        foreach (var t in fromAssemblies.Concat(special).Distinct())
        {
            if (!string.IsNullOrEmpty(t.Name) && !_typeCache.ContainsKey(t.Name))
                _typeCache[t.Name] = t;
            if (!string.IsNullOrEmpty(t.FullName) && !_typeCache.ContainsKey(t.FullName!))
                _typeCache[t.FullName!] = t;
        }
    }

    public static int Indent(string s) => s.TakeWhile(c => c == ' ').Count() / 2;

    // Verificar si es diccionario
    public static bool IsDict(Type t) => typeof(IDictionary).IsAssignableFrom(t) ||
                                          (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Dictionary<,>));


    public object Deserialize(string text, Type t)
    {
        if (text == null) throw new ArgumentNullException(nameof(text));
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n')
            .Where(l => !l.TrimStart().StartsWith('#'))
            .Select(CleanLine)   // CAMBIO: ya no se recortan los espacios finales
            .ToList();
        int i = 0;
        return Read(t, lines, ref i, 0);
    }

    private static string CleanLine(string l) => string.IsNullOrWhiteSpace(l) ? "" : l;
    private object Read(Type t, List<string> lines, ref int i, int lvl)
    {
        if (t.GetCustomAttribute<LowIgnore>() != null) return null;

        if (t == typeof(string)) return ReadString(lines, ref i, lvl);
        if (IsPrimOrSpecial(t)) return ReadPrimitive(t, lines, ref i);
        if (IsDict(t)) return ReadDict(t, lines, ref i, lvl);
        if (IsCollectionLike(t)) return ReadCollection(t, lines, ref i, lvl);
        if (t.IsAbstract || t.IsInterface)
            throw new Exception($"No se puede instanciar tipo abstracto/interfaz '{t.FullName}'");

        return ReadObject(t, lines, ref i, lvl);
    }

    private string ReadString(List<string> lines, ref int i, int lvl)
    {
        if (i >= lines.Count) return "";
        if (IsMultilineMarker(lines[i].Trim()))
        {
            string prefix = ExtractInlinePrefix(lines[i].Trim());
            i++;
            return ReadMultilineBlock(lines, ref i, lvl, prefix);
        }
        int start = i;
        while (i < lines.Count && InfoSerializer.Indent(lines[i]) >= lvl) i++;
        return string.Join("\n", lines.Skip(start).Take(i - start));
    }

    private object ReadPrimitive(Type t, List<string> lines, ref int i)
    {
        bool nonNullableValue = t.IsValueType && Nullable.GetUnderlyingType(t) == null;
        object Default() => nonNullableValue ? Activator.CreateInstance(t) : null;

        if (i >= lines.Count || string.IsNullOrWhiteSpace(lines[i])) return Default();
        if (lines[i].Trim() == "null") { i++; return Default(); }
        return Parse(t, lines[i++].Trim());
    }

    private object ReadObject(Type t, List<string> lines, ref int i, int lvl)
    {
        var obj = Activator.CreateInstance(t);

        if (IsLowParseType(t))
        {
            ((LowParse)obj).LowTo(ConsumeBlockLines(lines, ref i, lvl));
            return obj;
        }
        var ext = _info.GetExternalParser(t);
        if (ext != null)
            return ext.GetType().GetMethod("LowTo")!.Invoke(ext, new object[] { ConsumeBlockLines(lines, ref i, lvl) });

        while (i < lines.Count)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) { i++; continue; }
            int indent = InfoSerializer.Indent(line);
            if (indent < lvl) break;
            if (indent > lvl) { i++; continue; }
            if (line.TrimStart().StartsWith('#')) { i++; continue; }

            string body = line.Length >= indent * 2 ? line[(indent * 2)..] : line.TrimStart();
            int sep = body.IndexOf(':');
            string name = sep >= 0 ? body[..sep].Trim() : "";
            string rest = sep >= 0 ? body[(sep + 1)..] : "";
            if (rest.Length > 0 && rest[0] == ' ') rest = rest[1..];

            var field = FindField(t, name);
            if (field == null) { i++; continue; }

            AssignField(obj!, field, rest, lines, ref i, lvl);
        }
        return obj;
    }

    private void AssignField(object obj, FieldInfo field, string rest, List<string> lines, ref int i, int lvl)
    {
        // 1) Sin contenido inline
        if (string.IsNullOrWhiteSpace(rest))
        {
            if (IsPrimOrSpecial(field.FieldType)) { i++; return; }
            bool hasChild = i + 1 < lines.Count && InfoSerializer.Indent(lines[i + 1]) > lvl;
            if (!hasChild) { i++; return; }
            i++;
            field.SetValue(obj, Read(field.FieldType, lines, ref i, lvl + 1));
            return;
        }

        // 2) Polimorfismo (!Tipo)
        bool wasBang = rest.TrimStart().StartsWith("!");
        Type realType = ResolvePolymorphism(field.FieldType, ref rest);

        if (wasBang && string.IsNullOrWhiteSpace(rest))
        {
            i++;
            field.SetValue(obj, Read(realType, lines, ref i, lvl + 1));
            return;
        }

        // 3) String literal (incluye "null" como texto)
        if (realType == typeof(string))
        {
            string r = rest.TrimStart();
            if (IsMultilineMarker(r))
            {
                i++;
                field.SetValue(obj, ReadMultilineBlock(lines, ref i, lvl, ExtractInlinePrefix(r)));
            }
            else { field.SetValue(obj, rest); i++; }
            return;
        }

        // 4) Resto: delega a ReadValue
        field.SetValue(obj, ReadValue(realType, rest, lines, ref i, lvl));
    }

    private object ReadValue(Type type, string val, List<string> lines, ref int i, int lvl)
    {
        Type real = ResolvePolymorphism(type, ref val);
        if (val.Trim() == "null") { i++; return null; }

        if (real == typeof(string))
        {
            if (IsMultilineMarker(val))
            {
                string prefix = ExtractInlinePrefix(val);
                i++;
                return ReadMultilineBlock(lines, ref i, lvl, prefix);
            }
            i++;
            return val;
        }

        if (IsLowParseType(real))
        {
            var lp = (LowParse)Activator.CreateInstance(real);
            if (IsMultilineMarker(val))
            {
                string prefix = ExtractInlinePrefix(val);
                i++;
                lp.LowTo(ReadMultilineBlock(lines, ref i, lvl, prefix));
            }
            else { lp.LowTo(val); i++; }
            return lp;
        }

        if (IsPrimOrSpecial(real)) { i++; return Parse(real, val.Trim()); }

        if (IsCollectionLike(real))
        {
            if (!string.IsNullOrWhiteSpace(val))
            {
                i++;
                int temp = 0;
                return ReadCollection(real, new List<string> { val }, ref temp, 0);
            }
            i++;
            return ReadCollection(real, lines, ref i, lvl);
        }

        var ext = _info.GetExternalParser(real);
        if (ext != null) { i++; return CreateWithExternalParser(real, val); }

        return ReadInlineOrBlock(real, val, lines, ref i);
    }

    private object ReadInlineOrBlock(Type type, string inline, List<string> lines, ref int i)
    {
        if (!string.IsNullOrWhiteSpace(inline))
        {
            i++;
            int temp = 0;
            return Read(type, new List<string> { inline }, ref temp, 0);
        }
        i++;
        return Read(type, lines, ref i, InfoSerializer.Indent(lines[i - 1]) + 1);
    }

    private string ReadMultilineBlock(List<string> lines, ref int i, int lvl, string prefix)
    {
        var buffer = new List<string>();
        if (!string.IsNullOrEmpty(prefix)) buffer.Add(prefix);

        while (i < lines.Count)
        {
            string raw = lines[i];
            if (string.IsNullOrWhiteSpace(raw)) { buffer.Add(""); i++; continue; }
            if (InfoSerializer.Indent(raw) <= lvl) break;

            int cut = (lvl + 1) * 2;
            buffer.Add(raw.Length >= cut ? raw[cut..] : raw.TrimStart());
            i++;
        }
        while (buffer.Count > 0 && buffer[^1].Length == 0) buffer.RemoveAt(buffer.Count - 1);
        return string.Join("\n", buffer);
    }

    private static bool IsMultilineMarker(string s)
    {
        var t = s.TrimStart();
        return t is "|" or "|-" or ">" or "| " or "|- " or "> "
            || t.StartsWith("| ") || t.StartsWith("|- ") || t.StartsWith("> ");
    }

    private static string ExtractInlinePrefix(string marker)
    {
        var t = marker.TrimStart();
        int idx;
        if (t.StartsWith("|- ")) idx = 3;
        else if (t.StartsWith("| ") || t.StartsWith("> ")) idx = 2;
        else return "";
        return t[idx..].TrimStart();
    }


    private static bool IsCollectionLike(Type t) =>
        (t.IsArray && t.GetArrayRank() == 1)
        || (t.IsGenericType && (t.GetGenericTypeDefinition() == typeof(List<>)
                             || t.GetGenericTypeDefinition() == typeof(HashSet<>)));

    private object ReadCollection(Type collectionType, List<string> lines, ref int i, int lvl)
    {
        Type elemType, listType;
        if (collectionType.IsArray)
        {
            elemType = collectionType.GetElementType()!;
            listType = typeof(List<>).MakeGenericType(elemType);
        }
        else if (collectionType.GetGenericTypeDefinition() == typeof(HashSet<>))
        {
            elemType = collectionType.GetGenericArguments()[0];
            listType = typeof(List<>).MakeGenericType(elemType);
        }
        else
        {
            elemType = collectionType.GetGenericArguments()[0];
            listType = collectionType;
        }

        var list = (IList)ReadList(listType, lines, ref i, lvl);
        if (collectionType == listType) return list;

        if (collectionType.IsArray)
        {
            var arr = Array.CreateInstance(elemType, list.Count);
            list.CopyTo(arr, 0);
            return arr;
        }

        var hs = Activator.CreateInstance(collectionType);
        var add = collectionType.GetMethod("Add")!;
        foreach (var item in list) add.Invoke(hs, new[] { item });
        return hs;
    }

    private object ReadList(Type listType, List<string> lines, ref int i, int lvl)
    {
        var itemType = listType.GetGenericArguments()[0];
        var list = (IList)Activator.CreateInstance(listType);
        bool isSimple = IsPrimOrSpecial(itemType);

        // saltar líneas que no son items (comentarios / "key:")
        while (i < lines.Count && InfoSerializer.Indent(lines[i]) >= lvl)
        {
            string trimmed0 = lines[i].Trim();
            if (trimmed0.StartsWith("-")) break;
            if (InfoSerializer.Indent(lines[i]) > lvl) break;
            if (string.IsNullOrWhiteSpace(trimmed0)) { i++; continue; }
            break;
        }

        while (i < lines.Count)
        {
            int indent = InfoSerializer.Indent(lines[i]);
            if (indent < lvl) break;

            string raw = lines[i];
            string trimmed = raw.Trim();
            if (trimmed.StartsWith("#")) { i++; continue; }

            if (!trimmed.StartsWith("-"))
            {
                if (!string.IsNullOrWhiteSpace(trimmed)) break;
                i++;
                break;
            }

            // CAMBIO: tras el '-' se elimina UN solo espacio separador;
            // el resto (espacios iniciales y finales) forma parte del valor.
            string afterDash = raw.TrimStart()[1..];
            string content = afterDash.StartsWith(' ') ? afterDash[1..] : afterDash;
            string probe = content.Trim();   // solo para detectar marcadores / vacío / "!Tipo"
            i++;

            if (isSimple)
            {
                if (itemType == typeof(string) && IsMultilineMarker(probe))
                {
                    string prefix = ExtractInlinePrefix(probe);
                    list.Add(ReadMultilineBlock(lines, ref i, lvl, prefix));
                }
                else if (probe.Length == 0)
                {
                    list.Add(IsNullableOrReference(itemType)
                        ? null
                        : Activator.CreateInstance(itemType));
                }
                else list.Add(Parse(itemType, content));   // string se conserva tal cual
            }
            else if (probe.StartsWith("!"))
            {
                string tmp = probe;
                Type realType = ResolvePolymorphism(itemType, ref tmp);
                if (!string.IsNullOrWhiteSpace(tmp))
                {
                    int temp = 0;
                    list.Add(Read(realType, new List<string> { tmp }, ref temp, 0));
                }
                else list.Add(Read(realType, lines, ref i, lvl + 1));
            }
            else if (probe.Length > 0)
            {
                int temp = 0;
                string tmp = probe;
                list.Add(Read(ResolvePolymorphism(itemType, ref tmp),
                              new List<string> { probe }, ref temp, 0));
            }
            else list.Add(Read(itemType, lines, ref i, lvl + 1));
        }
        return list;
    }
    private object ReadDict(Type dictType, List<string> lines, ref int i, int lvl)
    {
        var args = dictType.GetGenericArguments();
        var dict = (IDictionary)Activator.CreateInstance(dictType);

        if (i < lines.Count && lines[i].Trim().EndsWith(":") && InfoSerializer.Indent(lines[i]) == lvl)
            i++;

        while (i < lines.Count)
        {
            int indent = InfoSerializer.Indent(lines[i]);
            if (indent < lvl) break;
            if (indent != lvl || !lines[i].Trim().StartsWith("-")) { i++; continue; }

            int itemIndent = indent;
            i++;

            object key = null, value = null;
            while (i < lines.Count)
            {
                int fi = InfoSerializer.Indent(lines[i]);
                if (fi <= itemIndent) break;
                if (fi != itemIndent + 1) { i++; continue; }

                // CAMBIO: solo TrimStart, para no perder espacios finales de valores string
                string line = lines[i].TrimStart();
                if (line.StartsWith("#")) { i++; continue; }

                if (line.StartsWith("key:"))
                    key = ReadValue(args[0], StripOneSpace(line[4..]), lines, ref i, itemIndent + 1);
                else if (line.StartsWith("value:"))
                    value = ReadValue(args[1], StripOneSpace(line[6..]), lines, ref i, itemIndent + 1);
                else i++;
            }

            if (key != null)
            {
                if (dict.Contains(key)) dict[key] = value;
                else dict.Add(key, value);
            }
        }
        return dict;
    }

    // Quita UN solo espacio inicial (separador tras "key:" / "value:")
    private static string StripOneSpace(string s) => s.Length > 0 && s[0] == ' ' ? s[1..] : s;


    private object Parse(Type t, string v)
    {
        var underlying = Nullable.GetUnderlyingType(t);
        if (underlying != null)
            return v.Trim() == "null" ? null : Parse(underlying, v);

        if (t == typeof(string)) return v;
        v = v.Trim();

        if (t.IsEnum) return Enum.Parse(t, v, false);
        if (t == typeof(bool)) return bool.Parse(v);
        if (t == typeof(char)) return char.Parse(v);

        if (t == typeof(int)) return int.Parse(v, CultureInfo.InvariantCulture);
        if (t == typeof(long)) return long.Parse(v, CultureInfo.InvariantCulture);
        if (t == typeof(short)) return short.Parse(v, CultureInfo.InvariantCulture);
        if (t == typeof(ushort)) return ushort.Parse(v, CultureInfo.InvariantCulture);
        if (t == typeof(uint)) return uint.Parse(v, CultureInfo.InvariantCulture);
        if (t == typeof(ulong)) return ulong.Parse(v, CultureInfo.InvariantCulture);
        if (t == typeof(byte)) return byte.Parse(v, CultureInfo.InvariantCulture);
        if (t == typeof(sbyte)) return sbyte.Parse(v, CultureInfo.InvariantCulture);
        if (t == typeof(float)) return float.Parse(v, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture);
        if (t == typeof(double)) return double.Parse(v, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture);
        if (t == typeof(decimal)) return decimal.Parse(v, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture);

        if (t == typeof(DateTime)) return DateTime.Parse(v, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        if (t == typeof(DateTimeOffset)) return DateTimeOffset.Parse(v, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        if (t == typeof(TimeSpan)) return TimeSpan.Parse(v, CultureInfo.InvariantCulture);
        if (t == typeof(Guid)) return Guid.Parse(v);
        if (t == typeof(Uri)) return new Uri(v);
        if (t == typeof(Version)) return Version.Parse(v);

        if (t == typeof(DateOnly))
            return DateOnly.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                ? d : DateOnly.ParseExact(v, "yyyy-MM-dd", CultureInfo.InvariantCulture);

        if (t == typeof(TimeOnly))
            return TimeOnly.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.None, out var to)
                ? to : TimeOnly.ParseExact(v, "HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture);

        return Convert.ChangeType(v, t, CultureInfo.InvariantCulture);
    }


    private static bool IsLowParseType(Type t) => typeof(LowParse).IsAssignableFrom(t);
    private static bool IsPrimOrSpecial(Type t) => InfoSerializer.IsPrimitiveOrSpecial(Nullable.GetUnderlyingType(t) ?? t);
    private static bool IsNullableOrReference(Type t) => !t.IsValueType || Nullable.GetUnderlyingType(t) != null;

    private static string ConsumeBlockLines(List<string> lines, ref int i, int lvl)
    {
        var buffer = new List<string>();
        while (i < lines.Count && InfoSerializer.Indent(lines[i]) >= lvl)
        {
            buffer.Add(lines[i].Length >= lvl * 2 ? lines[i][(lvl * 2)..] : lines[i]);
            i++;
        }
        return string.Join("\n", buffer);
    }

    private Type ResolveType(string name, Type baseType) =>
        _typeCache.TryGetValue(name, out var t) && baseType.IsAssignableFrom(t)
            ? t : throw new Exception($"Tipo '{name}' no encontrado o no asignable a '{baseType.Name}'");

    private Type ResolvePolymorphism(Type expected, ref string rest)
    {
        if (string.IsNullOrEmpty(rest) || !rest.TrimStart().StartsWith("!"))
            return expected;

        string trimmed = rest.TrimStart();
        int sp = trimmed.IndexOf(' ');
        string typeName = sp == -1 ? trimmed[1..] : trimmed[1..sp];

        if (string.IsNullOrEmpty(typeName))
        {
            rest = sp == -1 ? "" : trimmed[(sp + 1)..];
            return expected;
        }
        rest = sp == -1 ? "" : trimmed[(sp + 1)..];
        return ResolveType(typeName, expected);
    }

    private FieldInfo FindField(Type t, string name)
    {
        var f = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (f != null) return f;
        return t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(x => x.GetCustomAttribute<LowRename>()?.Nombre == name);
    }

    private object CreateWithExternalParser(Type t, string value)
    {
        var parser = _info.GetExternalParser(t)
            ?? throw new Exception($"Sin parser externo para '{t.Name}'");
        var method = parser.GetType().GetMethod("LowTo")
            ?? throw new Exception($"El parser externo de '{t.Name}' no tiene método 'LowTo'");
        return method.Invoke(parser, new object[] { value });
    }
}