using System;
using System.Collections.Generic;
using System.Diagnostics;
using dnlib.DotNet;

namespace Confuser.Renamer.BAML {
	internal class BamlElement {
		public BamlElement Parent { get; private set; }
		public BamlRecord Header { get; private set; }
		public IList<BamlRecord> Body { get; private set; }
		public IList<BamlElement> Children { get; private set; }
		public BamlRecord Footer { get; private set; }

		// Type of this element
		public TypeDef Type { get; set; }
		// Attribute this element will be assigned to.
		public IDnlibDef Attribute { get; set; }

		static bool IsHeader(BamlRecord rec) {
			switch (rec.Type) {
				case BamlRecordType.ConstructorParametersStart:
				case BamlRecordType.DocumentStart:
				case BamlRecordType.ElementStart:
				case BamlRecordType.KeyElementStart:
				case BamlRecordType.NamedElementStart:
				case BamlRecordType.PropertyArrayStart:
				case BamlRecordType.PropertyComplexStart:
				case BamlRecordType.PropertyDictionaryStart:
				case BamlRecordType.PropertyListStart:
				case BamlRecordType.StaticResourceStart:
					return true;
			}
			return false;
		}

		static bool IsFooter(BamlRecord rec) {
			switch (rec.Type) {
				case BamlRecordType.ConstructorParametersEnd:
				case BamlRecordType.DocumentEnd:
				case BamlRecordType.ElementEnd:
				case BamlRecordType.KeyElementEnd:
				case BamlRecordType.PropertyArrayEnd:
				case BamlRecordType.PropertyComplexEnd:
				case BamlRecordType.PropertyDictionaryEnd:
				case BamlRecordType.PropertyListEnd:
				case BamlRecordType.StaticResourceEnd:
					return true;
			}
			return false;
		}

		static bool IsMatch(BamlRecord header, BamlRecord footer) {
			switch (header.Type) {
				case BamlRecordType.ConstructorParametersStart:
					return footer.Type == BamlRecordType.ConstructorParametersEnd;

				case BamlRecordType.DocumentStart:
					return footer.Type == BamlRecordType.DocumentEnd;

				case BamlRecordType.KeyElementStart:
					return footer.Type == BamlRecordType.KeyElementEnd;

				case BamlRecordType.PropertyArrayStart:
					return footer.Type == BamlRecordType.PropertyArrayEnd;

				case BamlRecordType.PropertyComplexStart:
					return footer.Type == BamlRecordType.PropertyComplexEnd;

				case BamlRecordType.PropertyDictionaryStart:
					return footer.Type == BamlRecordType.PropertyDictionaryEnd;

				case BamlRecordType.PropertyListStart:
					return footer.Type == BamlRecordType.PropertyListEnd;

				case BamlRecordType.StaticResourceStart:
					return footer.Type == BamlRecordType.StaticResourceEnd;

				case BamlRecordType.ElementStart:
				case BamlRecordType.NamedElementStart:
					return footer.Type == BamlRecordType.ElementEnd;
			}
			return false;
		}

		public static BamlElement Read(BamlDocument document) {
			Debug.Assert(document.Count > 0 && document[0].Type == BamlRecordType.DocumentStart);

			BamlElement current = null;
			var stack = new Stack<BamlElement>();

			for (int i = 0; i < document.Count; i++) {
				if (IsHeader(document[i])) {
					BamlElement prev = current;

					current = new BamlElement();
					current.Header = document[i];
					current.Body = new List<BamlRecord>();
					current.Children = new List<BamlElement>();

					if (prev != null) {
						prev.Children.Add(current);
						current.Parent = prev;
						stack.Push(prev);
					}
				}
				else if (IsFooter(document[i])) {
					if (current == null)
						throw new Exception("Unexpected footer.");

					while (!IsMatch(current.Header, document[i])) {
						// End record can be omited (sometimes).
						if (stack.Count > 0)
							current = stack.Pop();
					}
					current.Footer = document[i];
					if (stack.Count > 0)
						current = stack.Pop();
				}
				else
					current.Body.Add(document[i]);
			}
			Debug.Assert(stack.Count == 0);
			return current;
		}
	}
}