using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace LowYaml;

public class InfoSerializer
{
    public HashSet<object> ReferenceSet { get; } = new(ReferenceEqualityComparer.Instance);

    public sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instance = new();
        public new bool Equals(object a, object b) => ReferenceEquals(a, b);
        public int GetHashCode(object o) => RuntimeHelpers.GetHashCode(o);
    }

    public Stack<string> ReferenceStack { get; } = new();

    public bool IncludeNulls = IncludeNullsDefault;
    public static bool IncludeNullsDefault = true;
    public static bool Debug;

    public static void Print(string msg) { if (Debug) Console.WriteLine(msg); }

    public InfoSerializer() { }

    public Dictionary<Type, object> ExternalParsers { get; } = new();




    public void RegisterParser<T>(LowParse<T> parser) => RegisterParser(typeof(T), parser);

    public void RegisterParser(Type targetType, object parser)
    {
        if (!typeof(LowParse<>).MakeGenericType(targetType).IsInstanceOfType(parser))
            throw new ArgumentException($"El parser no implementa LowParse<{targetType.Name}>");

        ExternalParsers[targetType] = parser;
    }

    public void AddExternalParser(Type t, object parser) => RegisterParser(t, parser);

    public object GetExternalParser(Type t) =>
        ExternalParsers.TryGetValue(t, out var p) ? p : null;

    public bool HasExternalParser(Type t) => ExternalParsers.ContainsKey(t);

    public InfoSerializer AutoRegisterParsers(Assembly assembly = null)
    {
        assembly ??= Assembly.GetCallingAssembly();
        foreach (var (parserType, targetType) in FindParsers(assembly, null))
            TryRegister(parserType, targetType);
        return this;
    }

    public static InfoSerializer AutoRegister(Assembly assembly = null) =>
        new InfoSerializer().AutoRegisterParsers(assembly);

    public void AutoRegisterParsersFromAssembly(Assembly assembly, Func<Type, bool> filter = null)
    {
        foreach (var (parserType, targetType) in FindParsers(assembly, filter))
            TryRegister(parserType, targetType);
    }

    public void AutoRegisterParsersForBaseType<TBase>(Assembly assembly = null)
    {
        assembly ??= Assembly.GetCallingAssembly();
        foreach (var (parserType, targetType) in FindParsers(assembly, null)
                     .Where(x => typeof(TBase).IsAssignableFrom(x.TargetType)))
            TryRegister(parserType, targetType);
    }

    private static IEnumerable<(Type ParserType, Type TargetType)> FindParsers(Assembly assembly, Func<Type, bool> filter) =>
        assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && !t.IsInterface)
            .Where(t => filter == null || filter(t))
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(LowParse<>))
                .Select(i => (ParserType: t, TargetType: i.GetGenericArguments()[0])));

    private void TryRegister(Type parserType, Type targetType)
    {
        try
        {
            var parser = Activator.CreateInstance(parserType);
            RegisterParser(targetType, parser);
        }
        catch (Exception ex){
            Console.WriteLine($"Error con {parserType.Name}: {ex.Message}");
        }
    }

    public static bool IsPrimitiveOrSpecial(Type t) =>  
        t.IsPrimitive || t.IsEnum || t == typeof(string) ||
        t == typeof(DateTime) || t == typeof(DateTimeOffset) ||
        t == typeof(TimeSpan) || t == typeof(Guid) || t == typeof(Uri) ||
        t == typeof(Version) || t == typeof(DateOnly) || t == typeof(TimeOnly) ||
        t == typeof(decimal);

    public static int Indent(string s) => s.TakeWhile(c => c == ' ').Count() / 2;

    public static bool IsDict(Type t) =>
        typeof(IDictionary).IsAssignableFrom(t) ||
        (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Dictionary<,>));
}