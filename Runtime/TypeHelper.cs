using UnityEngine;
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Assertions;

namespace Toolbox
{
    /// <summary>
    /// 
    /// </summary>
	public static class TypeHelper
	{
        /// <summary>
        /// Helper class for storing an object and a
        /// path to one of its properties or fields.
        /// </summary>
        [Serializable]
        public class BindingMap
        {
            public string SourceKey;
            public UnityEngine.Object DestObj;
            public string Path;

            //#if UNITY_EDITOR
            //helper stuff for the editor UI
            public int KeyIndex;
            public int PathIndex;
            //#endif

            protected BindingMap()
            {

            }
            
            protected BindingMap(string key, int keyIndex, UnityEngine.Object destObj, string path)
            {
                SourceKey = key;
                DestObj = destObj;
                Path = path;
                KeyIndex = 0;
                PathIndex = 0;
            }

            public static T Create<T>(string key, int keyIndex, UnityEngine.Object destObj, string path) where T : BindingMap
            {
                T val = Activator.CreateInstance<T>();
                val.SourceKey = key;
                val.DestObj = destObj;
                val.Path = path;
//#if UNITY_EDITOR
                val.KeyIndex = 0;
                val.PathIndex = 0;
//#endif
                return val;
            }

            /// <summary>
            /// Pulls the source value from the supplied OnObtainBoundResult callback, applies
            /// all curves to it and then returns the resulting value.
            /// </summary>
            /// <param name="binding"></param>
            public (T, MemberInfo)? GetBoundValue<T>()
            {
                if (DestObj == null ||string.IsNullOrEmpty(Path) || string.IsNullOrEmpty(SourceKey))
                    return null;

                var t = DestObj.GetType();
                var member = t.GetMember(Path, BindingFlags.Public | BindingFlags.Instance);
                for (int i = 0; i < member.Length; i++)
                {
                    PropertyInfo prop = member[i] as PropertyInfo;
                    if (prop != null)
                    {
                        object value = prop.GetValue(DestObj, null);
                        return ((T)value, member[i]);
                    }

                    FieldInfo field = member[i] as FieldInfo;
                    if (field != null)
                    {
                        T value = (T)field.GetValue(DestObj);
                        return (value, member[i]);
                    }

                }

                Debug.LogError("There is no binding state for '" + SourceKey == null ? "null" : SourceKey + " at the dest path '" + Path == null ? "null" : Path + "'.");
                return null;
            }
            
        }

        //cached results for type-finding methods
        static Assembly[] _LoadedAssemblies;
        #if UNITY_EDITOR
        static Assembly[] _LoadedEditorAssemblies;
        #endif

        static Dictionary<string, Type> LoadedTypes = new Dictionary<string, Type>();
        static Dictionary<Type, Type> BaseType = new Dictionary<Type, Type>();

        /// <summary>
        /// Runtime assemblies.
        /// </summary>
        public static Assembly[] LoadedAssemblies
        {
            get
            {
                if (_LoadedAssemblies == null)
                    _LoadedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                        .Where( (asm) => !asm.GetName().Name.Contains("Editor") && !asm.GetName().Name.Contains("firstpass") && !asm.IsDynamic)
                        .ToArray();
                return _LoadedAssemblies;
            }
        }

        #if UNITY_EDITOR
        /// <summary>
        /// Returns all assemblies, including ones available in the editor.
        /// WARNING! This method is compiled out during runtime so do not 
        /// use it outside of editor-only use!
        /// </summary>
        public static Assembly[] LoadedEditorAssemblies
        {
            get
            {
                if (_LoadedEditorAssemblies == null)
                    _LoadedEditorAssemblies = AppDomain.CurrentDomain.GetAssemblies();
                return _LoadedAssemblies;
            }
        }
        #endif

        /// <summary>
        /// Returns a list of all availables types that have any of the given attributes.
        /// </summary>
        /// <param name="attributes"></param>
        /// <returns></returns>
        public static Type[] GetTypesWithAnyAttributes(params Type[] attributes)
        {
            if (attributes == null || attributes.Length < 1) return new Type[0];
            var attrs = attributes.Where((a) => IsSameOrSubclass(typeof(Attribute), a));
            if (attrs == null || attrs.Count() < 0) return new Type[0];


            var types = new List<Type>();
            foreach (var asm in LoadedAssemblies)
            {
                Type[] exportedTypes;
                try
                {
                    exportedTypes = asm.GetExportedTypes();
                }
                catch (ReflectionTypeLoadException)
                {
                    Debug.LogWarning(string.Format("Ignoring the following assembly due to type-loading errors: {0}", asm.FullName));
                    continue;
                }

                for (int i = 0; i < exportedTypes.Length; i++)
                {
                    Type type = exportedTypes[i];
                    var custom = type.GetCustomAttributes(true);// as Type[];
                    if (custom != null && attrs.Intersect(custom).Count() > 0)
                        types.Add(type);
                }
            }

            return types.ToArray();
        }

        /// <summary>
        /// Returns a list of all availables types that have any of the given attributes.
        /// </summary>
        /// <param name="attributes"></param>
        /// <returns></returns>
        public static Type[] GetTypesWithAttribute(Type attribute)
        {
            var types = new List<Type>();
            if (!IsSameOrSubclass(typeof(Attribute), attribute))
                return types.ToArray();

            foreach (var asm in LoadedAssemblies)
            {
                Type[] exportedTypes;
                try
                {
                    exportedTypes = asm.GetExportedTypes();
                }
                catch (ReflectionTypeLoadException)
                {
                    Debug.LogWarning(string.Format("Ignoring the following assembly due to type-loading errors: {0}", asm.FullName));
                    continue;
                }

                for (int i = 0; i < exportedTypes.Length; i++)
                {
                    Type type = exportedTypes[i];
                    var custom = type.GetCustomAttribute(attribute);
                    if (custom != null)
                        types.Add(type);
                }
            }

            return types.ToArray();
        }

        /// <summary>
        /// Returns the children classes of the supplied type.
        /// </summary>
        /// <param name="baseType">The base type.</param>
        /// <returns>The children classes of baseType.</returns>
        public static System.Type[] GetDerivedTypes(System.Type baseType, bool includeAssignable = false, bool includeAbstract = false, bool includeInterface = true)
        {
            // Create the derived type list
            var derivedTypes = new List<System.Type>();

            foreach (Assembly asm in LoadedAssemblies)
            {
                // Get types
                Type[] exportedTypes;

                try
                {
                    exportedTypes = asm.GetExportedTypes();
                }
                catch (ReflectionTypeLoadException)
                {
                    Debug.LogWarning(string.Format("Ignoring the following assembly due to type-loading errors: {0}", asm.FullName));
                    continue;
                }

                for (int i = 0; i < exportedTypes.Length; i++)
                {
                    // Get type
                    Type type = exportedTypes[i];
                    // The type is a subclass of baseType?
                    if ( (includeAbstract || !type.IsAbstract) &&
                         (includeInterface || !type.IsInterface) &&
                         type.IsSubclassOf(baseType) && type.FullName != null)
                    {
                        derivedTypes.Add(type);
                    }
                    else if ((includeAbstract || !type.IsAbstract) &&
                             (includeInterface || !type.IsInterface) &&
                              IsSubclassOfRawGeneric(baseType, type) && type.FullName != null)
                    {
                        derivedTypes.Add(type);
                    }
                    else if ((includeAbstract || !type.IsAbstract) &&
                             (includeInterface || !type.IsInterface) &&
                              includeAssignable && baseType.IsAssignableFrom(type))
                        derivedTypes.Add(type);
                }
            }
            derivedTypes.Sort((Type o1, Type o2) => o1.ToString().CompareTo(o2.ToString()));
            return derivedTypes.ToArray();
        }

        /// <summary>
        /// Returns the System.Type of the supplied name.
        /// <param name="name">The type name.</param>
        /// <returns>The System.Type of the supplied string.</returns>
        /// </summary>
        public static Type GetType(string name)
        {
            // Try to get the type
            Type type = null;
            if (LoadedTypes.TryGetValue(name, out type))
                return type;

            // Try C# scripts
            type = Type.GetType(name + ",Assembly-CSharp-firstpass") ?? Type.GetType(name + ",Assembly-CSharp");

            // Try AppDomain
            if (type == null)
            {
                foreach (Assembly asm in LoadedAssemblies)
                {
                    type = asm.GetType(name);
                    if (type != null)
                        break;
                }
            }

            // Add type
            LoadedTypes.Add(name, type);
            return type;
        }

        /// <summary>
        /// Returns the base class System.Type of the supplied type.
        /// The base type if it's the last abstract type in the class hierarchy.
        /// <param name="targetType">The target type.</param>
        /// <returns>The base class System.Type.</returns>
        /// </summary>
        public static Type GetBaseType(Type targetType)
        {
            // Try to get the type
            Type type = null;
            if (BaseType.TryGetValue(targetType, out type))
                return type;

            {
                System.Type typeIterator = targetType;
                while (typeIterator != typeof(object))
                {
                    if (typeIterator.IsAbstract)
                        type = typeIterator;
                    typeIterator = typeIterator.BaseType;
                }
            }

            if (type == null)
                type = targetType;

            // Add type
            BaseType.Add(targetType, type);

            return type;
        }

		/// <summary>
        /// 
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
		public static object GetDefault(Type type)
		{
            // If no Type was supplied, if the Type was a reference type, or if the Type was a System.Void, return null
            if (type == null || !type.IsValueType || type == typeof(void))
				return null;
			
			// If the supplied Type has generic parameters, its default value cannot be determined
			if (type.ContainsGenericParameters)
				throw new ArgumentException(
					"{" + MethodInfo.GetCurrentMethod() + "} Error:\n\nThe supplied value type <" + type +
					"> contains generic parameters, so the default value cannot be retrieved");
			
			// If the Type is a primitive type, or if it is another publicly-visible value type (i.e. struct/enum), return a 
			//  default instance of the value type
			if (type.IsPrimitive || !type.IsNotPublic)
			{
				try
				{
					return Activator.CreateInstance(type);
				}
				catch (Exception e)
				{
					throw new ArgumentException(
						"{" + MethodInfo.GetCurrentMethod() + "} Error:\n\nThe Activator.CreateInstance method could not " +
						"create a default instance of the supplied value type <" + type +
						"> (Inner Exception message: \"" + e.Message + "\")", e);
				}
			}
			
			// Fail with exception
			throw new ArgumentException("{" + MethodInfo.GetCurrentMethod() + "} Error:\n\nThe supplied value type <" + type + 
			                            "> is not a publicly-visible type, so the default value cannot be retrieved");
		}

		/// <summary>
		/// Returns true if 'generic' is derived from 'known' without consideration for generic type parameters.
        /// EX: IsSubclassOfRawGeneric(List;lt;gt, List;ltstring;gt) would return true.
		/// </summary>
		/// <returns><c>true</c> if is subclass of raw generic the specified generic known; otherwise, <c>false</c>.</returns>
		/// <param name="generic">Generic.</param>
		/// <param name="known">Known.</param>
		public static bool IsSubclassOfRawGeneric(Type generic, Type known) 
		{
			while(known != null && known != typeof(object)) 
			{
				var cur = known.IsGenericType ? known.GetGenericTypeDefinition() : known;
				if (generic == cur) return true;
				known = known.BaseType;
			}
			return false;
		}
		
		/// <summary>
		/// Returns the generic type without consideration for type parameters.
		/// </summary>
		/// <returns>The raw generic base class.</returns>
		/// <param name="generic">Generic.</param>
		/// <param name="known">Known.</param>
		public static Type FindRawGenericBaseClass(Type generic, Type known) 
		{
			while(known.BaseType != null && known.BaseType != typeof(object)) 
			{
				var cur = known.BaseType.IsGenericType ? known.BaseType.GetGenericTypeDefinition() : known.BaseType;
				if (generic == cur) return known.BaseType;
				known = known.BaseType;
			}
			
			return null;
		}

        /// <summary>
        /// Some unity types can appear to have their references be null but are in fact
        /// using a place-holder null-like object. This method can help compare references types
        /// to both situations.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static bool IsReferenceNull(object obj)
        {
            if (obj == null) return true;
            else if (obj.Equals(null)) return true;
            else return false;
        }

        /// <summary>
        /// Returns true if the typeToCheck is the same or a sub-class of baseCls.
        /// </summary>
        /// <param name="baseCls"></param>
        /// <param name="typeToCheck"></param>
        /// <returns></returns>
        public static bool IsSameOrSubclass(Type baseCls, Type typeToCheck)
        {
            //Debug.Log(typeToCheck.Name + " is same or sub of " + baseCls.Name);
            return (typeToCheck == baseCls || typeToCheck.IsSubclassOf(baseCls));
        }

        /// <summary>
        /// Returns true if the two classes are either the same or one is the base class of the other.
        /// </summary>
        /// <param name="cls1"></param>
        /// <param name="cls2"></param>
        /// <returns></returns>
        public static bool AreClasesInterchangeable(Type cls1, Type cls2)
        {
            if(cls1 == cls2) return true;
            else if(cls1.IsSubclassOf(cls2)) return true;
            else return cls2.IsSubclassOf(cls1);
        }

        /// <summary>
        /// Enumerates all subclasses of a type.
        /// </summary>
        /// <param name="baseType"></param>
        /// <returns></returns>
        public static Type[] FindSubClasses(Type baseType, Assembly assembly)
        {
            return assembly.GetTypes().Where(t => t.IsSubclassOf(baseType)).ToArray();
            //return assembly.GetTypes().Where(t => t.IsAssignableFrom(baseType)).ToArray();
        }

        /// <summary>
        /// Enumerates all base classes of a type.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="includeSelf"></param>
        /// <returns></returns>
        public static IEnumerable<Type> GetBaseClasses(Type type, bool includeSelf = false)
        {
            if (!(type == null) && !(type.BaseType == null))
            {
                if (includeSelf)
                {
                    yield return type;
                }

                Type current = type.BaseType;
                while (current != null)
                {
                    yield return current;
                    current = current.BaseType;
                }
            }
        }

        /// <summary>
        /// Returns all subclasses of a type found in this project.
        /// </summary>
        /// <param name="baseType"></param>
        /// <returns></returns>
        public static Type[] FindSubClasses(Type baseType)
        {
            List<Type> types = new List<Type>(20);
            var allAsm = LoadedAssemblies;
            for (int i = 0; i < allAsm.Length; i++ )
                types.AddRange(FindSubClasses(baseType, allAsm[i]));
            
            return types.ToArray<Type>();
        }

        /// <summary>
        /// Returns all subclasses that implement an interface found in this project.
        /// </summary>
        /// <param name="baseType"></param>
        /// <param name="assembly"></param>
        /// <returns></returns>
        public static Type[] FindSubClassesWithDefaultConstructors(Type baseType)
        {
            List<Type> types = new List<Type>(20);
            var allAsm = LoadedAssemblies;
            for (int i = 0; i < allAsm.Length; i++)
            {
                Type sub;
                var subTypes = FindSubClasses(baseType, allAsm[i]);
                for (int j = 0; j < subTypes.Length; j++)
                {
                    sub = subTypes[j];
                    var cons = sub.GetConstructors();
                    if (cons != null && cons.Length > 0)
                    {
                        var pars = cons[0].GetParameters();
                        if (pars == null || pars.Length < 1)
                            types.Add(sub);
                    }

                }

            }
            return types.ToArray<Type>();
        }

        /// <summary>
        /// Returns all subclasses that implement an interface.
        /// </summary>
        /// <param name="baseType"></param>
        /// <param name="assembly"></param>
        /// <returns></returns>
        public static Type[] FindInterfaceImplementations(Type baseType, Assembly assembly)
        {
            //don't forget that we have concrete classes and interfaces with the same name. Let's only get the concrete, non-generic classes
            return assembly.GetTypes().Where(t => (t.GetInterfaces().Contains(baseType) && t.IsClass && !t.IsGenericType && !t.IsAbstract)).ToArray();
        }

        /// <summary>
        /// Returns all subclasses that implement an interface found in this project.
        /// </summary>
        /// <param name="baseType"></param>
        /// <param name="assembly"></param>
        /// <returns></returns>
        public static Type[] FindInterfaceImplementations(Type baseType)
        {
            List<Type> types = new List<Type>(20);
            var allAsm = LoadedAssemblies;
            for (int i = 0; i < allAsm.Length; i++)
                types.AddRange(FindInterfaceImplementations(baseType, allAsm[i]));
            return types.ToArray<Type>();
        }

        /// <summary>
        /// Determines if a type implements exactly the given interface type without consideration for inheritance.
        /// </summary>
        /// <param name="t"></param>
        /// <returns><c>true</c> if the type implements the given interface, <c>false</c>if it does not or if it implements another interface derived from the given interface.</returns>
        public static bool ImplementsInterfaceAtGivenType(Type t, Type interfaceType)
        {
            if (t.BaseType != null && ImplementsInterfaceAtGivenType(t.BaseType, interfaceType))
                return false;

            foreach (var intf in t.GetInterfaces())
            {
                if (ImplementsInterfaceAtGivenType(intf, interfaceType))
                    return false;
            }
            return t.GetInterfaces().Any(i => i == interfaceType);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="t"></param>
        /// <param name="interfaceType"></param>
        /// <returns></returns>
        public static bool ImplementsInterface(Type t, Type interfaceType)
        {
            Assert.IsTrue(interfaceType.IsInterface);
            return t.GetInterfaces().Any(x => x == interfaceType);
        }

        /// <summary>
        /// Returns all subclasses that implement an interface found in this project.
        /// </summary>
        /// <param name="baseType"></param>
        /// <param name="assembly"></param>
        /// <returns></returns>
        public static Type[] FindInterfaceImplementationsWithDefaultConstructors(Type baseType)
        {
            List<Type> types = new List<Type>(20);
            var allAsm = LoadedAssemblies;
            for (int i = 0; i < allAsm.Length; i++)
            {
                Type sub;
                var subTypes = FindInterfaceImplementations(baseType, allAsm[i]);
                for(int j = 0; j < subTypes.Length; j++)
                {
                    sub = subTypes[j];
                    var cons = sub.GetConstructors();
                    if(cons != null && cons.Length > 0)
                    {
                        var pars = cons[0].GetParameters();
                        if(pars == null || pars.Length < 1)
                            types.Add(sub);
                    }
                    
                }
                
            }
            return types.ToArray<Type>();
        }

        /// <summary>
        /// Attempts to convert the given member to a field or property and get its stored value.
        /// </summary>
        /// <param name="info"></param>
        /// <param name="context"></param>
        /// <returns>The stored value or null if it is not a field or a readable property.</returns>
        public static object GetValue(this MemberInfo info, object context)
        {
            Assert.IsNotNull(context);

            FieldInfo f = info as FieldInfo;
            if (f != null) return f.GetValue(context);
            else
            {
                PropertyInfo p = info as PropertyInfo;
                if (p != null && p.CanRead) return p.GetValue(context, null);
            }

            return null;
        }

        /// <summary>
        /// Attempts to set the value for the given member as a field or property.
        /// </summary>
        /// <param name="info"></param>
        /// <param name="context"></param>
        /// <returns>The stored value or null if it is not a field or a readable property.</returns>
        public static void SetValue(this MemberInfo info, object context, object value)
        {
            Assert.IsNotNull(context);

            FieldInfo f = info as FieldInfo;
            if (f != null) f.SetValue(context, value);
            else
            {
                PropertyInfo p = info as PropertyInfo;
                if (p != null && p.CanRead) p.SetValue(context, value, null);
            }
        }

        /// <summary>
        /// Given a MemberInfo, this will return the field or property that it represents.
        /// If the member is not a field or property, null is returned instead.
        /// </summary>
        /// <param name="info"></param>
        /// <returns></returns>
        public static Type DataType(this MemberInfo info)
        {
            FieldInfo f = info as FieldInfo;
            if (f != null) return f.FieldType;
            else
            {
                PropertyInfo p = info as PropertyInfo;
                if (p != null) return p.PropertyType;
            }
            return null;
        }

    }


    
}
