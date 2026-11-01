using System;
using System.Text;
namespace LowYaml;

public class LowFile
{
    public static InfoSerializer default_info = new InfoSerializer();
    /// <summary>
    /// Serializa un objeto a formato LowFile
    /// </summary>
    /// <param name="ob">Objeto a serializar</param>
    /// <param name="info">InfoSerializer opcional (si es null, crea uno nuevo)</param>
    public static string Serialize(object ob, InfoSerializer info = null)
    {
        info ??=default_info;
        info ??= new InfoSerializer();
        var writer = new LowWriter(info);
        var sb = new StringBuilder();
        sb.Append(writer.Write(ob, 0));
        sb.Append("\n# creado con lowfile");
        return sb.ToString();
    }
    
    /// <summary>
    /// Deserializa un string a objeto del tipo T
    /// </summary>
    /// <param name="text">Texto en formato LowFile</param>
    /// <param name="info">InfoSerializer opcional (si es null, crea uno nuevo)</param>
    public static T Deserialize<T>(string text, InfoSerializer info = null) where T : new()
    {
        info ??=default_info;
        info ??= new InfoSerializer();
        var reader = new LowReader(info);
        return (T)reader.Deserialize(text, typeof(T));
    }
    
    /// <summary>
    /// Deserializa un string a objeto del tipo especificado
    /// </summary>
    /// <param name="text">Texto en formato LowFile</param>
    /// <param name="t">Tipo del objeto a deserializar</param>
    /// <param name="info">InfoSerializer opcional (si es null, crea uno nuevo)</param>
    public static object Deserialize(string text, Type t, InfoSerializer info = null)
    {
        info ??=default_info;
        info ??= new InfoSerializer();
        var reader = new LowReader(info);
        return reader.Deserialize(text, t);
    }
}
