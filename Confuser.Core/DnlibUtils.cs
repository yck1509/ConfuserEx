using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.IO;

namespace Confuser.Core {
	/// <summary>
	///     Provides a set of utility methods about dnlib
	/// </summary>
	public static class DnlibUtils {
		/// <summary>
		///     Finds all definitions of interest in a module.
		/// </summary>
		/// <param name="module">The module.</param>
		/// <returns>A collection of all required definitions</returns>
		public static IEnumerable<IDnlibDef> FindDefinitions(this ModuleDef module) {
			yield return module;
			foreach (TypeDef type in module.GetTypes()) {
				yield return type;

				foreach (MethodDef method in type.Methods)
					yield return method;

				foreach (FieldDef field in type.Fields)
					yield return field;

				foreach (PropertyDef prop in type.Properties)
					yield return prop;

				foreach (EventDef evt in type.Events)
					yield return evt;
			}
		}

		/// <summary>
		///     Finds all definitions of interest in a type.
		/// </summary>
		/// <param name="typeDef">The type.</param>
		/// <returns>A collection of all required definitions</returns>
		public static IEnumerable<IDnlibDef> FindDefinitions(this TypeDef typeDef) {
			yield return typeDef;

			foreach (TypeDef nestedType in typeDef.NestedTypes)
				yield return nestedType;

			foreach (MethodDef method in typeDef.Methods)
				yield return method;

			foreach (FieldDef field in typeDef.Fields)
				yield return field;

			foreach (PropertyDef prop in typeDef.Properties)
				yield return prop;

			foreach (EventDef evt in typeDef.Events)
				yield return evt;
		}

		/// <summary>
		///     Determines whether the specified type is visible outside the containing assembly.
		/// </summary>
		/// <param name="typeDef">The type.</param>
		/// <param name="exeNonPublic">Visibility of executable modules.</param>
		/// <returns><c>true</c> if the specified type is visible outside the containing assembly; otherwise, <c>false</c>.</returns>
		public static bool IsVisibleOutside(this TypeDef typeDef, bool exeNonPublic = true) {
			// Assume executable modules' type is not visible
			if (exeNonPublic && (typeDef.Module.Kind == ModuleKind.Windows || typeDef.Module.Kind == ModuleKind.Console))
				return false;

			do {
				if (typeDef.DeclaringType == null)
					return typeDef.IsPublic;
				if (!typeDef.IsNestedPublic && !typeDef.IsNestedFamily && !typeDef.IsNestedFamilyOrAssembly)
					return false;
				typeDef = typeDef.DeclaringType;
			} while (typeDef != null);

			throw new UnreachableException();
		}

		/// <summary>
		///     Determines whether the object has the specified custom attribute.
		/// </summary>
		/// <param name="obj">The object.</param>
		/// <param name="fullName">The full name of the type of custom attribute.</param>
		/// <returns><c>true</c> if the specified object has custom attribute; otherwise, <c>false</c>.</returns>
		public static bool HasAttribute(this IHasCustomAttribute obj, string fullName) {
			return obj.CustomAttributes.Any(attr => attr.TypeFullName == fullName);
		}

		/// <summary>
		///     Determines whether the specified type is COM import.
		/// </summary>
		/// <param name="type">The type.</param>
		/// <returns><c>true</c> if specified type is COM import; otherwise, <c>false</c>.</returns>
		public static bool IsComImport(this TypeDef type) {
			return type.IsImport ||
			       type.HasAttribute("System.Runtime.InteropServices.ComImportAttribute") ||
			       type.HasAttribute("System.Runtime.InteropServices.TypeLibTypeAttribute");
		}

		/// <summary>
		///     Determines whether the specified type is compiler generated.
		/// </summary>
		/// <param name="type">The type.</param>
		/// <returns><c>true</c> if specified type is compiler generated; otherwise, <c>false</c>.</returns>
		public static bool IsCompilerGenerated(this TypeDef type) {
			return type.HasAttribute("System.Runtime.CompilerServices.CompilerGeneratedAttribute");
		}

		/// <summary>
		///     Determines whether the specified type is a delegate.
		/// </summary>
		/// <param name="type">The type.</param>
		/// <returns><c>true</c> if the specified type is a delegate; otherwise, <c>false</c>.</returns>
		public static bool IsDelegate(this TypeDef type) {
			if (type.BaseType == null)
				return false;

			string fullName = type.BaseType.FullName;
			return fullName == "System.Delegate" || fullName == "System.MulticastDelegate";
		}

		/// <summary>
		///     Determines whether the specified type is inherited from a base type in corlib.
		/// </summary>
		/// <param name="type">The type.</param>
		/// <param name="baseType">The full name of base type.</param>
		/// <returns><c>true</c> if the specified type is inherited from a base type; otherwise, <c>false</c>.</returns>
		public static bool InheritsFromCorlib(this TypeDef type, string baseType) {
			if (type.BaseType == null)
				return false;

			TypeDef bas = type;
			do {
				bas = bas.BaseType.ResolveTypeDefThrow();
				if (bas.ReflectionFullName == baseType)
					return true;
			} while (bas.BaseType != null && bas.BaseType.DefinitionAssembly.IsCorLib());
			return false;
		}

		/// <summary>
		///     Determines whether the specified type is inherited from a base type.
		/// </summary>
		/// <param name="type">The type.</param>
		/// <param name="baseType">The full name of base type.</param>
		/// <returns><c>true</c> if the specified type is inherited from a base type; otherwise, <c>false</c>.</returns>
		public static bool InheritsFrom(this TypeDef type, string baseType) {
			if (type.BaseType == null)
				return false;

			TypeDef bas = type;
			do {
				bas = bas.BaseType.ResolveTypeDefThrow();
				if (bas.ReflectionFullName == baseType)
					return true;
			} while (bas.BaseType != null);
			return false;
		}

		/// <summary>
		///     Determines whether the specified type implements the specified interface.
		/// </summary>
		/// <param name="type">The type.</param>
		/// <param name="fullName">The full name of the type of interface.</param>
		/// <returns><c>true</c> if the specified type implements the interface; otherwise, <c>false</c>.</returns>
		public static bool Implements(this TypeDef type, string fullName) {
			do {
				foreach (InterfaceImpl iface in type.Interfaces) {
					if (iface.Interface.ReflectionFullName == fullName)
						return true;
				}

				if (type.BaseType == null)
					return false;

				type = type.BaseType.ResolveTypeDefThrow();
			} while (type != null);
			throw new UnreachableException();
		}

		/// <summary>
		///     Resolves the method.
		/// </summary>
		/// <param name="method">The method to resolve.</param>
		/// <returns>A <see cref="MethodDef" /> instance.</returns>
		/// <exception cref="MemberRefResolveException">The method couldn't be resolved.</exception>
		public static MethodDef ResolveThrow(this IMethod method) {
			var def = method as MethodDef;
			if (def != null)
				return def;

			var spec = method as MethodSpec;
			if (spec != null)
				return spec.Method.ResolveThrow();

			return ((MemberRef)method).ResolveMethodThrow();
		}

		/// <summary>
		///     Resolves the field.
		/// </summary>
		/// <param name="field">The field to resolve.</param>
		/// <returns>A <see cref="FieldDef" /> instance.</returns>
		/// <exception cref="MemberRefResolveException">The method couldn't be resolved.</exception>
		public static FieldDef ResolveThrow(this IField field) {
			var def = field as FieldDef;
			if (def != null)
				return def;

			return ((MemberRef)field).ResolveFieldThrow();
		}

		/// <summary>
		///     Find the basic type reference.
		/// </summary>
		/// <param name="typeSig">The type signature to get the basic type.</param>
		/// <returns>A <see cref="ITypeDefOrRef" /> instance, or null if the typeSig cannot be resolved to basic type.</returns>
		public static ITypeDefOrRef ToBasicTypeDefOrRef(this TypeSig typeSig) {
			while (typeSig.Next != null)
				typeSig = typeSig.Next;

			if (typeSig is GenericInstSig)
				return ((GenericInstSig)typeSig).GenericType.TypeDefOrRef;
			if (typeSig is TypeDefOrRefSig)
				return ((TypeDefOrRefSig)typeSig).TypeDefOrRef;
			return null;
		}

		/// <summary>
		///     Find the type references within the specified type signature.
		/// </summary>
		/// <param name="typeSig">The type signature to find the type references.</param>
		/// <returns>A list of <see cref="ITypeDefOrRef" /> instance.</returns>
		public static IList<ITypeDefOrRef> FindTypeRefs(this TypeSig typeSig) {
			var ret = new List<ITypeDefOrRef>();
			FindTypeRefsInternal(typeSig, ret);
			return ret;
		}

		static void FindTypeRefsInternal(TypeSig typeSig, IList<ITypeDefOrRef> ret) {
			while (typeSig.Next != null) {
				if (typeSig is ModifierSig)
					ret.Add(((ModifierSig)typeSig).Modifier);
				typeSig = typeSig.Next;
			}

			if (typeSig is GenericInstSig) {
				var genInst = (GenericInstSig)typeSig;
				ret.Add(genInst.GenericType.TypeDefOrRef);
				foreach (TypeSig genArg in genInst.GenericArguments)
					FindTypeRefsInternal(genArg, ret);
			}
			else if (typeSig is TypeDefOrRefSig) {
				var type = ((TypeDefOrRefSig)typeSig).TypeDefOrRef;
				while (type != null) {
					ret.Add(type);
					type = type.DeclaringType;
				}
			}
		}

		/// <summary>
		///     Determines whether the specified property is public.
		/// </summary>
		/// <param name="property">The property.</param>
		/// <returns><c>true</c> if the specified property is public; otherwise, <c>false</c>.</returns>
		public static bool IsPublic(this PropertyDef property) {
			if (property.GetMethod != null && property.GetMethod.IsPublic)
				return true;

			if (property.SetMethod != null && property.SetMethod.IsPublic)
				return true;

			return property.OtherMethods.Any(method => method.IsPublic);
		}

		/// <summary>
		///     Determines whether the specified property is static.
		/// </summary>
		/// <param name="property">The property.</param>
		/// <returns><c>true</c> if the specified property is static; otherwise, <c>false</c>.</returns>
		public static bool IsStatic(this PropertyDef property) {
			if (property.GetMethod != null && property.GetMethod.IsStatic)
				return true;

			if (property.SetMethod != null && property.SetMethod.IsStatic)
				return true;

			return property.OtherMethods.Any(method => method.IsStatic);
		}

		/// <summary>
		///     Determines whether the specified event is public.
		/// </summary>
		/// <param name="evt">The event.</param>
		/// <returns><c>true</c> if the specified event is public; otherwise, <c>false</c>.</returns>
		public static bool IsPublic(this EventDef evt) {
			if (evt.AddMethod != null && evt.AddMethod.IsPublic)
				return true;

			if (evt.RemoveMethod != null && evt.RemoveMethod.IsPublic)
				return true;

			if (evt.InvokeMethod != null && evt.InvokeMethod.IsPublic)
				return true;

			return evt.OtherMethods.Any(method => method.IsPublic);
		}

		/// <summary>
		///     Determines whether the specified event is static.
		/// </summary>
		/// <param name="evt">The event.</param>
		/// <returns><c>true</c> if the specified event is static; otherwise, <c>false</c>.</returns>
		public static bool IsStatic(this EventDef evt) {
			if (evt.AddMethod != null && evt.AddMethod.IsStatic)
				return true;

			if (evt.RemoveMethod != null && evt.RemoveMethod.IsStatic)
				return true;

			if (evt.InvokeMethod != null && evt.InvokeMethod.IsStatic)
				return true;

			return evt.OtherMethods.Any(method => method.IsStatic);
		}

		/// <summary>
		///     Replaces the specified instruction reference with another instruction.
		/// </summary>
		/// <param name="body">The method body.</param>
		/// <param name="target">The instruction to replace.</param>
		/// <param name="newInstr">The new instruction.</param>
		public static void ReplaceReference(this CilBody body, Instruction target, Instruction newInstr) {
			foreach (ExceptionHandler eh in body.ExceptionHandlers) {
				if (eh.TryStart == target)
					eh.TryStart = newInstr;
				if (eh.TryEnd == target)
					eh.TryEnd = newInstr;
				if (eh.HandlerStart == target)
					eh.HandlerStart = newInstr;
				if (eh.HandlerEnd == target)
					eh.HandlerEnd = newInstr;
			}
			foreach (Instruction instr in body.Instructions) {
				if (instr.Operand == target)
					instr.Operand = newInstr;
				else if (instr.Operand is Instruction[]) {
					var targets = (Instruction[])instr.Operand;
					for (int i = 0; i < targets.Length; i++)
						if (targets[i] == target)
							targets[i] = newInstr;
				}
			}
		}

		/// <summary>
		///     Determines whether the specified method is array accessors.
		/// </summary>
		/// <param name="method">The method.</param>
		/// <returns><c>true</c> if the specified method is array accessors; otherwise, <c>false</c>.</returns>
		public static bool IsArrayAccessors(this IMethod method) {
			var declType = method.DeclaringType.ToTypeSig();
			if (declType is GenericInstSig)
				declType = ((GenericInstSig)declType).GenericType;

			if (declType.IsArray) {
				return method.Name == "Get" || method.Name == "Set" || method.Name == "Address";
			}
			return false;
		}
	}


	/// <summary>
	///     <see cref="Stream" /> wrapper of <see cref="IImageStream" />.
	/// </summary>
	public class ImageStream : Stream {
		/// <summary>
		///     Initializes a new instance of the <see cref="ImageStream" /> class.
		/// </summary>
		/// <param name="baseStream">The base stream.</param>
		public ImageStream(IImageStream baseStream) {
			BaseStream = baseStream;
		}

		/// <summary>
		///     Gets the base stream of this instance.
		/// </summary>
		/// <value>The base stream.</value>
		public IImageStream BaseStream { get; private set; }

		/// <inheritdoc />
		public override bool CanRead {
			get { return true; }
		}

		/// <inheritdoc />
		public override bool CanSeek {
			get { return true; }
		}

		/// <inheritdoc />
		public override bool CanWrite {
			get { return false; }
		}

		/// <inheritdoc />
		public override long Length {
			get { return BaseStream.Length; }
		}

		/// <inheritdoc />
		public override long Position {
			get { return BaseStream.Position; }
			set { BaseStream.Position = value; }
		}

		/// <inheritdoc />
		public override void Flush() { }

		/// <inheritdoc />
		public override int Read(byte[] buffer, int offset, int count) {
			return BaseStream.Read(buffer, offset, count);
		}

		/// <inheritdoc />
		public override long Seek(long offset, SeekOrigin origin) {
			switch (origin) {
				case SeekOrigin.Begin:
					BaseStream.Position = offset;
					break;
				case SeekOrigin.Current:
					BaseStream.Position += offset;
					break;
				case SeekOrigin.End:
					BaseStream.Position = BaseStream.Length + offset;
					break;
			}
			return BaseStream.Position;
		}

		/// <inheritdoc />
		public override void SetLength(long value) {
			throw new NotSupportedException();
		}

		/// <inheritdoc />
		public override void Write(byte[] buffer, int offset, int count) {
			throw new NotSupportedException();
		}
	}
}