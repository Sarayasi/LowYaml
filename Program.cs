// using LowYaml.Test;
// using LowYaml;

// LowFileTest.Ejecutar();

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using LowYaml;


// registrar personalizados y modificadores personalizados de serializadores
LowFile.default_info = InfoSerializer.AutoRegister();
// LowFile.default_info = InfoSerializer.AutoRegister(Assembly.GetExecutingAssembly()); // o otro .dll


// ejemplo
var cfg = new Archivo();
string texto = LowFile.Serialize(cfg);

Console.WriteLine("-------Serializado-------");
Console.WriteLine(texto);

var leido = LowFile.Deserialize<Archivo>(texto);
Console.WriteLine("-------Deserializado-----------");
Console.WriteLine(LowFile.Serialize(leido));
// fin del ejemplo :v









// clasess
public class Archivo
{
    public string Nombre = "Sensor 1";
    public int IntervaloMs = 500;
    public List<Forma> Formas = new() {
        new Circulo(),
        new Rect()                    };
    public Vector3 Posicion = new(10.5f, 20.3f, 5.7f);
    public Punto2D Origen = new() { X = 10, Y = 20 };
    public string vacio = "";
    public string multi = "pepito\n le gusta saltar \nmucho";

    [LowRename("nombre_corto")]
    public string Alias = "S1";
    [LowIgnore]
    public string Cache = "no se guarda";
}


public abstract class Forma { }
public class Circulo : Forma { public float Radio = 2; }
public class Rect : Forma { public float Ancho = 3, Alto = 4; }
public class Punto2D { public int X, Y; }





// clase personalizada
public class Vector3 : LowParse
{
    public Vector3(float x, float y, float z)
    {
        X = x; Y = y; Z = z;
    }
    public Vector3(){} // siempre debe haber un constructor sin parametros para deserializar
    public float X, Y, Z;
    public string ToLow() => FormattableString.Invariant($"{X};{Y};{Z}");
    public void LowTo(string line)
    {
        var p = line.Split(';'); 
        // usar invariat cultura si no quieres lidiar con problemas de fechas o puntos y comas (aunque esto ultimo ya lo aregle creo)
        X = float.Parse(p[0], CultureInfo.InvariantCulture);
        Y = float.Parse(p[1], CultureInfo.InvariantCulture);
        Z = float.Parse(p[2], CultureInfo.InvariantCulture);
    }
}

// modificador de clase
public class Punto2DParser : LowParse<Punto2D>
{
    public string ToLow(Punto2D p) => $"{p.X}:{p.Y}";
    public Punto2D LowTo(string line)
    {
        var p = line.Split(':');
        return new Punto2D { X = int.Parse(p[0]), Y = int.Parse(p[1]) };
    }
}
