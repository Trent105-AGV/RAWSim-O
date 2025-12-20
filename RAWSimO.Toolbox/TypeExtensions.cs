using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace RAWSimO.Toolbox;

/// <summary>
/// Introduces some helping type extensions.
/// </summary>
public static class TypeExtensions
{
    /// <summary>
    /// Compares two objects for equality in all simple public field values.
    /// </summary>
    /// <typeparam name="T">The type of the objects to compare.</typeparam>
    /// <param name="self">The object itself.</param>
    /// <param name="to">The object to compare to.</param>
    /// <returns><code>true</code> if both objects have equal values for their simple and public fields, <code>false</code> otherwise.</returns>
    public static bool SimplePublicInstanceFieldsEqual<T>(this T self, T to) where T : class
    {
        if (self != null && to != null)
        {
            var type = typeof(T);
            return type.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetUnderlyingType().IsSimpleType())
                .All(p =>
                {
                    var selfValue = type.GetField(p.Name).GetValue(self);
                    var toValue = type.GetField(p.Name).GetValue(to);
                    if (selfValue != toValue && (selfValue == null || !selfValue.Equals(toValue)))
                        return false;
                    return true;
                });
        }
        return self == to;
    }
    /// <summary>
    /// Compares two objects for equality in all simple public property values.
    /// </summary>
    /// <typeparam name="T">The type of the objects to compare.</typeparam>
    /// <param name="self">The object itself.</param>
    /// <param name="to">The object to compare to.</param>
    /// <returns><code>true</code> if both objects have equal values for their simple and public properties, <code>false</code> otherwise.</returns>
    public static bool SimplePublicInstancePropertiesEqual<T>(this T self, T to) where T : class
    {
        if (self != null && to != null)
        {
            var type = typeof(T);
            return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetUnderlyingType().IsSimpleType())
                .All(p =>
                {
                    var selfValue = type.GetProperty(p.Name).GetValue(self, null);
                    var toValue = type.GetProperty(p.Name).GetValue(to, null);
                    if (selfValue != toValue && (selfValue == null || !selfValue.Equals(toValue)))
                        return false;
                    return true;
                });
        }
        return self == to;
    }
    /// <summary>
    /// Set of simple types.
    /// </summary>
    private static readonly HashSet<Type> _simpleTypes =
    [
        ..new[]
        {
            typeof(string), typeof(decimal), typeof(DateTime), typeof(DateTimeOffset), typeof(TimeSpan), typeof(Guid)
        }
    ];
    /// <summary>
    /// Determine whether a type is simple (String, Decimal, DateTime, etc) 
    /// or complex (i.e. custom class with public properties and methods).
    /// </summary>
    /// <see cref="http://stackoverflow.com/questions/2442534/how-to-test-if-type-is-primitive"/>
    public static bool IsSimpleType(this Type type)
    {
        return
            type.IsValueType ||
            type.IsPrimitive ||
            _simpleTypes.Contains(type) ||
            Convert.GetTypeCode(type) != TypeCode.Object;
    }
    /// <summary>
    /// Gets the underlying type of the member (member must be event, field, method or property).
    /// </summary>
    /// <param name="member">The member to retrieve the underlying type for.</param>
    /// <returns>The underlying type.</returns>
    public static Type GetUnderlyingType(this MemberInfo member)
    {
        return member.MemberType switch
        {
            MemberTypes.Event => ((EventInfo)member).EventHandlerType,
            MemberTypes.Field => ((FieldInfo)member).FieldType,
            MemberTypes.Method => ((MethodInfo)member).ReturnType,
            MemberTypes.Property => ((PropertyInfo)member).PropertyType,
            _ => throw new ArgumentException(
                "Input MemberInfo must be if type EventInfo, FieldInfo, MethodInfo, or PropertyInfo")
        };
    }
}