using System;

internal class Mutation {
	public static readonly int KeyI0 = 0;
	public static readonly int KeyI1 = 1;
	public static readonly int KeyI2 = 2;
	public static readonly int KeyI3 = 3;
	public static readonly int KeyI4 = 4;
	public static readonly int KeyI5 = 5;
	public static readonly int KeyI6 = 6;
	public static readonly int KeyI7 = 7;
	public static readonly int KeyI8 = 8;
	public static readonly int KeyI9 = 9;
	public static readonly int KeyI10 = 10;
	public static readonly int KeyI11 = 11;
	public static readonly int KeyI12 = 12;
	public static readonly int KeyI13 = 13;
	public static readonly int KeyI14 = 14;
	public static readonly int KeyI15 = 15;

	public static T Placeholder<T>(T val) {
		return val;
	}

	public static T Value<T>() {
		return default(T);
	}

	public static T Value<T, Arg0>(Arg0 arg0) {
		return default(T);
	}

	public static void Crypt(uint[] data, uint[] key) { }
}