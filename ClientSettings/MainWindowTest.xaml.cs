using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;

namespace ClientSettings
{
	/// <summary>
	/// Interaction logic for MainWindowTest.xaml
	/// </summary>
	public partial class MainWindowTest : Window
	{
		public class PropertyInfo
		{
			public FPropertyTag Tag = new FPropertyTag();
			public UProperty Prop = null;
			public IList<PropertyInfo> Children { get; set; } = new List<PropertyInfo>();
			public int ArrayIndex = -1;

			// Only used for unsupported types
			public byte[] UnsupportedData = null;

			public string Name
			{
				get
				{
					if (ArrayIndex >= 0)
						return "[" + ArrayIndex + "]";

					return Tag.Name;
				}
			}

			public string Type
			{
				get
				{
					if (Tag.Type == "StructProperty")
						return "struct " + Tag.StructName;
					if (Tag.Type == "EnumProperty")
						return "enum " + Tag.EnumName;
					if (Tag.Type == "ArrayProperty")
						return (Children.Count > 0 ? Children[0].Type : Tag.InnerType) + "["  + Children.Count + "]";
					if (Tag.Type == "FloatProperty")
						return "float";
					if (Tag.Type == "BoolProperty")
						return "bool";
					if (Tag.Type == "StrProperty")
						return "string";
					if (Tag.Type == "NameProperty")
						return "FName";
					if (Tag.Type == "TextProperty")
						return "FText";
					if (Tag.Type == "MapProperty")
						return "map<" + Tag.InnerType + ", " + Tag.ValueType + ">";

					return Tag.Type;
				}
			}

			public string Value
			{
				get
				{
					if (Prop == null)
					{
						if (Tag.Type == "BoolProperty")
							return Tag.BoolVal != 0 ? "True" : "False";
						else
							return "";
					}
					else
					{
						return Prop.DisplayValue();
					}
				}
				set
				{
					if (Prop == null)
					{
						if (Tag.Type == "BoolProperty")
						{
							switch (value.ToLower())
							{
								case "true": Tag.BoolVal = 1; break;
								case "1": Tag.BoolVal = 1; break;
								case "false": Tag.BoolVal = 0; break;
								case "0": Tag.BoolVal = 0; break;
							}
						}
						else
						{
							return;
						}
					}
					else
					{
						Prop.Modify(value);
					}
				}
			}

			public bool Editable
			{
				get
				{
					return Tag.Type == "BoolProperty" || (Prop != null && Prop.Editable);
				}
			}

			public bool NotEditable { get { return !Editable; } }
		}

		public class PropertyCrawler
		{
			public PropertyCrawler(IList<PropertyInfo> ListToCrawl, int NewIndex = 0)
			{
				CurrentList = ListToCrawl;
				Index = NewIndex;
			}

			public IList<PropertyInfo> CurrentList;
			public int Index;

			public PropertyInfo Info { get { return CurrentList[Index]; } }

			public PropertyCrawler Next { get { return new PropertyCrawler(CurrentList, Index + 1); } }
			public PropertyCrawler Children { get { return new PropertyCrawler(Info.Children); } }

			public bool HasNext { get { return Index + 1 < CurrentList.Count; } }
			public bool HasChildren { get { return Info.Children.Count > 0; } }
		}

		public IList<PropertyInfo> PropertyList = new List<PropertyInfo>();

		IDictionary<string, Func<UProperty>> PropertyConstructors = new Dictionary<string, Func<UProperty>>()
		{
			{ "FloatProperty", () => new UFloatProperty() },
			{ "NameProperty", () => new UNameProperty() },
			{ "StrProperty", () => new UNameProperty() },
			{ "EnumProperty", () => new UNameProperty() },
			{ "TextProperty", () => new UTextProperty() }
		};

		IDictionary<string, Func<UProperty>> HardcodedStructs = new Dictionary<string, Func<UProperty>>()
		{
			{ "Vector2D", () => new FVector2D() }
		};

		private PropertyInfo ReadProperty(BinaryReader Reader, FPropertyTag Tag = null)
		{
			var Info = new PropertyInfo();

			if (Tag == null)
				Info.Tag.Deserialize(Reader);
			else
				Info.Tag = Tag;

			if (Info.Tag.Name == "None")
				return null;

			if (Info.Tag.Size == 0)
				return Info;

			if (Info.Tag.Type == "StructProperty")
			{
				if (!HardcodedStructs.ContainsKey(Info.Tag.StructName))
				{
					// If it's not hardcoded it has tags for each field
					for (var Child = ReadProperty(Reader); Child != null; Child = ReadProperty(Reader))
						Info.Children.Add(Child);

					return Info;
				}

				Info.Prop = HardcodedStructs[Info.Tag.StructName]();
			}
			else if (Info.Tag.Type == "ArrayProperty")
			{
				var ArrayProp = new UArrayProperty();
				ArrayProp.Deserialize(Reader);
				Info.Prop = ArrayProp;

				for (var i = 0; i < ArrayProp.Length; i++)
				{
					var Child = ReadProperty(Reader, ArrayProp.InnerTag);
					Child.ArrayIndex = i;
					Info.Children.Add(Child);
				}

				return Info;
			}
			else
			{
				if (!PropertyConstructors.ContainsKey(Info.Tag.Type))
				{
					// Unsupported type
					Info.UnsupportedData = Reader.ReadBytes(Info.Tag.Size);
					return Info;
				}

				Info.Prop = PropertyConstructors[Info.Tag.Type]();
			}

			var Data = Reader.ReadBytes(Info.Tag.Size);
			using (var DataStream = new MemoryStream(Data))
			{
				using (var DataReader = new BinaryReader(DataStream))
				{
					Info.Prop.Deserialize(DataReader);
				}
			}

			return Info;
		}

		private void WriteProperty(BinaryWriter Writer, PropertyInfo Info, bool NoTag = false)
		{
			if (!NoTag)
				Info.Tag.Serialize(Writer);

			if (Info.Tag.Name == "None" || Info.Tag.Size == 0)
				return;

			var PropStart = Writer.BaseStream.Position;

			if (Info.Tag.Type == "StructProperty" && !HardcodedStructs.ContainsKey(Info.Tag.StructName))
			{
				// If it's not hardcoded it has tags for each field
				foreach (var Child in Info.Children)
					WriteProperty(Writer, Child);

				// Terminator
				Writer.WriteFString("None");
			}
			else if (Info.Tag.Type == "ArrayProperty")
			{
				var ArrayProp = (UArrayProperty)(Info.Prop);
				ArrayProp.Length = Info.Children.Count;

				Info.Prop.Serialize(Writer);

				var ArrayStart = Writer.BaseStream.Position;

				// No tag on ArrayProperty elements because it's stored in InnerTag
				foreach (var Child in Info.Children)
					WriteProperty(Writer, Child, true);

				// Set InnerTag size
				var ArrayEnd = Writer.BaseStream.Position;
				Writer.BaseStream.Position = ArrayProp.InnerTag.SizeOffset;
				Writer.Write((Int32)(ArrayEnd - ArrayStart));
				Writer.BaseStream.Position = ArrayEnd;
			}
			else if (Info.Prop == null)
			{
				if (Info.UnsupportedData != null)
					Writer.Write(Info.UnsupportedData);
			}
			else
			{
				Info.Prop.Serialize(Writer);
			}

			// Correctly set the size in the tag since it could change
			var PropEnd = Writer.BaseStream.Position;
			Writer.BaseStream.Position = Info.Tag.SizeOffset;
			Writer.Write((Int32)(PropEnd - PropStart));
			Writer.BaseStream.Position = PropEnd;
		}

		public MainWindowTest()
		{
			InitializeComponent();

			byte[] Header;
			Int32 Unknown;
			string Text;

			using (var Settings = File.OpenRead("C:\\Users\\altim\\Documents\\Fortnite\\ClientSettings.sav"))
			{
				using (var Reader = new BinaryReader(Settings))
				{
					Header = Reader.ReadBytes(0x12);

					// Fortnite version
					Text = Reader.ReadFString();
					Unknown = Reader.ReadInt32();

					for (var PropInfo = ReadProperty(Reader); PropInfo != null; PropInfo = ReadProperty(Reader))
						PropertyList.Add(PropInfo);
				}
			}

			using (var Settings = File.OpenWrite("C:\\Users\\altim\\Documents\\Fortnite\\Test.sav"))
			{
				using (var Writer = new BinaryWriter(Settings))
				{
					Writer.Write(Header);
					Writer.WriteFString(Text);
					Writer.Write(Unknown);

					foreach (var Info in PropertyList)
						WriteProperty(Writer, (PropertyInfo)(Info));

					Writer.WriteFString("None");
					Writer.Write(0);
				}
			}

			DataContext = new PropertyCrawler(PropertyList);
			//DataContext = new PropertyInfo { Children = PropertyList };
		}
	}
}
