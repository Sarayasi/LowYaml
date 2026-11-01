using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace LowYaml.Test;
#region CLASES DE SOPORTE
public class ClaseConRenombres
{
    [LowRename("id")]
    public string Id = "123";
    
    [LowRename("nombre_completo")]
    public string Nombre = "Juan Perez";
    
    [LowRename("edad_usuario")]
    public int Edad = 25;
    
    [LowRename("es_activo")]
    public bool Activo = true;
    
    [LowRename("lista_numeros")]
    public List<int> Numeros = new() { 1, 2, 3 };
    
    [LowRename("datos_extra")]
    public Dictionary<string, string> Extra = new() { ["key1"] = "valor1", ["key2"] = "valor2" };
    
    public string Normal = "sin renombrar";
    
    [LowIgnore]
    public string Ignorado = "no se serializa";
}
// =========================================
// CLASES CON LowParse<T>
// =========================================
public class Vector3 : LowParse
{
    public float X, Y, Z;
    
    public string ToLow() => $"{X};{Y};{Z}";
    public void LowTo(string line)
    {
        var parts = line.Split(';');
        X = float.Parse(parts[0]);
        Y = float.Parse(parts[1]);
        Z = float.Parse(parts[2]);
    }
}
public class ColorRGBA : LowParse
{
    public byte R, G, B, A;
    
    public string ToLow() => $"{R},{G},{B},{A}";
    public void LowTo(string line)
    {
        var parts = line.Split(',');
        R = byte.Parse(parts[0]);
        G = byte.Parse(parts[1]);
        B = byte.Parse(parts[2]);
        A = parts.Length > 3 ? byte.Parse(parts[3]) : (byte)255;
    }
}
public abstract class Entidad
{
    public string Id;
}
public class LowItem : Entidad, LowParse
{
    public string Nombre;
    public int Cantidad;
    public string ToLow() => $"{Nombre}|{Cantidad}";
    public void LowTo(string line)
    {
    string ini = line;
        try
    {
        var sp = line.Split('|');
        Nombre = sp[0];
        Cantidad = int.Parse(sp[1]);
    }
    catch (System.Exception)
    {
        Console.WriteLine("Error en low parse{" + GetType().FullName + "} : {" + ini + "}");
    }
    }
}
public class Arma : LowItem
{
    public int Danio;
}
public class Pocion : LowItem
{
    public float Curacion;
}
public abstract class Personaje : Entidad
{
    public string Nombre;
    public int Nivel;
    public List<LowItem> Inventario = new();
}
public class Humano : Personaje
{
    public string Profesion;
}
public class Guerrero : Humano
{
    public int Fuerza;
}
public class Mago : Humano, LowParse
{
    public int Mana;
    public string ToLow() => Mana.ToString();
    public void LowTo(string line) => Mana = int.Parse(line);
}
public class Bestia : Personaje
{
    public bool Salvaje;
}
public class Dragon : Bestia
{
    public float Fuego;
    public List<Personaje> Enemigos = new();
}
public class ShaderData : LowParse
{
    public string Codigo;
    public string ToLow() => Codigo;
    public void LowTo(string line) => Codigo = line;
}
// Clase SIN implementar LowParse (para usar solo con parser externo)
public class Punto2D
{
    public int X, Y;
}
public class Rectangulo
{
    public float Ancho, Alto;
}
public class PersonaExterna
{
    public string Nombre;
    public int Edad;
}
// =========================================
// PARSERS EXTERNOS
// =========================================
public class Punto2DParser : LowParse<Punto2D>
{
    public string ToLow(Punto2D ob) => $"{ob.X}:{ob.Y}";
    
    public Punto2D LowTo(string line)
    {
        var parts = line.Split(':');
        return new Punto2D { X = int.Parse(parts[0]), Y = int.Parse(parts[1]) };
    }
}
public class RectanguloParser : LowParse<Rectangulo>
{
    public string ToLow(Rectangulo ob) => $"{ob.Ancho}x{ob.Alto}";
    
    public Rectangulo LowTo(string line)
    {
        var parts = line.Split('x');
        return new Rectangulo { Ancho = float.Parse(parts[0]), Alto = float.Parse(parts[1]) };
    }
}
public class PersonaExternaParser : LowParse<PersonaExterna>
{
    public string ToLow(PersonaExterna ob) => $"{ob.Nombre}|{ob.Edad}";
    
    public PersonaExterna LowTo(string line)
    {
        var parts = line.Split('|');
        return new PersonaExterna { Nombre = parts[0], Edad = int.Parse(parts[1]) };
    }
}
#endregion
#region CLASE PRINCIPAL DE TEST
public class ArchivoTest
{
// =========================================
// PRIMITIVOS
// =========================================
    
    public List<LowItem> Items = new();
    public Dictionary<string, LowItem> ItemsDict = new();
    public int Entero = 42;
    public float Flotante = 3.14f;
    public double Doble = 99.99;
    public bool Booleano = true;
    public char Caracter = 'A';
    public long Largo = 123456789L;
    public byte Byte = 255;
    
    // =========================================
    // STRINGS
    // =========================================
    public string TextoSimple = "Hola mundo";
    public string TextoMultilinea = "linea1\nlinea2\nlinea3";
    public string TextoVacio = "";
    public string TextoConEspacios = "   texto con espacios   ";
    
    // =========================================
    // LISTAS
    // =========================================
    public List<int> Numeros = new() { 1, 2, 3, 4, 5 };
    public List<string> Nombres = new() { "Ana", "Juan", "Pedro" };
    public List<Personaje> Personajes = new();
    public List<List<int>> Matriz = new() { new() { 1, 2 }, new() { 3, 4 } };
    public List<int> ListaVacia = new();
    public List<int> ListaUnItem = new() { 99 };
    
    // =========================================
    // ARRAYS
    // =========================================
    public int[] ArrayNumeros = new int[] { 10, 20, 30 };
    public string[] ArrayStrings = new string[] { "a", "b", "c" };
    public int[] ArrayVacio = new int[0];
    public int[] ArrayUnItem = new int[] { 777 };
    
    // =========================================
    // DICCIONARIOS
    // =========================================
    public Dictionary<string, int> Scores = new();
    public Dictionary<string, string> Textos = new();
    public Dictionary<string, Personaje> PersonajesDict = new();
    public Dictionary<string, List<int>> ListasDict = new();
    public Dictionary<string, Dictionary<string, int>> NestedDict = new();
    public Dictionary<string, int[]> ArraysDict = new();
    public Dictionary<string, ShaderData> Shaders = new();
    public Dictionary<string, string> KeysRaras = new();
    public Dictionary<string, int> DictVacio = new();
    
    // =========================================
    // LOWPARSE
    // =========================================
    public ShaderData ShaderSimple = new() { Codigo = "solid red" };
    public ShaderData ShaderMultilinea = new() { Codigo = "color blue\nwave 0.5\nspeed 3" };
    
    // =========================================
    // OBJETOS COMPLEJOS
    // =========================================
    public LowItem ItemSimple = new() { Id = "i1", Nombre = "Pocion", Cantidad = 10 };
    public Arma ArmaEjemplo = new() { Id = "a1", Nombre = "Espada", Cantidad = 1, Danio = 50 };
    public Pocion PocionEjemplo = new() { Id = "p1", Nombre = "Curacion", Cantidad = 5, Curacion = 25.5f };
    public Guerrero GuerreroEjemplo = new() { Id = "g1", Nombre = "Ragnar", Nivel = 10, Profesion = "Tanque", Fuerza = 999 };
    public Mago MagoEjemplo = new() { Id = "m1", Nombre = "Ezren", Nivel = 14, Profesion = "Hechicero", Mana = 400 };
    public Dragon DragonEjemplo = new() { Id = "d1", Nombre = "Aldur", Nivel = 99, Salvaje = true, Fuego = 999.9f };
    public Node nodo_vacio;
    public Node nodo_normal;
    public Node nodo_circular;
    public Node nodo_a;
    public Node nodo_b;
    
    // =========================================
    // NULLS
    // =========================================
    public LowItem ItemNulo = null;
    public string StringNulo = null;
    public List<int> ListaNula = null;
    public Dictionary<string, int> DictNulo = null;
    
    // =========================================
    // VALORES POR DEFECTO
    // =========================================
    public int Cero = 0;
    public bool Falso = false;
    public string Vacio = "";
    // =========================================
    // NUEVOS CAMPOS PARA PROBAR LowRename
    // =========================================
    public ClaseConRenombres ObjetoConRenombres = new ClaseConRenombres();
    public List<ClaseConRenombres> ListaConRenombres = new();
    
    // =========================================
    // NUEVOS CAMPOS PARA PROBAR LowParse<T>
    // =========================================
    public Vector3 Posicion = new Vector3 { X = 10.5f, Y = 20.3f, Z = 5.7f };
    public ColorRGBA ColorFondo = new ColorRGBA { R = 255, G = 128, B = 64, A = 200 };
    public List<Vector3> Puntos3D = new();
    public Dictionary<string, ColorRGBA> PaletaColores = new();
    
    
    // =========================================
    // NUEVOS: LowParse<T> externo (Vector3, ColorRGBA)
    // =========================================
    public Vector3 PosicionInicial = new Vector3 { X = 10.5f, Y = 20.3f, Z = 5.7f };
    public Vector3 PosicionFinal = new Vector3 { X = 100, Y = 200, Z = 50 };
    public ColorRGBA ColorTexto = new ColorRGBA { R = 0, G = 0, B = 0, A = 255 };
    
    // =========================================
    // NUEVOS: Nullable y tipos especiales
    // =========================================
    public int? EnteroNulo = null;
    public int? EnteroConValor = 42;
    public DateTime? FechaNula = null;
    public DateTime FechaActual = DateTime.Now;
    public DateTime FechaUTC = DateTime.UtcNow;
    public DateOnly SoloFecha = DateOnly.FromDateTime(DateTime.Today);
    public TimeOnly SoloHora = TimeOnly.FromDateTime(DateTime.Now);
    public Guid Identificador = Guid.NewGuid();
    public decimal ValorDecimal = 123.456m;
    public Uri UrlEjemplo = new Uri("https://ejemplo.com/ruta");
    public Version VersionApp = new Version(1, 2, 3, 4);
    
    // =========================================
    // NUEVOS: HashSet
    // =========================================
    public HashSet<int> HashSetNumeros = new() { 1, 2, 3, 4, 5 };
    public HashSet<string> HashSetStrings = new() { "a", "b", "c" };
    public HashSet<Vector3> HashSetPuntos = new();
    
    // =========================================
    // NUEVOS: LowParse externo con null
    // =========================================
    public Vector3 PosicionNula = null;
    public ColorRGBA ColorNulo = null;
    
    
    // =========================================
    // NUEVOS: Strings multilínea en diferentes contextos
    // =========================================
    [LowComment("Texto multilínea simple")]
    public string TextoMultiSimple = "línea1\nlínea2\nlínea3";
    
    [LowComment("Texto multilínea con espacios")]
    public string TextoMultiEspacios = "  línea con espacios\n\tlínea con tab\n  línea final  ";
    
    public List<string> ListaStringsMulti = new();
    public Dictionary<string, string> DictStringsMulti = new();
    
    [LowComment("Punto 2D con parser externo")]
    public Punto2D Origen = new Punto2D { X = 10, Y = 20 };
    
    public Punto2D Destino = new Punto2D { X = 100, Y = 200 };
    public List<Punto2D> ListaPuntos = new();
    public Dictionary<string, Punto2D> DiccionarioPuntos = new();
    
    public Rectangulo Marco = new Rectangulo { Ancho = 1920, Alto = 1080 };
    public List<Rectangulo> Rectangulos = new();
    
    public PersonaExterna Usuario = new PersonaExterna { Nombre = "Juan", Edad = 30 };
    public List<PersonaExterna> Personas = new();
    
    // Null con parser externo
    public Punto2D PuntoNulo = null;
    public Dictionary<string, Entidad> DiccionarioPolimorficoEntidades = new();
    public Dictionary<string, LowItem> DiccionarioPolimorficoItems = new();
    public Dictionary<string, Personaje> DiccionarioPolimorficoPersonajes = new();
    public Dictionary<object, object> DiccionarioPolimorficoObject = new();
    public Dictionary<string, object> DiccionarioPolimorficoObjectValue = new();
    
    
    // Constructor para inicializar colecciones
    public void construir()
    {
        // Items
        Items.Add(new Arma() { Id = "a1", Nombre = "Espada", Cantidad = 1, Danio = 30 });
        Items.Add(new LowItem() { Id = "i1", Nombre = "Item1", Cantidad = 5 });
        Items.Add(new Pocion() { Id = "p1", Nombre = "Pocion", Cantidad = 3, Curacion = 15.5f });
        
        // Personajes
        var guerrero = new Guerrero() { Id = "g1", Nombre = "Ragnar", Nivel = 10, Profesion = "Tanque", Fuerza = 999 };
        var mago = new Mago() { Id = "m1", Nombre = "Ezren", Nivel = 14, Profesion = "Hechicero", Mana = 400 };
        guerrero.Inventario.Add(new Arma() { Id = "a2", Nombre = "Hacha", Cantidad = 1, Danio = 45 });
        mago.Inventario.Add(new Pocion() { Id = "p2", Nombre = "Mana", Cantidad = 3, Curacion = 50 });
        Personajes.Add(guerrero);
        Personajes.Add(mago);
        
        // Scores
        Scores["juan"] = 10;
        Scores["pedro"] = 99;
        Scores["ana"] = -5;
        
        // Textos
        Textos["simple"] = "valor";
        Textos["multilinea"] = "hola\nmundo";
        
        // ItemsDict
        ItemsDict["espada"] = new Arma() { Id = "itm1", Nombre = "Espada Roja", Cantidad = 1, Danio = 55 };
        ItemsDict["pocion"] = new LowItem() { Id = "itm2", Nombre = "Pocion", Cantidad = 5 };
        
        // PersonajesDict
        PersonajesDict["tank"] = new Guerrero() { Id = "p1", Nombre = "Ragnar", Fuerza = 999 };
        PersonajesDict["mage"] = new Mago() { Id = "p2", Nombre = "Ezren", Mana = 400 };
        
        // ListasDict
        ListasDict["a"] = new List<int>() { 1, 2, 3, 4 };
        ListasDict["b"] = new List<int>() { 9, 8, 7 };
        
        // NestedDict
        NestedDict["grupo1"] = new Dictionary<string, int>() { ["x"] = 1, ["y"] = 2 };
        NestedDict["grupo2"] = new Dictionary<string, int>() { ["a"] = 100, ["b"] = 200 };
        
        // ArraysDict
        ArraysDict["nums"] = new int[] { 5, 6, 7, 8 };
        
        // Shaders
        Shaders["agua"] = new ShaderData() { Codigo = "color blue\nwave 0.5\nspeed 3" };
        
        // Keys raras
        KeysRaras["hola mundo"] = "a";
        KeysRaras["x:y"] = "b";
        KeysRaras["----"] = "c";
        KeysRaras["123"] = "d";
        
        // Dragon con enemigos (referencias)
        DragonEjemplo.Enemigos.Add(guerrero);
        DragonEjemplo.Enemigos.Add(mago);
        // nodos
        nodo_normal = new Node { Name = "normal" };
        nodo_circular = new Node{Name = "circulo"};
        nodo_circular.Next = nodo_circular;
        
        nodo_a = new Node { Name = "A" };
        nodo_b = new Node { Name = "B" };
        nodo_a.Next = nodo_b;
        nodo_b.Next = nodo_a;  // ¡Circular!
        // Inicializar ListaConRenombres
        ListaConRenombres.Add(new ClaseConRenombres { Id = "1", Nombre = "Item1", Edad = 10, Activo = true });
        ListaConRenombres.Add(new ClaseConRenombres { Id = "2", Nombre = "Item2", Edad = 20, Activo = false });
        ListaConRenombres.Add(new ClaseConRenombres { Id = "3", Nombre = "Item3", Edad = 30, Activo = true });
        
        // Inicializar Puntos3D
        Puntos3D.Add(new Vector3 { X = 0, Y = 0, Z = 0 });
        Puntos3D.Add(new Vector3 { X = 1, Y = 1, Z = 1 });
        Puntos3D.Add(new Vector3 { X = 2, Y = 4, Z = 8 });
        
        // Inicializar PaletaColores
        PaletaColores["rojo"] = new ColorRGBA { R = 255, G = 0, B = 0 };
        PaletaColores["verde"] = new ColorRGBA { R = 0, G = 255, B = 0 };
        PaletaColores["azul"] = new ColorRGBA { R = 0, G = 0, B = 255 };
        PaletaColores["blanco"] = new ColorRGBA { R = 255, G = 255, B = 255 };
        
        // =========================================
        // NUEVOS: HashSetPuntos
        // =========================================
        HashSetPuntos.Add(new Vector3 { X = 1, Y = 1, Z = 1 });
        HashSetPuntos.Add(new Vector3 { X = 2, Y = 2, Z = 2 });
        HashSetPuntos.Add(new Vector3 { X = 3, Y = 3, Z = 3 });
        
        // =========================================
        // NUEVOS: ListaStringsMulti
        // =========================================
        ListaStringsMulti.Add("string simple");
        ListaStringsMulti.Add("línea1\nlínea2\nlínea3");
        ListaStringsMulti.Add("  espacios al inicio y al final  ");
        ListaStringsMulti.Add("");
        ListaStringsMulti.Add(null);
        
        // =========================================
        // NUEVOS: DictStringsMulti
        // =========================================
        DictStringsMulti["simple"] = "valor simple";
        DictStringsMulti["multiline"] = "hola\nmundo\ncruel";
        DictStringsMulti["espacios"] = "  texto con espacios  ";
        DictStringsMulti["vacio"] = "";
        
        // =========================================
        // NUEVOS: ClaseConRenombres (continuación)
        // =========================================
        ListaConRenombres.Add(new ClaseConRenombres { Id = "1", Nombre = "Item1", Edad = 10, Activo = true });
        ListaConRenombres.Add(new ClaseConRenombres { Id = "2", Nombre = "Item2", Edad = 20, Activo = false });
        ListaConRenombres.Add(new ClaseConRenombres { Id = "3", Nombre = "Item3", Edad = 30, Activo = true });
        
        // =========================================
        // NUEVOS: Puntos3D
        // =========================================
        Puntos3D.Add(new Vector3 { X = 0, Y = 0, Z = 0 });
        Puntos3D.Add(new Vector3 { X = 1, Y = 1, Z = 1 });
        Puntos3D.Add(new Vector3 { X = 2, Y = 4, Z = 8 });
        
        // =========================================
        // NUEVOS: PaletaColores
        // =========================================
        PaletaColores["rojo"] = new ColorRGBA { R = 255, G = 0, B = 0, A = 255 };
        PaletaColores["verde"] = new ColorRGBA { R = 0, G = 255, B = 0, A = 255 };
        PaletaColores["azul"] = new ColorRGBA { R = 0, G = 0, B = 255, A = 255 };
        PaletaColores["transparente"] = new ColorRGBA { R = 128, G = 128, B = 128, A = 128 };
        
        ListaPuntos.Add(new Punto2D { X = 1, Y = 2 });
        ListaPuntos.Add(new Punto2D { X = 3, Y = 4 });
        ListaPuntos.Add(new Punto2D { X = 5, Y = 6 });
        
        DiccionarioPuntos["origen"] = new Punto2D { X = 0, Y = 0 };
        DiccionarioPuntos["medio"] = new Punto2D { X = 50, Y = 50 };
        DiccionarioPuntos["final"] = new Punto2D { X = 100, Y = 100 };
        
        Rectangulos.Add(new Rectangulo { Ancho = 800, Alto = 600 });
        Rectangulos.Add(new Rectangulo { Ancho = 1024, Alto = 768 });
        Rectangulos.Add(new Rectangulo { Ancho = 3840, Alto = 2160 });
        
        Personas.Add(new PersonaExterna { Nombre = "Ana", Edad = 25 });
        Personas.Add(new PersonaExterna { Nombre = "Luis", Edad = 35 });
        Personas.Add(new PersonaExterna { Nombre = "Marta", Edad = 28 });
        
        DiccionarioPolimorficoEntidades["arma1"] = new Arma() { Id = "a1", Nombre = "Espada", Cantidad = 1, Danio = 50 };
        DiccionarioPolimorficoEntidades["arma2"] = new Arma() { Id = "a2", Nombre = "Hacha", Cantidad = 1, Danio = 75 };
        DiccionarioPolimorficoEntidades["pocion1"] = new Pocion() { Id = "p1", Nombre = "Vida", Cantidad = 5, Curacion = 25.5f };
        DiccionarioPolimorficoEntidades["pocion2"] = new Pocion() { Id = "p2", Nombre = "Mana", Cantidad = 3, Curacion = 50f };
        // Diccionario de LowItem con polimorfismo
        DiccionarioPolimorficoItems["espada_roja"] = new Arma() { Id = "itm1", Nombre = "Espada Roja", Cantidad = 1, Danio = 55 };
        DiccionarioPolimorficoItems["espada_azul"] = new Arma() { Id = "itm2", Nombre = "Espada Azul", Cantidad = 1, Danio = 45 };
        DiccionarioPolimorficoItems["pocion_pequena"] = new Pocion() { Id = "itm3", Nombre = "Pocion Pequeña", Cantidad = 5, Curacion = 10f };
        // Diccionario de Personaje con polimorfismo
        DiccionarioPolimorficoPersonajes["guerrero_ragnar"] = new Guerrero() { Id = "g1", Nombre = "Ragnar", Nivel = 10, Profesion = "Tanque", Fuerza = 999 };
        DiccionarioPolimorficoPersonajes["mago_ezren"] = new Mago() { Id = "m1", Nombre = "Ezren", Nivel = 14, Profesion = "Hechicero", Mana = 400 };
        DiccionarioPolimorficoPersonajes["dragon_aldur"] = new Dragon() { Id = "d1", Nombre = "Aldur", Nivel = 99, Salvaje = true, Fuego = 999.9f };
        // Diccionario con object como key y value (máximo polimorfismo)
        DiccionarioPolimorficoObject["string_key"] = "valor string";
        DiccionarioPolimorficoObject[42] = 123;
        DiccionarioPolimorficoObject[new Vector3 { X = 1, Y = 2, Z = 3 }] = new Arma() { Id = "obj_arma", Nombre = "Arma en diccionario", Cantidad = 1, Danio = 100 };
        // Diccionario con object como value
        DiccionarioPolimorficoObjectValue["entero"] = 999;
        DiccionarioPolimorficoObjectValue["string"] = "texto de ejemplo";
        DiccionarioPolimorficoObjectValue["arma"] = new Arma() { Id = "a_obj", Nombre = "Arma objeto", Cantidad = 1, Danio = 80 };
        DiccionarioPolimorficoObjectValue["pocion"] = new Pocion() { Id = "p_obj", Nombre = "Pocion objeto", Cantidad = 3, Curacion = 30 };
        DiccionarioPolimorficoObjectValue["vector"] = new Vector3 { X = 10, Y = 20, Z = 30 };
    }
}
#endregion
#region TESTER
#region TESTER

public static class LowFileTest
{
    public static void Ejecutar()
    {
        if (!Directory.Exists("test")) Directory.CreateDirectory("test");
        LowFile.default_info = InfoSerializer.AutoRegister();

        var obj = new ArchivoTest();
        obj.construir();

        // Serializar -> Deserializar -> Re-serializar
        string ser1 = LowFile.Serialize(obj);
        File.WriteAllText("test/test_original.yaml", ser1);

        var reconstruido = LowFile.Deserialize<ArchivoTest>(ser1);
        string ser2 = LowFile.Serialize(reconstruido);
        File.WriteAllText("test/test_reconstruido.yaml", ser2);

        Console.WriteLine($"Original: {ser1.Length} chars | Reconstruido: {ser2.Length} chars");

        // Comparar línea a línea y parar en la primera diferencia
        var a = ser1.Replace("\r", "").Split('\n');
        var b = ser2.Replace("\r", "").Split('\n');

        for (int i = 0; i < Math.Max(a.Length, b.Length); i++)
        {
            string o = i < a.Length ? a[i] : "[EOF]";
            string r = i < b.Length ? b[i] : "[EOF]";
            if (o == r) continue;

            Console.WriteLine($"⚠️ DIFERENCIA EN LÍNEA {i + 1}:");
            Console.WriteLine($"   O: '{o}'");
            Console.WriteLine($"   R: '{r}'");
            File.WriteAllLines("test/diferencias.txt", new[] { $"LÍNEA {i + 1}:", $"  O: {o}", $"  R: {r}" });
            return;
        }

        Console.WriteLine("✅ LOS ARCHIVOS SON IDÉNTICOS");
    }
}

public class Node
{
    public string Name;
    public Node Next;
}

#endregion
#endregion