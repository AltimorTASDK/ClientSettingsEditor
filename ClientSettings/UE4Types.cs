using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Globalization;

namespace ClientSettings
{
	public static class UEBinaryExtensions
	{
		public static string ReadFString(this BinaryReader Reader)
		{
			var Length = Reader.ReadInt32();

			if (Length > 0)
			{
				var Data = Reader.ReadBytes(Length);
				return Encoding.ASCII.GetString(Data, 0, Data.Length - 1);
			}
			else if (Length < 0)
			{
				var Data = Reader.ReadBytes(-Length);
				return Encoding.Unicode.GetString(Data, 0, Data.Length - 1);
			}

			return null;
		}

		public static void WriteFString(this BinaryWriter Writer, string value)
		{
			var Data = Encoding.ASCII.GetBytes(value);
			Writer.Write(Data.Length + 1);
			Writer.Write(Data);
			Writer.Write((byte)(0)); // Null terminator
		}
	}

	public class UESerializationException : IOException
	{
		public UESerializationException(string Message) : base(Message)
		{
		}
	}

	public struct FGuid
	{
		public UInt32 A, B, C, D;

		public void Deserialize(BinaryReader Reader)
		{
			A = Reader.ReadUInt32();
			B = Reader.ReadUInt32();
			C = Reader.ReadUInt32();
			D = Reader.ReadUInt32();
		}

		public void Serialize(BinaryWriter Writer)
		{
			Writer.Write(A);
			Writer.Write(B);
			Writer.Write(C);
			Writer.Write(D);
		}
	}

	public abstract class UProperty
	{
		public abstract UProperty Clone();
		public abstract void Deserialize(BinaryReader Reader);
		public abstract void Serialize(BinaryWriter Writer);
		public abstract string DisplayValue();
		public abstract void Modify(string NewValue);
		public abstract bool Editable { get; }
	}

	public class UFloatProperty : UProperty
	{
		private float Value;

		public override UProperty Clone()
		{
			return new UFloatProperty { Value = Value };
		}

		public override void Deserialize(BinaryReader Reader)
		{
			Value = Reader.ReadSingle();
		}

		public override void Serialize(BinaryWriter Writer)
		{
			Writer.Write(Value);
		}

		public override string DisplayValue()
		{
			return Value.ToString();
		}

		public override void Modify(string NewValue)
		{
			if (float.TryParse(NewValue, out var FloatVal))
				Value = FloatVal;
		}

		public override bool Editable { get { return true; } }
	}

	public class UArrayProperty : UProperty
	{
		public int Length;
		public FPropertyTag InnerTag { get; private set; } = new FPropertyTag();

		public override UProperty Clone()
		{
			return new UArrayProperty { Length = Length, InnerTag = InnerTag.Clone() };
		}

		public override void Deserialize(BinaryReader Reader)
		{
			Length = Reader.ReadInt32();
			InnerTag.Deserialize(Reader);
		}

		public override void Serialize(BinaryWriter Writer)
		{
			Writer.Write(Length);
			InnerTag.Serialize(Writer);
		}

		public override string DisplayValue()
		{
			return "";
		}

		public override void Modify(string NewValue)
		{
		}

		public override bool Editable { get { return false; } }
	}

	public class UMapProperty : UProperty
	{
		public int NumKeysToRemove;
		public int NumEntries;

		public override UProperty Clone()
		{
			return new UMapProperty { NumKeysToRemove = NumKeysToRemove, NumEntries = NumEntries };
		}

		public override void Deserialize(BinaryReader Reader)
		{
			NumKeysToRemove = Reader.ReadInt32();
			NumEntries = Reader.ReadInt32();
		}

		public override void Serialize(BinaryWriter Writer)
		{
			Writer.Write(NumKeysToRemove);
			Writer.Write(NumEntries);
		}

		public override string DisplayValue()
		{
			return "";
		}

		public override void Modify(string NewValue)
		{
		}

		public override bool Editable { get { return false; } }
	}

	public class UNameProperty : UProperty
	{
		private string Value;

		public override UProperty Clone()
		{
			return new UNameProperty { Value = string.Copy(Value) };
		}

		public override void Deserialize(BinaryReader Reader)
		{
			Value = Reader.ReadFString();
		}

		public override void Serialize(BinaryWriter Writer)
		{
			Writer.WriteFString(Value);
		}

		public override string DisplayValue()
		{
			return Value;
		}

		public override void Modify(string NewValue)
		{
			Value = NewValue;
		}

		public override bool Editable { get { return true; } }
	}

	public class UTextProperty : UProperty
	{
		private Int32 Flags;
		private byte HistoryType;
		private string Namespace, Key, SourceString;

		public override UProperty Clone()
		{
			return new UTextProperty { Flags = Flags, HistoryType = HistoryType, Namespace = string.Copy(Namespace), Key = string.Copy(Key), SourceString = string.Copy(SourceString) };
		}

		public override void Deserialize(BinaryReader Reader)
		{
			Flags = Reader.ReadInt32();
			HistoryType = Reader.ReadByte();

			if (HistoryType != 0)
				throw new UESerializationException("Unsupported HistoryType");

			Namespace = Reader.ReadFString();
			Key = Reader.ReadFString();
			SourceString = Reader.ReadFString();
		}

		public override void Serialize(BinaryWriter Writer)
		{
			Writer.Write(Flags);
			Writer.Write(HistoryType);

			if (HistoryType != 0)
				throw new UESerializationException("Unsupported HistoryType");

			Writer.WriteFString(Namespace);
			Writer.WriteFString(Key);
			Writer.WriteFString(SourceString);
		}

		public override string DisplayValue()
		{
			return SourceString;
		}

		public override void Modify(string NewValue)
		{
		}

		public override bool Editable { get { return false; } }
	}

	public class FVector2D : UProperty
	{
		private float X, Y;

		public override UProperty Clone()
		{
			return new FVector2D { X = X, Y = Y };
		}

		public override void Deserialize(BinaryReader Reader)
		{
			X = Reader.ReadSingle();
			Y = Reader.ReadSingle();
		}

		public override void Serialize(BinaryWriter Writer)
		{
			Writer.Write(X);
			Writer.Write(Y);
		}

		public override string DisplayValue()
		{
			return "(" + X.ToString() + ", " + Y.ToString() + ")";
		}

		public override void Modify(string NewValue)
		{
			if (!NewValue.StartsWith("(") || !NewValue.EndsWith(")"))
				return;

			var Split = NewValue.Substring(1, NewValue.Length - 2).Split(',');
			if (Split.Length != 2)
				return;

			if (!float.TryParse(Split[0], out var NewX) || !float.TryParse(Split[1], out var NewY))
				return;

			X = NewX;
			Y = NewY;
		}

		public override bool Editable { get { return true; } }
	}

	public class FPropertyTag
	{
		public string Type { get; private set; }
		public byte BoolVal;
		public string Name;
		public string StructName { get; private set; }
		public string EnumName { get; private set; }
		public string InnerType { get; private set; }
		public string ValueType { get; private set; }
		public Int32 Size { get; private set; }
		public long SizeOffset { get; private set; }
		private Int32 ArrayIndex;
		private FGuid StructGuid;
		private byte HasPropertyGuid;
		private FGuid PropertyGuid;

		public FPropertyTag()
		{
		}

		public FPropertyTag(string InType)
		{
			Type = InType;
		}

		public FPropertyTag Clone()
		{
			return new FPropertyTag
			{
				Type = Type,
				BoolVal = BoolVal,
				Name = Name,
				StructName = StructName,
				EnumName = EnumName,
				InnerType = InnerType,
				ValueType = ValueType,
				Size = Size,
				ArrayIndex = ArrayIndex,
				StructGuid = StructGuid,
				HasPropertyGuid = HasPropertyGuid,
				PropertyGuid = PropertyGuid
			};
		}

		public void Deserialize(BinaryReader Reader)
		{
			Name = Reader.ReadFString();

			if (Name == "None")
				return;

			Type = Reader.ReadFString();
			Size = Reader.ReadInt32();
			ArrayIndex = Reader.ReadInt32();

			if (Type == "StructProperty")
			{
				StructName = Reader.ReadFString();
				StructGuid.Deserialize(Reader);
			}
			else if (Type == "BoolProperty")
			{
				BoolVal = Reader.ReadByte();
			}
			else if (Type == "ByteProperty" || Type == "EnumProperty")
			{
				EnumName = Reader.ReadFString();
			}
			else if (Type == "ArrayProperty" || Type == "SetProperty")
			{
				InnerType = Reader.ReadFString();
			}
			else if (Type == "MapProperty")
			{
				InnerType = Reader.ReadFString();
				ValueType = Reader.ReadFString();
			}

			HasPropertyGuid = Reader.ReadByte();

			if (HasPropertyGuid != 0)
				PropertyGuid.Deserialize(Reader);
		}

		public void Serialize(BinaryWriter Writer)
		{
			Writer.WriteFString(Name);

			if (Name == "None")
				return;

			Writer.WriteFString(Type);
			SizeOffset = Writer.BaseStream.Position;
			Writer.Write(Size);
			Writer.Write(ArrayIndex);

			if (Type == "StructProperty")
			{
				Writer.WriteFString(StructName);
				StructGuid.Serialize(Writer);
			}
			else if (Type == "BoolProperty")
			{
				Writer.Write(BoolVal);
			}
			else if (Type == "ByteProperty" || Type == "EnumProperty")
			{
				Writer.WriteFString(EnumName);
			}
			else if (Type == "ArrayProperty" || Type == "SetProperty")
			{
				Writer.WriteFString(InnerType);
			}
			else if (Type == "MapProperty")
			{
				Writer.WriteFString(InnerType);
				Writer.WriteFString(ValueType);
			}

			Writer.Write(HasPropertyGuid);

			if (HasPropertyGuid != 0)
				PropertyGuid.Serialize(Writer);
		}
	}
}
