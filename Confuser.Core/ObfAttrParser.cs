using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Confuser.Core {
	internal struct ObfAttrParser {
		readonly IDictionary items;

		string str;
		int index;

		public ObfAttrParser(IDictionary items) {
			this.items = items;
			str = null;
			index = -1;
		}

		enum ParseState {
			Init,
			ReadPreset,
			ReadItemName,
			ProcessItemName,
			ReadParam,
			EndItem,
			End
		}

		bool ReadId(StringBuilder sb) {
			while (index < str.Length) {
				switch (str[index]) {
					case '(':
					case ')':
					case '+':
					case '-':
					case '=':
					case ';':
					case ',':
						return true;
					default:
						sb.Append(str[index++]);
						break;
				}
			}
			return false;
		}

		bool ReadString(StringBuilder sb) {
			Expect('\'');
			while (index < str.Length) {
				switch (str[index]) {
					case '\\':
						sb.Append(str[++index]);
						break;
					case '\'':
						index++;
						return true;
					default:
						sb.Append(str[index]);
						break;
				}
				index++;
			}
			return false;
		}

		void Expect(char chr) {
			if (str[index] != chr)
				throw new ArgumentException("Expect '" + chr + "' at position " + (index + 1) + ".");
			index++;
		}

		char Peek() {
			return str[index];
		}

		void Next() {
			index++;
		}

		bool IsEnd() {
			return index == str.Length;
		}

		public void ParseProtectionString(IDictionary<ConfuserComponent, Dictionary<string, string>> settings, string str) {
			if (str == null)
				return;

			this.str = str;
			index = 0;

			var state = ParseState.Init;
			var buffer = new StringBuilder();

			bool protAct = true;
			string protId = null;
			var protParams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

			while (state != ParseState.End) {
				switch (state) {
					case ParseState.Init:
						ReadId(buffer);
						if (buffer.ToString().Equals("preset", StringComparison.OrdinalIgnoreCase)) {
							if (IsEnd())
								throw new ArgumentException("Unexpected end of string in Init state.");
							Expect('(');
							buffer.Length = 0;
							state = ParseState.ReadPreset;
						}
						else if (buffer.Length == 0) {
							if (IsEnd())
								throw new ArgumentException("Unexpected end of string in Init state.");
							state = ParseState.ReadItemName;
						}
						else {
							protAct = true;
							state = ParseState.ProcessItemName;
						}
						break;

					case ParseState.ReadPreset:
						if (!ReadId(buffer))
							throw new ArgumentException("Unexpected end of string in ReadPreset state.");
						Expect(')');

						var preset = (ProtectionPreset)Enum.Parse(typeof(ProtectionPreset), buffer.ToString(), true);
						foreach (var item in items.Values.OfType<Protection>().Where(prot => prot.Preset <= preset)) {
							if (item.Preset != ProtectionPreset.None && settings != null && !settings.ContainsKey(item))
								settings.Add(item, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
						}
						buffer.Length = 0;

						if (IsEnd())
							state = ParseState.End;
						else {
							Expect(';');
							if (IsEnd())
								state = ParseState.End;
							else
								state = ParseState.ReadItemName;
						}
						break;

					case ParseState.ReadItemName:
						protAct = true;
						if (Peek() == '+') {
							protAct = true;
							Next();
						}
						else if (Peek() == '-') {
							protAct = false;
							Next();
						}
						ReadId(buffer);
						state = ParseState.ProcessItemName;
						break;

					case ParseState.ProcessItemName:
						protId = buffer.ToString();
						buffer.Length = 0;
						if (IsEnd() || Peek() == ';')
							state = ParseState.EndItem;
						else if (Peek() == '(') {
							if (!protAct)
								throw new ArgumentException("No parameters is allowed when removing protection.");
							Next();
							state = ParseState.ReadParam;
						}
						else
							throw new ArgumentException("Unexpected character in ProcessItemName state at " + index + ".");
						break;

					case ParseState.ReadParam:
						string paramName, paramValue;

						if (!ReadId(buffer))
							throw new ArgumentException("Unexpected end of string in ReadParam state.");
						paramName = buffer.ToString();
						buffer.Length = 0;

						Expect('=');
						if (!(Peek() == '\'' ? ReadString(buffer) : ReadId(buffer)))
							throw new ArgumentException("Unexpected end of string in ReadParam state.");

						paramValue = buffer.ToString();
						buffer.Length = 0;

						protParams.Add(paramName, paramValue);

						if (Peek() == ',') {
							Next();
							state = ParseState.ReadParam;
						}
						else if (Peek() == ')') {
							Next();
							state = ParseState.EndItem;
						}
						else
							throw new ArgumentException("Unexpected character in ReadParam state at " + index + ".");
						break;

					case ParseState.EndItem:
						if (settings != null) {
							if (!items.Contains(protId))
								throw new KeyNotFoundException("Cannot find protection with id '" + protId + "'.");

							if (protAct) {
								settings[(Protection)items[protId]] = protParams;
							}
							else
								settings.Remove((Protection)items[protId]);
						}
						protParams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

						if (IsEnd())
							state = ParseState.End;
						else {
							Expect(';');
							if (IsEnd())
								state = ParseState.End;
							else
								state = ParseState.ReadItemName;
						}
						break;
				}
			}
		}

		public void ParsePackerString(string str, out Packer packer, out Dictionary<string, string> packerParams) {
			packer = null;
			packerParams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

			if (str == null)
				return;

			this.str = str;
			index = 0;

			var state = ParseState.ReadItemName;
			var buffer = new StringBuilder();
			var ret = new ProtectionSettings();

			while (state != ParseState.End) {
				switch (state) {
					case ParseState.ReadItemName:
						ReadId(buffer);

						var packerId = buffer.ToString();
						if (!items.Contains(packerId))
							throw new KeyNotFoundException("Cannot find packer with id '" + packerId + "'.");

						packer = (Packer)items[packerId];
						buffer.Length = 0;

						if (IsEnd() || Peek() == ';')
							state = ParseState.EndItem;
						else if (Peek() == '(') {
							Next();
							state = ParseState.ReadParam;
						}
						else
							throw new ArgumentException("Unexpected character in ReadItemName state at " + index + ".");
						break;

					case ParseState.ReadParam:
						string paramName, paramValue;

						if (!ReadId(buffer))
							throw new ArgumentException("Unexpected end of string in ReadParam state.");
						paramName = buffer.ToString();
						buffer.Length = 0;

						Expect('=');
						if (!ReadId(buffer))
							throw new ArgumentException("Unexpected end of string in ReadParam state.");
						paramValue = buffer.ToString();
						buffer.Length = 0;

						packerParams.Add(paramName, paramValue);

						if (Peek() == ',') {
							Next();
							state = ParseState.ReadParam;
						}
						else if (Peek() == ')') {
							Next();
							state = ParseState.EndItem;
						}
						else
							throw new ArgumentException("Unexpected character in ReadParam state at " + index + ".");
						break;

					case ParseState.EndItem:
						if (IsEnd())
							state = ParseState.End;
						else {
							Expect(';');
							if (!IsEnd())
								throw new ArgumentException("Unexpected character in EndItem state at " + index + ".");
							state = ParseState.End;
						}
						break;
				}
			}
		}
	}
}