using System;
using System.Reflection;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Net;
using System.ComponentModel;
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
using System.Windows.Threading;
using System.IO;
using RestSharp;
using Newtonsoft.Json.Linq;

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
			public string FailureReason;

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
							return FailureReason;
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

			// Special handling for tagless bools in MapProperty
			if (Info.Tag.Type == "BoolProperty" && Tag != null)
			{
				Info.Tag.BoolVal = Reader.ReadByte();
				return Info;
			}

			if (Info.Tag.Type == "StructProperty")
			{
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
				}

				return Info;
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

			Info.RawData = Reader.ReadBytes(Info.Tag.Size);
			using (var DataStream = new MemoryStream(Info.RawData))
			{
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
			}

			return Info;
		}

		private void WriteProperty(BinaryWriter Writer, PropertyInfo Info, bool NoTag = false)
		{
			if (!NoTag)
				Info.Tag.Serialize(Writer);

			if (Info.Tag.Name == "None")
				return;

			// Special handling for tagless bools in MapProperty
			if (Info.Tag.Type == "BoolProperty" && NoTag)
			{
				Writer.Write(Info.Tag.BoolVal);
				return;
			}

			var PropStart = Writer.BaseStream.Position;

			if (Info.Tag.Type == "StructProperty" && (Info.Tag.StructName == null || !HardcodedStructs.ContainsKey(Info.Tag.StructName)))
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

				var MapStart = Writer.BaseStream.Position;

				foreach (var Child in Info.Children)
					WriteProperty(Writer, Child, true);
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
				Writer.Write(Info.RawData);
			}
			else
			{
				// Read into a temporary stream in a case an exception occurs
				using (var TempStream = new MemoryStream())
				{
					using (var TempWriter = new BinaryWriter(TempStream))
					{
						try
						{
							Info.Prop.Serialize(TempWriter);
							TempStream.CopyTo(Writer.BaseStream);
						}
						catch (UESerializationException)
						{
							// Write the original bytes instead
							Writer.Write(Info.RawData);
						}
					}
				}
			}

			// Correctly set the size in the tag since it could change
			if (NoTag)
				return;

			var PropEnd = Writer.BaseStream.Position;
			Writer.BaseStream.Position = Info.Tag.SizeOffset;
			Writer.Write((Int32)(PropEnd - PropStart));
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
			public Int32 Unknown2;
		}

		private SettingsHeader Header;
		private string OpenFilePath = null;

		private bool OpenFromStream(Stream Settings)
		{
			using (var Reader = new BinaryReader(Settings))
			{
				try
				{
					Header.Unknown1 = Reader.ReadBytes(0x12);
					Header.Version = Reader.ReadFString();
					Header.Unknown2 = Reader.ReadInt32();

					Application.Current.Dispatcher.BeginInvoke((Action)(PropertyList.Clear));

					for (var PropInfo = ReadProperty(Reader); PropInfo != null; PropInfo = ReadProperty(Reader))
						Application.Current.Dispatcher.BeginInvoke((Action<PropertyInfo>)(PropertyList.Add), PropInfo);

					return true;
				}
				catch (Exception e)
				{
					Application.Current.Dispatcher.BeginInvoke((Action)(PropertyList.Clear));
					Application.Current.Dispatcher.Invoke(() => MessageBox.Show("Failed to deserialize file. Exception: " + e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error));
					return false;
				}
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

					foreach (var Info in PropertyList)
						WriteProperty(Writer, Info);

					Writer.WriteFString("None");
					Writer.Write(0);

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
			var Request = new RestRequest("account/api/oauth/verify", Method.GET);
			Request.AddHeader("Authorization", "bearer " + StoredToken);

			var Client = new RestClient("https://account-public-service-prod03.ol.epicgames.com");
			var Response = Client.Execute(Request);

			var Obj = JObject.Parse(Response.Content);
			return !Obj.ContainsKey("errorCode");
		}

		private void CloudImport_Click(object sender, RoutedEventArgs e)
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

			var Request = new RestRequest("fortnite/api/cloudstorage/user/" + AccountId + "/" + UniqueFilename, Method.GET);
			Request.AddHeader("Authorization", "bearer " + Token);

			var Client = new RestClient("https://fortnite-public-service-prod11.ol.epicgames.com");
			Client.ExecuteAsync(Request, Response =>
			{
				using (var Stream = new MemoryStream(Response.RawBytes))
				{
					if (OpenFromStream(Stream))
						Application.Current.Dispatcher.Invoke(() => MVM.CanSaveFile = true);
				}
			});
		}

		private void CloudExport_Click(object sender, RoutedEventArgs e)
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
					var Request = new RestRequest("fortnite/api/cloudstorage/user/" + AccountId + "/" + UniqueFilename, Method.PUT);
					Request.AddHeader("Authorization", "bearer " + Token);
					Request.AddHeader("Content-Type", "application/octet-stream");
					Request.AddParameter("application/octet-stream", Stream.ToArray(), ParameterType.RequestBody);

					var Client = new RestClient("https://fortnite-public-service-prod11.ol.epicgames.com");
					Client.ExecuteAsync(Request, Response =>
					{
						Application.Current.Dispatcher.Invoke(() => MessageBox.Show(Response.StatusCode.ToString(), "Status", MessageBoxButton.OK, MessageBoxImage.Information));
					});
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
