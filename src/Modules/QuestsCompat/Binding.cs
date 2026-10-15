using System;
using System.Linq.Expressions;
using System.Reflection;
using HarmonyLib;

namespace WildWasteland.Modules.QuestsCompat
{
    internal static class Binding
    {
        internal static Func<object, T> Get<T>(Type t, string name)
        {
            var p = Expression.Parameter(typeof(object), "p"); var f = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            Expression value = f != null ? (Expression)Expression.Field(f.IsStatic ? null : Expression.Convert(p, t), f)
                : Expression.Property(AccessTools.Property(t, name).GetGetMethod(true).IsStatic ? null : Expression.Convert(p, t), AccessTools.Property(t, name));
            return Expression.Lambda<Func<object, T>>(Expression.Convert(value, typeof(T)), p).Compile();
        }
        internal static Action<object, T> Set<T>(Type t, string name)
        {
            var p = Expression.Parameter(typeof(object), "p"); var v = Expression.Parameter(typeof(T), "v"); var f = AccessTools.Field(t, name);
            return Expression.Lambda<Action<object, T>>(Expression.Assign(Expression.Field(f.IsStatic ? null : Expression.Convert(p, t), f), Expression.Convert(v, f.FieldType)), p, v).Compile();
        }
        internal static Action<object> Call(Type t, string name)
        {
            var p = Expression.Parameter(typeof(object), "p"); var m = AccessTools.Method(t, name);
            return Expression.Lambda<Action<object>>(Expression.Call(m.IsStatic ? null : Expression.Convert(p, t), m), p).Compile();
        }
    }
}
