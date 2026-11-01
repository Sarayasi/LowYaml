using System.Text;
using System.Collections.Generic;
using System;

namespace LowYaml;

public static class Extensiones
{

    public static List<string> AñadirInit(this List<string> strings, string indent)
    {
        for (int i = 0; i < strings.Count; i++)
        {
            strings[i] = indent + strings[i];
        }
        return strings;
    }
    // static string QuitarIndent(this string s)
    // {
    //     if (string.IsNullOrEmpty(s)) return s;

    //     int i = 0;
    //     while (i < s.Length && s[i] == ' ')
    //         i++;

    //     return s.Substring(i);
    // }
    public static string Join(this List<string> strings, string _char)
    {
        string join = strings[0];
        for (int i = 1; i < strings.Count; i++)
        {
            join += _char + strings[i];
        }
        return join;
    }
    public static string[] Separar(this string line, string split, bool incluir_split_en_la_lista = false)
    {
        if (line == null) return Array.Empty<string>();
        if (string.IsNullOrEmpty(split)) return new[] { line };

        var res = new List<string>();
        int start = 0;

        while (true)
        {
            int idx = line.IndexOf(split, start, StringComparison.Ordinal);
            if (idx == -1)
            {
                res.Add(line.Substring(start));
                break;
            }

            // antes del separador
            res.Add(line.Substring(start, idx - start));

            // opcional: incluir separador
            if (incluir_split_en_la_lista)
                res.Add(split);

            start = idx + split.Length;
        }

        return res.ToArray();
    }
    public static string Limpiar(this string s)
    {
        if (string.IsNullOrEmpty(s)) return s;

        var sb = new StringBuilder(s.Length);

        foreach (char c in s)
        {
            // elimina control chars (incluye \r, \t, etc.)
            // pero conserva espacio normal ' '
            if (!char.IsControl(c) || c == '\n')
                sb.Append(c);
        }

        return sb.ToString();
    }
}