using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Peg;


/// <summary>
/// 
/// </summary>
public static class TypeConstructorHelper
{
    public readonly static BindingFlags ConstructorBindFlags = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy;

    /// <summary>
    /// Returns a list containing all constructors for a type including those provided in the base class.
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static List<ConstructorInfo> FindAllConstructors(Type type, bool includeBaseClasses)
    {
        //get all constructors in the entire heirarchy
        List<ConstructorInfo> ctors = new(type.GetConstructors(ConstructorBindFlags));
        foreach (var baseType in TypeHelper.GetBaseClasses(type, false))
            ctors.AddRange(baseType.GetConstructors(ConstructorBindFlags));

        return ctors;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static ConstructorInfo FindBestConstructor(Type type, Type[] supportedParameterTypes)
    {
        var ctors = FindAllConstructors(type, true).OrderByDescending(x =>
        {
            var par = x.GetParameters();
            return par == null ? 0 : par.Length;
        });

        foreach (var ctor in ctors)
        {
            bool validCtor = true;
            foreach (var p in ctor.GetParameters())
            {
                //Debug.Log($"DataType is '{type.Name}' and Param name is '{p.Name}' and the type is '{p.ParameterType.Name}'.");
                if (!supportedParameterTypes.Contains(p.ParameterType) &&
                    !TypeHelper.IsSameOrSubclass(typeof(UnityEngine.Object), p.ParameterType))
                {
                    validCtor = false;
                    break;
                }
            }

            if (validCtor)
                return ctor;
        }

        return null;
    }


    /// <summary>
    /// 
    /// </summary>
    /// <param name="types"></param>
    /// <returns></returns>
    public static IEnumerable<Type> GetTypesWithValidConstructors(Type[] types, Type[] supportedParameterTypes)
    {
        return types.Where(type => FindBestConstructor(type, supportedParameterTypes) != null);
    }
}