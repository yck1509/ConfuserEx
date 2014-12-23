using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Confuser.DynCipher.AST;

namespace Confuser.DynCipher.Generation {
	public class x86CodeGen {
		List<x86Instruction> instrs;
		bool[] usedRegs;

		public IList<x86Instruction> Instructions {
			get { return instrs; }
		}

		public int MaxUsedRegister { get; private set; }

		public x86Register? GenerateX86(Expression expression, Func<Variable, x86Register, IEnumerable<x86Instruction>> loadArg) {
			instrs = new List<x86Instruction>();
			usedRegs = new bool[8];
			MaxUsedRegister = -1;

			// CRITICAL registers!
			usedRegs[(int)x86Register.EBP] = true;
			usedRegs[(int)x86Register.ESP] = true;

			try {
				return ((x86RegisterOperand)Emit(expression, loadArg)).Register;
			}
			catch (Exception ex) {
				if (ex.Message == "Register overflowed.")
					return null;
				throw;
			}
		}

		x86Register GetFreeRegister() {
			for (int i = 0; i < 8; i++)
				if (!usedRegs[i])
					return (x86Register)i;

			throw new Exception("Register overflowed.");
		}

		void TakeRegister(x86Register reg) {
			usedRegs[(int)reg] = true;
			if ((int)reg > MaxUsedRegister)
				MaxUsedRegister = (int)reg;
		}

		void ReleaseRegister(x86Register reg) {
			usedRegs[(int)reg] = false;
		}

		x86Register Normalize(x86Instruction instr) {
			if (instr.Operands.Length == 2 &&
			    instr.Operands[0] is x86ImmediateOperand &&
			    instr.Operands[1] is x86ImmediateOperand) {
				/*
                 * op imm1, imm2
                 * ==>
                 * mov reg, imm1
                 * op reg, imm2
                 */
				x86Register reg = GetFreeRegister();
				instrs.Add(x86Instruction.Create(x86OpCode.MOV, new x86RegisterOperand(reg), instr.Operands[0]));
				instr.Operands[0] = new x86RegisterOperand(reg);
				instrs.Add(instr);

				return reg;
			}

			if (instr.Operands.Length == 1 &&
			    instr.Operands[0] is x86ImmediateOperand) {
				/*
                 * op imm
                 * ==>
                 * mov reg, imm
                 * op reg
                 */
				x86Register reg = GetFreeRegister();
				instrs.Add(x86Instruction.Create(x86OpCode.MOV, new x86RegisterOperand(reg), instr.Operands[0]));
				instr.Operands[0] = new x86RegisterOperand(reg);
				instrs.Add(instr);

				return reg;
			}

			if (instr.OpCode == x86OpCode.SUB &&
			    instr.Operands[0] is x86ImmediateOperand &&
			    instr.Operands[1] is x86RegisterOperand) {
				/*
                 * sub imm, reg
                 * ==>
                 * neg reg
                 * add reg, imm
                 */

				x86Register reg = ((x86RegisterOperand)instr.Operands[1]).Register;
				instrs.Add(x86Instruction.Create(x86OpCode.NEG, new x86RegisterOperand(reg)));
				instr.OpCode = x86OpCode.ADD;
				instr.Operands[1] = instr.Operands[0];
				instr.Operands[0] = new x86RegisterOperand(reg);
				instrs.Add(instr);

				return reg;
			}

			if (instr.Operands.Length == 2 &&
			    instr.Operands[0] is x86ImmediateOperand &&
			    instr.Operands[1] is x86RegisterOperand) {
				/*
                 * op imm, reg
                 * ==>
                 * op reg, imm
                 */

				x86Register reg = ((x86RegisterOperand)instr.Operands[1]).Register;
				instr.Operands[1] = instr.Operands[0];
				instr.Operands[0] = new x86RegisterOperand(reg);
				instrs.Add(instr);

				return reg;
			}
			Debug.Assert(instr.Operands.Length > 0);
			Debug.Assert(instr.Operands[0] is x86RegisterOperand);

			if (instr.Operands.Length == 2 && instr.Operands[1] is x86RegisterOperand)
				ReleaseRegister(((x86RegisterOperand)instr.Operands[1]).Register);

			instrs.Add(instr);

			return ((x86RegisterOperand)instr.Operands[0]).Register;
		}

		Ix86Operand Emit(Expression exp, Func<Variable, x86Register, IEnumerable<x86Instruction>> loadArg) {
			if (exp is BinOpExpression) {
				var binOp = (BinOpExpression)exp;
				x86Register reg;
				switch (binOp.Operation) {
					case BinOps.Add:
						reg = Normalize(x86Instruction.Create(x86OpCode.ADD, Emit(binOp.Left, loadArg), Emit(binOp.Right, loadArg)));
						break;

					case BinOps.Sub:
						reg = Normalize(x86Instruction.Create(x86OpCode.SUB, Emit(binOp.Left, loadArg), Emit(binOp.Right, loadArg)));
						break;

					case BinOps.Mul:
						reg = Normalize(x86Instruction.Create(x86OpCode.IMUL, Emit(binOp.Left, loadArg), Emit(binOp.Right, loadArg)));
						break;

					case BinOps.Xor:
						reg = Normalize(x86Instruction.Create(x86OpCode.XOR, Emit(binOp.Left, loadArg), Emit(binOp.Right, loadArg)));
						break;

					default:
						throw new NotSupportedException();
				}
				TakeRegister(reg);
				return new x86RegisterOperand(reg);
			}

			if (exp is UnaryOpExpression) {
				var unaryOp = (UnaryOpExpression)exp;
				x86Register reg;
				switch (unaryOp.Operation) {
					case UnaryOps.Negate:
						reg = Normalize(x86Instruction.Create(x86OpCode.NEG, Emit(unaryOp.Value, loadArg)));
						break;

					case UnaryOps.Not:
						reg = Normalize(x86Instruction.Create(x86OpCode.NOT, Emit(unaryOp.Value, loadArg)));
						break;

					default:
						throw new NotSupportedException();
				}
				TakeRegister(reg);
				return new x86RegisterOperand(reg);
			}

			if (exp is LiteralExpression)
				return new x86ImmediateOperand((int)((LiteralExpression)exp).Value);

			if (exp is VariableExpression) {
				x86Register reg = GetFreeRegister();
				TakeRegister(reg);
				instrs.AddRange(loadArg(((VariableExpression)exp).Variable, reg));
				return new x86RegisterOperand(reg);
			}

			throw new NotSupportedException();
		}

		public override string ToString() {
			return string.Join("\r\n", instrs.Select(instr => instr.ToString()).ToArray());
		}
	}

	public enum x86OpCode {
		MOV,
		ADD,
		SUB,
		IMUL,
		DIV,
		NEG,
		NOT,
		XOR,
		POP
	}

	public enum x86Register {
		EAX,
		ECX,
		EDX,
		EBX,
		ESP,
		EBP,
		ESI,
		EDI
	}

	public interface Ix86Operand { }

	public class x86RegisterOperand : Ix86Operand {
		public x86RegisterOperand(x86Register reg) {
			Register = reg;
		}

		public x86Register Register { get; set; }

		public override string ToString() {
			return Register.ToString();
		}
	}

	public class x86ImmediateOperand : Ix86Operand {
		public x86ImmediateOperand(int imm) {
			Immediate = imm;
		}

		public int Immediate { get; set; }

		public override string ToString() {
			return Immediate.ToString("X") + "h";
		}
	}

	public class x86Instruction {
		public x86OpCode OpCode { get; set; }
		public Ix86Operand[] Operands { get; set; }

		public static x86Instruction Create(x86OpCode opCode, params Ix86Operand[] operands) {
			var ret = new x86Instruction();
			ret.OpCode = opCode;
			ret.Operands = operands;
			return ret;
		}

		public byte[] Assemble() {
			switch (OpCode) {
				case x86OpCode.MOV: {
					if (Operands.Length != 2) throw new InvalidOperationException();
					if (Operands[0] is x86RegisterOperand &&
					    Operands[1] is x86RegisterOperand) {
						var ret = new byte[2];
						ret[0] = 0x89;
						ret[1] = 0xc0;
						ret[1] |= (byte)((int)(Operands[1] as x86RegisterOperand).Register << 3);
						ret[1] |= (byte)((int)(Operands[0] as x86RegisterOperand).Register << 0);
						return ret;
					}
					if (Operands[0] is x86RegisterOperand &&
					    Operands[1] is x86ImmediateOperand) {
						var ret = new byte[5];
						ret[0] = 0xb8;
						ret[0] |= (byte)((int)(Operands[0] as x86RegisterOperand).Register << 0);
						Buffer.BlockCopy(BitConverter.GetBytes((Operands[1] as x86ImmediateOperand).Immediate), 0, ret, 1, 4);
						return ret;
					}
					throw new NotSupportedException();
				}

				case x86OpCode.ADD: {
					if (Operands.Length != 2) throw new InvalidOperationException();
					if (Operands[0] is x86RegisterOperand &&
					    Operands[1] is x86RegisterOperand) {
						var ret = new byte[2];
						ret[0] = 0x01;
						ret[1] = 0xc0;
						ret[1] |= (byte)((int)(Operands[1] as x86RegisterOperand).Register << 3);
						ret[1] |= (byte)((int)(Operands[0] as x86RegisterOperand).Register << 0);
						return ret;
					}
					if (Operands[0] is x86RegisterOperand &&
					    Operands[1] is x86ImmediateOperand) {
						var ret = new byte[6];
						ret[0] = 0x81;
						ret[1] = 0xc0;
						ret[1] |= (byte)((int)(Operands[0] as x86RegisterOperand).Register << 0);
						Buffer.BlockCopy(BitConverter.GetBytes((Operands[1] as x86ImmediateOperand).Immediate), 0, ret, 2, 4);
						return ret;
					}
					throw new NotSupportedException();
				}

				case x86OpCode.SUB: {
					if (Operands.Length != 2) throw new InvalidOperationException();
					if (Operands[0] is x86RegisterOperand &&
					    Operands[1] is x86RegisterOperand) {
						var ret = new byte[2];
						ret[0] = 0x29;
						ret[1] = 0xc0;
						ret[1] |= (byte)((int)(Operands[1] as x86RegisterOperand).Register << 3);
						ret[1] |= (byte)((int)(Operands[0] as x86RegisterOperand).Register << 0);
						return ret;
					}
					if (Operands[0] is x86RegisterOperand &&
					    Operands[1] is x86ImmediateOperand) {
						var ret = new byte[6];
						ret[0] = 0x81;
						ret[1] = 0xe8;
						ret[1] |= (byte)((int)(Operands[0] as x86RegisterOperand).Register << 0);
						Buffer.BlockCopy(BitConverter.GetBytes((Operands[1] as x86ImmediateOperand).Immediate), 0, ret, 2, 4);
						return ret;
					}
					throw new NotSupportedException();
				}

				case x86OpCode.NEG: {
					if (Operands.Length != 1) throw new InvalidOperationException();
					if (Operands[0] is x86RegisterOperand) {
						var ret = new byte[2];
						ret[0] = 0xf7;
						ret[1] = 0xd8;
						ret[1] |= (byte)((int)(Operands[0] as x86RegisterOperand).Register << 0);
						return ret;
					}
					throw new NotSupportedException();
				}

				case x86OpCode.NOT: {
					if (Operands.Length != 1) throw new InvalidOperationException();
					if (Operands[0] is x86RegisterOperand) {
						var ret = new byte[2];
						ret[0] = 0xf7;
						ret[1] = 0xd0;
						ret[1] |= (byte)((int)(Operands[0] as x86RegisterOperand).Register << 0);
						return ret;
					}
					throw new NotSupportedException();
				}

				case x86OpCode.XOR: {
					if (Operands.Length != 2) throw new InvalidOperationException();
					if (Operands[0] is x86RegisterOperand &&
					    Operands[1] is x86RegisterOperand) {
						var ret = new byte[2];
						ret[0] = 0x31;
						ret[1] = 0xc0;
						ret[1] |= (byte)((int)(Operands[1] as x86RegisterOperand).Register << 3);
						ret[1] |= (byte)((int)(Operands[0] as x86RegisterOperand).Register << 0);
						return ret;
					}
					if (Operands[0] is x86RegisterOperand &&
					    Operands[1] is x86ImmediateOperand) {
						var ret = new byte[6];
						ret[0] = 0x81;
						ret[1] = 0xf0;
						ret[1] |= (byte)((int)(Operands[0] as x86RegisterOperand).Register << 0);
						Buffer.BlockCopy(BitConverter.GetBytes((Operands[1] as x86ImmediateOperand).Immediate), 0, ret, 2, 4);
						return ret;
					}
					throw new NotSupportedException();
				}

				case x86OpCode.POP: {
					if (Operands.Length != 1) throw new InvalidOperationException();
					if (Operands[0] is x86RegisterOperand) {
						var ret = new byte[1];
						ret[0] = 0x58;
						ret[0] |= (byte)((int)(Operands[0] as x86RegisterOperand).Register << 0);
						return ret;
					}
					throw new NotSupportedException();
				}

				case x86OpCode.IMUL: {
					if (Operands.Length != 2) throw new InvalidOperationException();
					if (Operands[0] is x86RegisterOperand &&
					    Operands[1] is x86RegisterOperand) {
						var ret = new byte[3];
						ret[0] = 0x0f;
						ret[1] = 0xaf;
						ret[1] = 0xc0;
						ret[1] |= (byte)((int)(Operands[1] as x86RegisterOperand).Register << 3);
						ret[1] |= (byte)((int)(Operands[0] as x86RegisterOperand).Register << 0);
						return ret;
					}
					if (Operands[0] is x86RegisterOperand &&
					    Operands[1] is x86ImmediateOperand) {
						var ret = new byte[6];
						ret[0] = 0x69;
						ret[1] = 0xc0;
						ret[1] |= (byte)((int)(Operands[0] as x86RegisterOperand).Register << 3);
						ret[1] |= (byte)((int)(Operands[0] as x86RegisterOperand).Register << 0);
						Buffer.BlockCopy(BitConverter.GetBytes((Operands[1] as x86ImmediateOperand).Immediate), 0, ret, 2, 4);
						return ret;
					}
					throw new NotSupportedException();
				}

				default:
					throw new NotSupportedException();
			}
		}

		public override string ToString() {
			var ret = new StringBuilder();
			ret.Append(OpCode);
			for (int i = 0; i < Operands.Length; i++) {
				ret.AppendFormat("{0}{1}", i == 0 ? " " : ", ", Operands[i]);
			}
			return ret.ToString();
		}
	}
}