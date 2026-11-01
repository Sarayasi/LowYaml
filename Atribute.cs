namespace LowYaml;

[System.AttributeUsage(System.AttributeTargets.All, Inherited = false, AllowMultiple = true)]
public class LowIgnore : System.Attribute{}
public interface LowParse{ // solo construir el cuerpo
    public string ToLow();
    public void LowTo(string line);
}


public interface LowParse<T>{ // solo construir el cuerpo
    public string ToLow(T ob);
    public T LowTo(string line);
}


[System.AttributeUsage(System.AttributeTargets.Field, Inherited = false, AllowMultiple = true)]
public class LowRename : System.Attribute
{
    public string Nombre;
    public LowRename(string nombre)
    {
        Nombre = nombre;   
    }
}


[System.AttributeUsage(System.AttributeTargets.Field, Inherited = false, AllowMultiple = true)]
public class LowComment : System.Attribute
{
    public string Msg;
    public LowComment(string Msg)
    {
        this.Msg = Msg;   
    }
}
