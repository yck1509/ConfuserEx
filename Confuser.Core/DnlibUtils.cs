using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;

namespace Confuser.Core
{
    /// <summary>
    /// Provides a set of utility methods about dnlib
    /// </summary>
    public static class DnlibUtils
    {
        /// <summary>
        /// Finds all definitions of interest in a module.
        /// </summary>
        /// <param name="module">The module.</param>
        /// <returns>A collection of all required definitions</returns>
        public static IEnumerable<IDefinition> FindDefinitions(this ModuleDef module)
        {
            yield return module;
            foreach (var type in module.GetTypes())
            {
                yield return type;
                foreach (var method in type.Methods)
                    yield return method;
                foreach (var field in type.Fields)
                    yield return field;
                foreach (var prop in type.Properties)
                    yield return prop;
                foreach (var evt in type.Events)
                    yield return evt;
            }
        }

        /// <summary>
        /// Determines whether the specified type is visible outside the containing assembly.
        /// </summary>
        /// <param name="typeDef">The type.</param>
        /// <returns><c>true</c> if the specified type is visible outside the containing assembly; otherwise, <c>false</c>.</returns>
        public static bool IsVisibleOutside(this TypeDef typeDef)
        {
            // Assume executable modules' type is not visible
            if (typeDef.Module.Kind == ModuleKind.Windows || typeDef.Module.Kind == ModuleKind.Console)
                return false;

            do
            {
                if (typeDef.DeclaringType == null)
                    return typeDef.IsPublic;
                else if (!typeDef.IsNestedPublic && !typeDef.IsNestedFamily && !typeDef.IsNestedFamilyOrAssembly)
                    return false;
                typeDef = typeDef.DeclaringType;

            } while (typeDef != null);

            throw new UnreachableException();
        }

        /// <summary>
        /// Determines whether the object has the specified custom attribute.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <param name="fullName">The full name of the type of custom attribute.</param>
        /// <returns><c>true</c> if the specified object has custom attribute; otherwise, <c>false</c>.</returns>
        public static bool HasAttribute(this IHasCustomAttribute obj, string fullName)
        {
            return obj.CustomAttributes.Any(attr => attr.TypeFullName == fullName);
        }

        /// <summary>
        /// Determines whether the specified type is COM import.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns><c>true</c> if specified type is COM import; otherwise, <c>false</c>.</returns>
        public static bool IsComImport(this TypeDef type)
        {
            return type.IsImport ||
                   type.HasAttribute("System.Runtime.InteropServices.ComImportAttribute") ||
                   type.HasAttribute("System.Runtime.InteropServices.TypeLibTypeAttribute");
        }

        /// <summary>
        /// Determines whether the specified type is a delegate.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns><c>true</c> if the specified type is a delegate; otherwise, <c>false</c>.</returns>
        public static bool IsDelegate(this TypeDef type)
        {
            if (type.BaseType == null)
                return false;

            string fullName = type.BaseType.FullName;
            return fullName == "System.Delegate" || fullName == "System.MulticastDelegate";
        }

        /// <summary>
        /// Determines whether the specified type implements the specified interface.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="fullName">The full name of the type of interface.</param>
        /// <returns><c>true</c> if the specified type implements the interface; otherwise, <c>false</c>.</returns>
        public static bool Implements(this TypeDef type, string fullName)
        {
            do
            {
                foreach (var iface in type.Interfaces)
                    if (iface.Interface.FullName == fullName)
                        return true;
                if (type.BaseType == null)
                    return false;
                type = type.BaseType.ResolveTypeDefThrow();
            } while (type != null);
            throw new UnreachableException();
        }

        /// <summary>
        /// Resolves the method.
        /// </summary>
        /// <returns>A <see cref="MethodDef"/> instance.</returns>
        /// <exception cref="MemberRefResolveException">The method couldn't be resolved.</exception>
        public static MethodDef ResolveThrow(this IMethodDefOrRef method)
        {
            MethodDef ret = method as MethodDef;
            if (ret != null)
                return ret;
            return ((MemberRef)method).ResolveMethodThrow();
        }
    }
}
