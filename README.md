# LowFile

Serializador de objetos C# a un formato de texto **estilo YAML**, basado en indentación, legible y editable a mano. Un solo método para guardar y otro para cargar, sin esquemas ni archivos de configuración aparte.

```csharp
string texto = LowFile.Serialize(miObjeto);
var cargado  = LowFile.Deserialize<Archivo>(texto);
```
<img width="1680" height="773" alt="image" src="https://github.com/user-attachments/assets/6c50c00a-9430-43f7-8e15-1f7a4e95651c" />

## Características

- Serializa y deserializa **campos** (públicos y privados) por reflexión.
- Tipos básicos, enums, `Nullable<T>`, `DateTime`, `DateOnly`, `TimeOnly`, `Guid`, `Uri`, `Version`, `decimal`, etc.
- `List<T>`, arrays de una dimensión, `HashSet<T>` y `Dictionary<K,V>`.
- Strings multilínea con bloques `|`.
- **Polimorfismo** con etiquetas `!Tipo`.
- Formato propio por tipo con `LowParse` (en la clase) o `LowParse<T>` (parser externo).
- Atributos `[LowIgnore]`, `[LowRename]` y `[LowComment]`.
- Los campos que faltan en el archivo conservan el valor que les da el constructor, y los campos desconocidos se ignoran.
- Las referencias circulares se detectan y se escriben como `null` en lugar de provocar un desbordamiento de pila.

## Instalación

Por ahora no hay paquete NuGet. Copia los archivos de la librería a tu proyecto (`LowFile.cs`, `LowWriter.cs`, `LowReader.cs`, `InfoSerializer.cs` y los archivos con los atributos e interfaces) y usa el namespace `LowYaml`.

## Inicio rápido

```csharp
using System.Collections.Generic;
using System.IO;
using LowYaml;

public class Config
{
    public string Nombre = "Sensor 1";
    public int IntervaloMs = 500;
    public bool Activo = true;
    public List<string> Tags = new() { "temperatura", "humedad" };
    public Dictionary<string, int> Umbrales = new() { ["min"] = 10, ["max"] = 90 };

    [LowIgnore]
    public string Cache = "no se guarda";
}

var cfg = new Config();

// Guardar
File.WriteAllText("config.low", LowFile.Serialize(cfg));

// Cargar
var cargado = LowFile.Deserialize<Config>(File.ReadAllText("config.low"));
```

Contenido de `config.low`:

```yaml
Nombre: Sensor 1
IntervaloMs: 500
Activo: true
Tags:
  - temperatura
  - humedad
Umbrales:
  -
    key: min
    value: 10
  -
    key: max
    value: 90
# creado con lowfile
```

> `Deserialize<T>` exige que `T` tenga constructor sin parámetros. Lo mismo vale para los objetos anidados.

## Guía del formato

### Indentación y comentarios

Cada nivel son **2 espacios**. Las líneas que empiezan por `#` son comentarios y se ignoran al leer.

### Objetos anidados

```yaml
Jugador:
  Nombre: Ana
  Posicion:
    X: 1
    Y: 2
```

### Strings

```yaml
Simple: Hola mundo
Multilinea: |
  línea 1
  línea 2
```

Los tabuladores dentro de un string se convierten en 2 espacios al escribir.

### Listas, arrays y HashSet

```yaml
Numeros:
  - 1
  - 2
  - 3
```

### Diccionarios

Se guardan como una lista de pares `key` / `value`:

```yaml
Scores:
  -
    key: juan
    value: 10
  -
    key: ana
    value: -5
```

### Nulls

Con `IncludeNulls = true` (valor por defecto) los nulls se escriben explícitamente:

```yaml
ItemNulo: null
EnteroNulo: null
```

Con `IncludeNulls = false` se omiten los nulls y las colecciones vacías.

## Atributos

| Atributo | Dónde | Efecto |
|---|---|---|
| `[LowIgnore]` | campo o clase | No se serializa. |
| `[LowRename("nombre")]` | campo | Usa otro nombre en el archivo. |
| `[LowComment("texto")]` | campo o clase | Escribe `# texto` antes del campo. |

```csharp
public class Usuario
{
    [LowRename("nombre_completo")]
    [LowComment("Nombre visible en la interfaz")]
    public string Nombre = "Juan Perez";

    [LowIgnore]
    public string Token = "secreto";
}
```

```yaml
# Nombre visible en la interfaz
nombre_completo: Juan Perez
```

## Polimorfismo

Si el tipo real de un campo, de un item de lista o de un valor de diccionario es una subclase del tipo declarado, se escribe la etiqueta `!NombreCompletoDelTipo`:

```csharp
namespace Demo;

public abstract class Forma { }
public class Circulo : Forma { public float Radio = 2; }
public class Rect : Forma { public float Ancho = 3, Alto = 4; }

public class Dibujo { public List<Forma> Formas = new() { new Circulo(), new Rect() }; }
```

```yaml
Formas:
  - !Demo.Circulo
    Radio: 2
  - !Demo.Rect
    Ancho: 3
    Alto: 4
```

Al leer, el tipo se busca por nombre en los ensamblados cargados. Si no se encuentra o no hereda del tipo esperado, lanza una excepción.

## Formato propio por tipo

### `LowParse` (en la propia clase)

Para tipos que quieres escribir en una sola línea:

```csharp
using System.Globalization;

public class Vector3 : LowParse
{
    public float X, Y, Z;

    public string ToLow() => string.Create(CultureInfo.InvariantCulture, $"{X};{Y};{Z}");

    public void LowTo(string line)
    {
        var p = line.Split(';');
        X = float.Parse(p[0], CultureInfo.InvariantCulture);
        Y = float.Parse(p[1], CultureInfo.InvariantCulture);
        Z = float.Parse(p[2], CultureInfo.InvariantCulture);
    }
}
```

```yaml
Posicion: 10.5;20.3;5.7
```

> Usa `CultureInfo.InvariantCulture` en tus `ToLow` / `LowTo`. Si no, el archivo cambiará según la configuración regional del equipo.

### `LowParse<T>` (parser externo)

Para tipos que no puedes o no quieres modificar:

```csharp
public class Punto2D { public int X, Y; }

public class Punto2DParser : LowParse<Punto2D>
{
    public string ToLow(Punto2D p) => $"{p.X}:{p.Y}";

    public Punto2D LowTo(string line)
    {
        var p = line.Split(':');
        return new Punto2D { X = int.Parse(p[0]), Y = int.Parse(p[1]) };
    }
}
```

Regístralo a mano:

```csharp
var info = new InfoSerializer();
info.RegisterParser(new Punto2DParser());
LowFile.default_info = info;
```

O deja que se descubran automáticamente en un ensamblado:

```csharp
LowFile.default_info = InfoSerializer.AutoRegister(typeof(Program).Assembly);
```

```yaml
Origen: 10:20
```

> Pasa el ensamblado explícitamente. Si usas la librería como DLL aparte y llamas a `AutoRegister()` sin argumentos, buscaría los parsers en el ensamblado equivocado.

## API

| Método | Descripción |
|---|---|
| `LowFile.Serialize(object ob, InfoSerializer info = null)` | Devuelve el texto. |
| `LowFile.Deserialize<T>(string text, InfoSerializer info = null)` | Crea un `T` a partir del texto. |
| `LowFile.Deserialize(string text, Type t, InfoSerializer info = null)` | Igual, con el tipo en tiempo de ejecución. |
| `LowFile.default_info` | `InfoSerializer` global que se usa cuando no pasas uno. |

### `InfoSerializer`

| Miembro | Descripción |
|---|---|
| `IncludeNulls` | Si es `true`, escribe los nulls. Por defecto `true`. |
| `InfoSerializer.IncludeNullsDefault` | Valor inicial de `IncludeNulls` para nuevas instancias. |
| `RegisterParser<T>(LowParse<T>)` | Registra un parser externo. |
| `AutoRegisterParsers(Assembly)` | Registra todos los `LowParse<T>` de un ensamblado. |
| `InfoSerializer.Debug`, `LowReader.Debug` | Traza por consola del escritor y del lector. |

## Test de ida y vuelta

La forma más rápida de comprobar que un tipo se guarda y se carga bien es serializar, deserializar, volver a serializar y comparar los dos textos:

```csharp
string t1 = LowFile.Serialize(obj);
var copia = LowFile.Deserialize<MiClase>(t1);
string t2 = LowFile.Serialize(copia);

Console.WriteLine(t1 == t2 ? "OK" : "Hay diferencias");
```

## Limitaciones conocidas

- Solo se serializan **campos**, no propiedades. Para propiedades automáticas usa campos o `[field: ...]` según tu caso.
- Los arrays multidimensionales no están soportados.
- Las referencias circulares se escriben como `null`; no se reconstruyen.
- Los strings no pueden comenzar con ( `!` o `|`).
- Usa reflexión intensiva y algo de ram. No se recomienda para AOT o endebidos como arduino o esp32
## Licencia
Apache 2.0
