using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using RestSharp;
using Newtonsoft.Json.Linq;
using Ionic.Zlib;

namespace ClientSettings
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public class PropertyInfo : INotifyPropertyChanged
		{
			public FPropertyTag Tag = new FPropertyTag();
			public UProperty Prop = null;
			public IList<PropertyInfo> Children { get; set; } = new ObservableCollection<PropertyInfo>();
			public PropertyInfo Parent;

			public event PropertyChangedEventHandler PropertyChanged;

			private int ArrayIndex_ = -1;
			public int ArrayIndex
			{
				get { return ArrayIndex_; }
				set
				{
					ArrayIndex_ = value;
					OnPropertyChanged("Name");
				}
			}

			// For UMapProperty children
			public enum KeyValueType { None, Key, Value }
			public KeyValueType KVType = KeyValueType.None;

			public byte[] RawData = null;

			// For unsupported types or properties that failed to deserialize
			public string FailureReason = null;

			public PropertyInfo Clone()
			{
				var Result = new PropertyInfo
				{
						Tag = Tag?.Clone(),
						Prop = Prop?.Clone(),
						KVType = KVType,
						Parent = Parent,
						ArrayIndex = ArrayIndex,
						RawData = RawData
				};

				var NewChildren = new ObservableCollection<PropertyInfo>();
				foreach (var Child in Children)
				{
					var NewChild = Child.Clone();
					NewChild.Parent = Result;
					NewChildren.Add(NewChild);
				}

				Result.Children = NewChildren;

				return Result;
			}

			public string Name
			{
				get
				{
					if (ArrayIndex >= 0)
					{
						if (KVType == KeyValueType.Key)
							return "[" + ArrayIndex + "].Key";
						else if (KVType == KeyValueType.Value)
							return "[" + ArrayIndex + "].Value";
						else
							return "[" + ArrayIndex + "]";
					}

					return Tag.Name;
				}
				set
				{
					if (ArrayIndex == -1)
						Tag.Name = value;
				}
			}

			public string Type
			{
				get
				{
					return TypeToCPP(Tag.Type);
				}
			}

			public string Value
			{
				get
				{
					if (FailureReason != null)
					{
						return FailureReason;
					}
					else if (Tag.Type == "BoolProperty")
					{
						return Tag.BoolVal != 0 ? "True" : "False";
					}
                    else if (Tag.Type == "StructProperty" && Tag.StructName == "FortActionKeyMapping")
                    {
                        // Show input names without expanding.
                        return Children.Where(x => x.Name == "ActionName").FirstOrDefault()?.Value;
                    }
                    else if (Tag.Type == "StructProperty" && Tag.StructName == "Key")
                    {
                        // Show input names without expanding.
                        return Children.Where(x => x.Name == "KeyName").FirstOrDefault()?.Value;
                    }
                    else if (Tag.Type == "StructProperty" && Prop == null)
                    {
                        // Show HUD visibility names without expanding.
                        return Children.Where(x => x.Name == "TagName").FirstOrDefault()?.Value;
                    }
                    else if (Prop == null)
                    {
                        return null;
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

                        if (Name == "ActionName" || Name == "TagName")
                        {
                            // Update input/HUD visibility mapping name previews.
                            Parent?.OnPropertyChanged("Value");
                        }
					}
				}
			}

			public bool EditableValue
			{
				get
				{
					return Tag.Type == "BoolProperty" || (Prop != null && Prop.Editable);
				}
			}

			public bool EditableName
			{
				get
				{
					return ArrayIndex == -1;
				}
			}

			private string TypeToCPP(string TypeName)
			{
                if (TypeName == "StructProperty" && Tag.StructName == null)
                    return "struct";
                if (TypeName == "StructProperty" && Tag.StructName == "Vector2D")
                    return "FVector2D";
                if (TypeName == "StructProperty" && Tag.StructName == "Guid")
                    return "FGuid";
                if (TypeName == "StructProperty")
                    return "struct " + Tag.StructName;
                if (TypeName == "EnumProperty" && Tag.EnumName == null)
                    return "enum";
                if (TypeName == "EnumProperty")
                    return $"enum {Tag.EnumName}";
                if (TypeName == "ArrayProperty")
                    return (Children.Count > 0 ? Children[0].Type : TypeToCPP(Tag.InnerType)) + "["  + Children.Count + "]";
                if (TypeName == "SetProperty")
                    return "set<" + TypeToCPP(Tag.InnerType) + ">";
                if (TypeName == "IntProperty")
                    return "int";
                if (TypeName == "UInt32Property")
                    return "uint";
                if (TypeName == "ByteProperty" && (Tag.EnumName == null || Tag.EnumName == "None"))
                    return "byte";
                if (TypeName == "ByteProperty")
                    return $"enum {Tag.EnumName}";
                if (TypeName == "FloatProperty")
                    return "float";
                if (TypeName == "BoolProperty")
                    return "bool";
                if (TypeName == "StrProperty")
                    return "string";
                if (TypeName == "NameProperty")
                    return "FName";
                if (TypeName == "TextProperty")
                    return "FText";
                if (TypeName == "MapProperty")
                    return "map<" + TypeToCPP(Tag.InnerType) + ", " + TypeToCPP(Tag.ValueType) + ">";

                return TypeName;
			}

			private void OnPropertyChanged(string Name)
			{
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(Name));
			}

			public void UpdateArrayCount()
			{
				OnPropertyChanged("Type");
			}
		}

		public IList<PropertyInfo> PropertyList = new ObservableCollection<PropertyInfo>();

		private void Duplicate_Click(object sender, RoutedEventArgs e)
		{
			var SenderItem = (MenuItem)(sender);
			var Info = (PropertyInfo)(SenderItem.DataContext);

			if (Info.Parent != null)
			{
				var Index = Info.Parent.Children.IndexOf(Info);
				int StartIndex;

				if (Info.KVType == PropertyInfo.KeyValueType.Key)
				{
					Info.Parent.Children.Insert(Index + 2, Info.Clone());
					Info.Parent.Children.Insert(Index + 3, Info.Parent.Children[Index + 1].Clone());
					StartIndex = Index + 2;
				}
				else if (Info.KVType == PropertyInfo.KeyValueType.Value)
				{
					Info.Parent.Children.Insert(Index + 1, Info.Parent.Children[Index - 1].Clone());
					Info.Parent.Children.Insert(Index + 2, Info.Clone());
					StartIndex = Index + 1;
				}
				else
				{
					Info.Parent.Children.Insert(Index + 1, Info.Clone());
					StartIndex = Index + 1;
				}

				for (var i = StartIndex; i < Info.Parent.Children.Count; i++)
				{
					if (Info.Parent.Children[i].ArrayIndex != -1)
						Info.Parent.Children[i].ArrayIndex++;
				}

				Info.Parent.UpdateArrayCount();
			}
			else
			{
				PropertyList.Insert(PropertyList.IndexOf(Info) + 1, Info.Clone());
			}
		}

		private void Delete_Click(object sender, RoutedEventArgs e)
		{
			var Result = MessageBox.Show("Are you sure?", "Delete Property", MessageBoxButton.YesNo, MessageBoxImage.Question);
			if (Result != MessageBoxResult.Yes)
				return;

			var SenderItem = (MenuItem)(sender);
			var Info = (PropertyInfo)(SenderItem.DataContext);

			if (Info.Parent != null)
			{
				var Index = Info.Parent.Children.IndexOf(Info);

				if (Info.KVType == PropertyInfo.KeyValueType.Key)
				{
					Info.Parent.Children.RemoveAt(Index + 1);
					Info.Parent.Children.RemoveAt(Index);
				}
				else if (Info.KVType == PropertyInfo.KeyValueType.Value)
				{
					Info.Parent.Children.RemoveAt(Index);
					Info.Parent.Children.RemoveAt(Index - 1);
				}
				else
				{
					Info.Parent.Children.RemoveAt(Index);
				}

				for (var i = Index; i < Info.Parent.Children.Count; i++)
				{
					if (Info.Parent.Children[i].ArrayIndex != -1)
						Info.Parent.Children[i].ArrayIndex--;
				}

				Info.Parent.UpdateArrayCount();
			}
			else
			{
				PropertyList.Remove(Info);
			}
		}

		IDictionary<string, Func<UProperty>> PropertyConstructors = new Dictionary<string, Func<UProperty>>()
		{
			{ "IntProperty",    () => new UIntProperty()    },
			{ "UInt32Property", () => new UUInt32Property() },
			{ "ByteProperty",   () => new UByteProperty()   },
			{ "FloatProperty",  () => new UFloatProperty()  },
			{ "NameProperty",   () => new UNameProperty()   },
			{ "StrProperty",    () => new UNameProperty()   },
			{ "EnumProperty",   () => new UNameProperty()   },
			{ "TextProperty",   () => new UTextProperty()   }
		};

		IDictionary<string, Func<UProperty>> HardcodedStructs = new Dictionary<string, Func<UProperty>>()
		{
			{ "Vector2D", () => new FVector2D() },
			{ "DateTime", () => new FDateTime() },
			{ "Guid",	  () => new FGuid()     }
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

			if (Info.Tag.Type == "BoolProperty")
			{
				// Special handling for tagless bools in MapProperty
				if (Tag != null)
				{
					Info.Tag.BoolVal = Reader.ReadByte();
				}
				return Info;
			}
			else if (Info.Tag.Type == "StructProperty")
			{
				// Serializes a naked array
				if (Info.Tag.StructName == "GameplayTagContainer")
				{
					var ArrayProp = new UArrayProperty("NameProperty");
					ArrayProp.Deserialize(Reader);
					Info.Prop = ArrayProp;

					for (var i = 0; i < ArrayProp.Length; i++)
					{
						var Child = ReadProperty(Reader, ArrayProp.InnerTag);
						Child.ArrayIndex = i;
						Child.Parent = Info;
						Info.Children.Add(Child);
					}

					return Info;
				}

                if (Info.Tag.StructName == null || !HardcodedStructs.ContainsKey(Info.Tag.StructName))
                {
                    // If it's not hardcoded it has tags for each field
                    for (var Child = ReadProperty(Reader); Child != null; Child = ReadProperty(Reader))
                    {
                        Child.Parent = Info;
                        Info.Children.Add(Child);
                    }

                    return Info;
                }

                Info.Prop = HardcodedStructs[Info.Tag.StructName]();
			}
			else if (Info.Tag.Type == "MapProperty")
			{
				var MapProp = new UMapProperty();
				var StartOffset = Reader.BaseStream.Position;
				MapProp.Deserialize(Reader);
				Info.Prop = MapProp;

				for (var i = 0; i < MapProp.NumEntries; i++)
				{
					var Key = ReadProperty(Reader, new FPropertyTag(Info.Tag.InnerType));
					Key.ArrayIndex = i;
					Key.KVType = PropertyInfo.KeyValueType.Key;
					Key.Parent = Info;
					Info.Children.Add(Key);

					var Value = ReadProperty(Reader, new FPropertyTag(Info.Tag.ValueType));
					Value.ArrayIndex = i;
					Value.KVType = PropertyInfo.KeyValueType.Value;
					Value.Parent = Info;
					Info.Children.Add(Value);

					if (Key.FailureReason != null || Value.FailureReason != null)
					{
						// It's impossible to skip a MapProperty child that can't be deserialized because the size is unknown
						Reader.BaseStream.Seek(StartOffset, SeekOrigin.Begin);
						Info.RawData = Reader.ReadBytes(Info.Tag.Size);
						Info.FailureReason = "Failed to deserialize child: " + (Key.FailureReason ?? Value.FailureReason);
						Info.Children.Clear();
						return Info;
					}
				}

				return Info;
			}
			else if (Info.Tag.Type == "ArrayProperty")
			{
				var ArrayProp = new UArrayProperty(Info.Tag.InnerType);
				ArrayProp.Deserialize(Reader);
				Info.Prop = ArrayProp;

				for (var i = 0; i < ArrayProp.Length; i++)
				{
					var Child = ReadProperty(Reader, ArrayProp.InnerTag);
					Child.ArrayIndex = i;
					Child.Parent = Info;
					Info.Children.Add(Child);
				}

				return Info;
			}
			else if (Info.Tag.Type == "SetProperty")
			{
				var SetProp = new USetProperty();
				SetProp.Deserialize(Reader);
				Info.Prop = SetProp;

				for (var i = 0; i < SetProp.Length; i++)
				{
                    var Child = ReadProperty(Reader, new FPropertyTag(Info.Tag.InnerType));
					Child.ArrayIndex = i;
					Child.Parent = Info;
					Info.Children.Add(Child);
				}

				return Info;
			}
			else
			{
				if (!PropertyConstructors.ContainsKey(Info.Tag.Type))
				{
					Info.RawData = Reader.ReadBytes(Info.Tag.Size);
					Info.FailureReason = "Unsupported type";
					return Info;
				}

				Info.Prop = PropertyConstructors[Info.Tag.Type]();
			}

			// UMapProperty does not provide a full tag for the inner types, so the size is unknown
			if (Tag != null && Tag.Size == 0)
			{
				try
				{
					Info.Prop.Deserialize(Reader);
				}
				catch (Exception e)
				{
					// Treat it the same as an unsupported property type
					Info.FailureReason = e.Message;
					Info.Prop = null;
				}

				return Info;
			}

			Info.RawData = Reader.ReadBytes(Info.Tag.Size);

			using (var DataStream = new MemoryStream(Info.RawData))
            using (var DataReader = new BinaryReader(DataStream))
            {
                try
                {
                    Info.Prop.Deserialize(DataReader);
                }
                catch (Exception e)
                {
                    // Treat it the same as an unsupported property type
                    Info.FailureReason = e.Message;
                    Info.Prop = null;
                }
            }

			return Info;
		}

		private void WriteProperty(BinaryWriter Writer, PropertyInfo Info, bool NoTag = false)
		{
			if (!NoTag)
				Info.Tag.Serialize(Writer);

			if (Info.Tag.Name == "None")
				return;

			if (Info.Tag.Type == "BoolProperty")
			{
				// Special handling for tagless bools in MapProperty
				if (NoTag)
					Writer.Write(Info.Tag.BoolVal);

				return;
			}

			var PropStart = Writer.BaseStream.Position;

			if (Info.Tag.Type == "ArrayProperty" ||
				(Info.Tag.Type == "StructProperty" && Info.Tag.StructName == "GameplayTagContainer"))
			{
				var ArrayProp = (UArrayProperty)Info.Prop;
				ArrayProp.Length = Info.Children.Count;

				Info.Prop.Serialize(Writer);

				var ArrayStart = Writer.BaseStream.Position;

				// No tag on ArrayProperty elements because it's stored in InnerTag
				foreach (var Child in Info.Children)
					WriteProperty(Writer, Child, true);

				if (ArrayProp.IsStruct)
				{
					// Set InnerTag size
					var ArrayEnd = Writer.BaseStream.Position;
					Writer.BaseStream.Position = ArrayProp.InnerTag.SizeOffset;
					Writer.Write((int)(ArrayEnd - ArrayStart));
					Writer.BaseStream.Position = ArrayEnd;
				}
			}
			else if (Info.Tag.Type == "StructProperty" &&
				(Info.Tag.StructName == null || !HardcodedStructs.ContainsKey(Info.Tag.StructName)))
			{
                // If it's not hardcoded it has tags for each field
                foreach (var Child in Info.Children)
                    WriteProperty(Writer, Child);

                // Terminator
                Writer.WriteFString("None");
			}
			else if (Info.Tag.Type == "MapProperty")
			{
				var MapProp = (UMapProperty)(Info.Prop);
				MapProp.NumEntries = Info.Children.Count / 2;

				MapProp.Serialize(Writer);

				foreach (var Child in Info.Children)
					WriteProperty(Writer, Child, true);
			}
			else if (Info.Prop == null)
			{
				Writer.Write(Info.RawData);
			}
			else
			{
				// Read into a temporary stream in a case an exception occurs
				using (var TempStream = new MemoryStream())
                using (var TempWriter = new BinaryWriter(TempStream))
                {
                    try
                    {
                        Info.Prop.Serialize(TempWriter);
                        Writer.Write(TempStream.ToArray());
                    }
                    catch (UESerializationException)
                    {
                        // Write the original bytes instead
                        Writer.Write(Info.RawData);
                    }
                }
			}

			// Correctly set the size in the tag since it could change
			if (NoTag)
				return;

			var PropEnd = Writer.BaseStream.Position;
			Writer.BaseStream.Position = Info.Tag.SizeOffset;
			Writer.Write((int)(PropEnd - PropStart));
			Writer.BaseStream.Position = PropEnd;
		}

		public class MainViewModel : DependencyObject
		{
			public IList<PropertyInfo> PropertyList { get; set; }

			public static readonly DependencyProperty CanSaveFileProperty = DependencyProperty.Register("CanSaveFile", typeof(Boolean), typeof(MainViewModel));
			public bool CanSaveFile
			{
				get { return (bool)(GetValue(CanSaveFileProperty)); }
				set { SetValue(CanSaveFileProperty, value); }
			}

			public MainViewModel()
			{
				CanSaveFile = false;
			}
		};

		public MainViewModel MVM = new MainViewModel();

		public MainWindow()
		{
			InitializeComponent();

			MVM.PropertyList = PropertyList;
			DataContext = MVM;
		}

		private struct SettingsHeader
		{
			public byte[] Unknown1;
			public string Version;
			public int Unknown2;
			public byte[] Unknown3;
		}

		private SettingsHeader Header;
		private byte[] Footer;
		private string OpenFilePath = null;

        private bool ReadFromStream(Stream Settings)
        {
            using (var Reader = new BinaryReader(Settings))
            {
                try
                {
                    Header.Unknown1 = Reader.ReadBytes(0x16);
                    Header.Version = Reader.ReadFString();
                    Header.Unknown2 = Reader.ReadInt32();
                    Header.Unknown3 = Reader.ReadBytes(0x7AE);

                    Application.Current.Dispatcher.BeginInvoke((Action)(PropertyList.Clear));

#if DEBUG
					var DebugPropertyList = new List<PropertyInfo>();
#endif

					for (var PropInfo = ReadProperty(Reader); PropInfo != null; PropInfo = ReadProperty(Reader))
					{
#if DEBUG
						DebugPropertyList.Add(PropInfo);
#endif
						Application.Current.Dispatcher.BeginInvoke((Action<PropertyInfo>)PropertyList.Add, PropInfo);
					}

					// Enhanced input stuff
					using (var FooterStream = new MemoryStream())
					{
						Settings.CopyTo(FooterStream);
						Footer = FooterStream.ToArray();
					}

                    return true;
                }
                catch (Exception e)
                {
                    Application.Current.Dispatcher.BeginInvoke((Action)PropertyList.Clear);
                    Application.Current.Dispatcher.Invoke(() => MessageBox.Show("Failed to deserialize file. Exception: " + e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error));
                    return false;
                }
            }
        }

		private bool OpenFromStream(Stream Settings)
		{
            var Buffer = new byte[4];
            Settings.Read(Buffer, 0, 4);

            // Check for compressed "ECFD" header magic
            if (Buffer.SequenceEqual(new byte[] { 0x45, 0x43, 0x46, 0x44 }))
            {
                // Skip header
                Settings.Position = 0x10;

                var Data = new byte[Settings.Length - 0x10];
                Settings.Read(Data, 0, Data.Length);

                using (var Decompressed = new MemoryStream(ZlibStream.UncompressBuffer(Data.ToArray())))
                    return ReadFromStream(Decompressed);
            }
            else
            {
                Settings.Position = 0;
                return ReadFromStream(Settings);
            }
		}

		private bool SaveToStream(Stream Settings)
		{
			using (var Writer = new BinaryWriter(Settings))
			{
				try
				{
					Writer.Write(Header.Unknown1);
					Writer.WriteFString(Header.Version);
					Writer.Write(Header.Unknown2);
					Writer.Write(Header.Unknown3);

					foreach (var Info in PropertyList)
						WriteProperty(Writer, Info);

					Writer.WriteFString("None");

					Writer.Write(Footer);

					return true;
				}
				catch (Exception e)
				{
					Application.Current.Dispatcher.Invoke(() => MessageBox.Show("Failed to serialize file. Exception: " + e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error));
					return false;
				}
			}
		}

		private void Open_Click(object sender, RoutedEventArgs e)
		{
			var Dialog = new Microsoft.Win32.OpenFileDialog
			{
				DefaultExt = ".sav",
				Filter = "Fortnite ClientSettings (*.sav)|*.sav"
			};

			var Result = Dialog.ShowDialog();
			if (!Result.HasValue || !Result.Value)
				return;

			try
			{
				using (var Settings = Dialog.OpenFile())
				{
					if (OpenFromStream(Settings))
					{
						OpenFilePath = Dialog.FileName;
						MVM.CanSaveFile = true;
					}
				}
			}
			catch
			{
				MessageBox.Show("Failed to open file.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		private void Save_Click(object sender, RoutedEventArgs e)
		{
			if (!MVM.CanSaveFile)
				return;

			if (OpenFilePath == null)
			{
				SaveAs_Click(sender, e);
				return;
			}

			try
			{
				using (var Settings = File.Open(OpenFilePath, FileMode.Create))
				{
					SaveToStream(Settings);
				}
			}
			catch
			{
				MessageBox.Show("Failed to save file.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		private void SaveAs_Click(object sender, RoutedEventArgs e)
		{
			if (!MVM.CanSaveFile)
				return;

			var Dialog = new Microsoft.Win32.SaveFileDialog
			{
				DefaultExt = ".sav",
				Filter = "Fortnite ClientSettings (*.sav)|*.sav"
			};

			var Result = Dialog.ShowDialog();
			if (!Result.HasValue || !Result.Value)
				return;

			try
			{
				using (var Settings = Dialog.OpenFile())
				{
					if (SaveToStream(Settings))
						OpenFilePath = Dialog.FileName;
				}
			}
			catch
			{
				MessageBox.Show("Failed to save file.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		private string StoredToken = null;
		private string StoredAccountId = null;

		private bool VerifyToken()
		{
			var Request = new RestRequest("account/api/oauth/verify", Method.Get);
			Request.AddHeader("Authorization", "bearer " + StoredToken);

			var Client = new RestClient("https://account-public-service-prod03.ol.epicgames.com");
			var Response = Client.Execute(Request);

			var Obj = JObject.Parse(Response.Content);
			return !Obj.ContainsKey("errorCode");
		}

		private async void CloudImport_Click(object sender, RoutedEventArgs e)
		{
			string Token, AccountId;

			if (StoredToken != null && StoredAccountId != null && VerifyToken())
			{
				Token = StoredToken;
				AccountId = StoredAccountId;
			}
			else
			{
				var LoginWindow = new EpicLogin();
				var LoginResult = LoginWindow.ShowDialog();
				if (LoginResult.HasValue && LoginResult.Value)
				{
					StoredToken = Token = LoginWindow.Token;
					StoredAccountId = AccountId = LoginWindow.AccountId;
				}
				else
				{
					return;
				}
			}

			VerifyToken();

			string UniqueFilename = null;

			var FileBrowser = new CloudFileBrowser(Token, AccountId);
			var Result = FileBrowser.ShowDialog();
			if (Result.HasValue && Result.Value)
				UniqueFilename = FileBrowser.SelectedUniqueFilename;

			if (UniqueFilename == null)
				return;

			var Request = new RestRequest("fortnite/api/cloudstorage/user/" + AccountId + "/" + UniqueFilename, Method.Get);
			Request.AddHeader("Authorization", "bearer " + Token);

			var Client = new RestClient("https://fortnite-public-service-prod11.ol.epicgames.com");
			var Response = await Client.ExecuteAsync(Request);

            using (var Stream = new MemoryStream(Response.RawBytes))
            {
                if (OpenFromStream(Stream))
                    Application.Current.Dispatcher.Invoke(() => MVM.CanSaveFile = true);
            }
		}

		private async void CloudExport_Click(object sender, RoutedEventArgs e)
		{
			string Token, AccountId;

			if (StoredToken != null && StoredAccountId != null && VerifyToken())
			{
				Token = StoredToken;
				AccountId = StoredAccountId;
			}
			else
			{
				var LoginWindow = new EpicLogin();
				var LoginResult = LoginWindow.ShowDialog();
				if (LoginResult.HasValue && LoginResult.Value)
				{
					StoredToken = Token = LoginWindow.Token;
					StoredAccountId = AccountId = LoginWindow.AccountId;
				}
				else
				{
					return;
				}
			}

			string UniqueFilename = null;

			var FileBrowser = new CloudFileBrowser(Token, AccountId);
			var Result = FileBrowser.ShowDialog();
			if (Result.HasValue && Result.Value)
				UniqueFilename = FileBrowser.SelectedUniqueFilename;

			if (UniqueFilename == null)
				return;

			using (var Stream = new MemoryStream())
			{
				if (SaveToStream(Stream))
				{
					var Request = new RestRequest("fortnite/api/cloudstorage/user/" + AccountId + "/" + UniqueFilename, Method.Put);
					Request.AddHeader("Authorization", "bearer " + Token);
					Request.AddHeader("Content-Type", "application/octet-stream");
					Request.AddParameter("application/octet-stream", Stream.ToArray(), ParameterType.RequestBody);

					var Client = new RestClient("https://fortnite-public-service-prod11.ol.epicgames.com");
					var Response = await Client.ExecuteAsync(Request);
                    Application.Current.Dispatcher.Invoke(() => MessageBox.Show(
						Response.StatusCode.ToString(), "Status", MessageBoxButton.OK, MessageBoxImage.Information));
				}
			}
		}

		private void Exit_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}

		// Prevent GridSplitter from dragging too far
		private void UpdateColumnWidth()
		{
			PropertySplitColumn.MaxWidth = Math.Max(ActualWidth - TypeSplitColumn.Width.Value - 150, 0);
			PropertySplitColumn.Width = new GridLength(Math.Max(Math.Min(PropertySplitColumn.Width.Value, PropertySplitColumn.MaxWidth), PropertySplitColumn.MinWidth));
			TypeSplitColumn.MaxWidth = Math.Max(ActualWidth - PropertySplitColumn.Width.Value - 150, 0);
			TypeSplitColumn.Width = new GridLength(Math.Max(Math.Min(TypeSplitColumn.Width.Value, TypeSplitColumn.MaxWidth), PropertySplitColumn.MinWidth));
		}

		private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			if (e.PreviousSize.Width > 0)
			{
				PropertySplitColumn.Width = new GridLength(PropertySplitColumn.Width.Value * e.NewSize.Width / e.PreviousSize.Width);
				TypeSplitColumn.Width = new GridLength(TypeSplitColumn.Width.Value * e.NewSize.Width / e.PreviousSize.Width);
			}

			UpdateColumnWidth();
		}

		private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
		{
			UpdateColumnWidth();
		}
	}
}
