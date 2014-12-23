using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Confuser.Renamer.BAML {
	internal class PropertyPathIndexer {
		public string Type { get; set; }
		public string Value { get; set; }
	}

	internal class PropertyPathPart {
		public PropertyPathPart(bool isIndexer, bool? isHiera, string name) {
			IsIndexer = isIndexer;
			IsHierarchical = isHiera;
			Name = name;
			IndexerArguments = null;
		}

		// Property or Indexer
		public bool IsIndexer { get; set; }
		public bool? IsHierarchical { get; set; }
		public string Name { get; set; }
		public PropertyPathIndexer[] IndexerArguments { get; set; }

		public bool IsAttachedDP() {
			return !IsIndexer && Name.Length >= 2 && Name[0] == '(' && Name[Name.Length - 1] == ')';
		}

		public void ExtractAttachedDP(out string type, out string property) {
			string name = Name.Substring(1, Name.Length - 2);
			if (!name.Contains('.')) {
				// type = null means the property is on the same type
				type = null;
				property = name.Trim();
			}
			else {
				int dot = name.LastIndexOf('.');
				type = name.Substring(0, dot).Trim();
				property = name.Substring(dot + 1).Trim();
			}
		}
	}

	internal class PropertyPath {
		// See: MS.Internal.Data.PathParser

		static readonly char[] SpecialChars = {
			'.',
			'/',
			'[',
			']'
		};

		readonly PropertyPathPart[] parts;

		public PropertyPath(string path) {
			parts = Parse(path);
		}

		public PropertyPathPart[] Parts {
			get { return parts; }
		}

		static PropertyPathPart ReadIndexer(string path, ref int index, bool? isHiera) {
			index++;

			var args = new List<PropertyPathIndexer>();
			var typeString = new StringBuilder();
			var valueString = new StringBuilder();
			bool trim = false;
			int level = 0;

			const int STATE_WAIT = 0;
			const int STATE_TYPE = 1;
			const int STATE_VALUE = 2;
			const int STATE_DONE = 3;
			int state = STATE_WAIT;

			while (state != STATE_DONE) {
				char c = path[index];
				switch (state) {
					case STATE_WAIT:
						if (c == '(') {
							index++;
							state = STATE_TYPE;
						}
						else if (c == '^') {
							valueString.Append(path[++index]);
							index++;
							state = STATE_VALUE;
						}
						else if (char.IsWhiteSpace(c)) {
							index++;
						}
						else {
							valueString.Append(path[index++]);
							state = STATE_VALUE;
						}
						break;
					case STATE_TYPE:
						if (c == ')') {
							index++;
							state = STATE_VALUE;
						}
						else if (c == '^') {
							typeString.Append(path[++index]);
							index++;
						}
						else {
							typeString.Append(path[index++]);
						}
						break;
					case STATE_VALUE:
						if (c == '[') {
							valueString.Append(path[index++]);
							level++;
							trim = false;
						}
						else if (c == '^') {
							valueString.Append(path[++index]);
							index++;
							trim = false;
						}
						else if (level > 0 && c == ']') {
							level--;
							valueString.Append(path[index++]);
							trim = false;
						}
						else if (c == ']' || c == ',') {
							string value = valueString.ToString();
							// Note: it may be a WPF bug that if the value is "^  " (2 spaces after caret), all spaces will be trimmed.
							// According to http://msdn.microsoft.com/en-us/library/ms742451.aspx, the result should have one space.
							if (trim)
								value.TrimEnd();
							args.Add(new PropertyPathIndexer {
								Type = typeString.ToString(),
								Value = value
							});

							valueString.Length = 0;
							typeString.Length = 0;
							trim = false;

							index++;
							if (c == ',')
								state = STATE_WAIT;
							else
								state = STATE_DONE;
						}
						else {
							valueString.Append(path[index++]);
							if (c == ' ' && level == 0)
								trim = true;
							else
								trim = false;
						}
						break;
				}
			}

			return new PropertyPathPart(true, isHiera, "Item") {
				IndexerArguments = args.ToArray()
			};
		}

		static PropertyPathPart ReadProperty(string path, ref int index, bool? isHiera) {
			int begin = index;
			while (index < path.Length && path[index] == '.')
				index++;

			int level = 0;
			// If in brackets, read until not in bracket, ignoring special chars.
			while (index < path.Length && (level > 0 || Array.IndexOf(SpecialChars, path[index]) == -1)) {
				if (path[index] == '(')
					level++;
				else if (path[index] == ')')
					level--;

				index++;
			}

			string name = path.Substring(begin, index - begin).Trim();

			return new PropertyPathPart(false, isHiera, name);
		}

		static PropertyPathPart[] Parse(string path) {
			if (string.IsNullOrEmpty(path))
				return new[] { new PropertyPathPart(true, null, "") };

			var ret = new List<PropertyPathPart>();
			bool? isHiera = null;
			int index = 0;
			while (index < path.Length) {
				if (char.IsWhiteSpace(path[index])) {
					index++;
					continue;
				}

				char c = path[index];
				switch (c) {
					case '.':
						isHiera = false;
						index++;
						break;
					case '/':
						isHiera = true;
						index++;
						break;
					case '[':
						ret.Add(ReadIndexer(path, ref index, isHiera));
						isHiera = null;
						break;
					default:
						ret.Add(ReadProperty(path, ref index, isHiera));
						isHiera = null;
						break;
				}
			}
			return ret.ToArray();
		}

		public override string ToString() {
			var ret = new StringBuilder();
			foreach (PropertyPathPart part in parts) {
				if (part.IsHierarchical.HasValue) {
					if (part.IsHierarchical.Value)
						ret.Append("/");
					else
						ret.Append(".");
				}

				ret.Append(part.Name);

				if (part.IsIndexer) {
					PropertyPathIndexer[] args = part.IndexerArguments;
					for (int i = 0; i < args.Length; i++) {
						if (i == 0)
							ret.Append("[");
						else
							ret.Append(",");

						if (!string.IsNullOrEmpty(args[i].Type))
							ret.AppendFormat("({0})", args[i].Type);

						if (!string.IsNullOrEmpty(args[i].Value))
							foreach (char c in args[i].Value) {
								// Too lazy to write all the level detection, just be safe, and escape all special chars.
								if (c == '[' || c == ']' || c == ' ')
									ret.Append("^");
								ret.Append(c);
							}
					}
					ret.Append("]");
				}
			}
			return ret.ToString();
		}
	}
}