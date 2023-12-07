using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Globalization;
using System.Runtime.InteropServices;
using RestSharp;
using Newtonsoft.Json.Linq;

namespace ClientSettings
{
	public class LongToFileSize : IValueConverter
	{
		[DllImport("Shlwapi.dll", CharSet = CharSet.Auto)]
			public static extern Int32 StrFormatByteSize(
			long fileSize,
			[MarshalAs(UnmanagedType.LPTStr)] StringBuilder buffer,
			int bufferSize);

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var Builder = new StringBuilder(32);
			StrFormatByteSize(System.Convert.ToInt64(value), Builder, 32);
			return Builder.ToString();
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}

	/// <summary>
	/// Interaction logic for CloudFileBrowser.xaml
	/// </summary>
	public partial class CloudFileBrowser : Window
	{
		private IList<JToken> CloudFiles = new ObservableCollection<JToken>();

		public string SelectedUniqueFilename { get; private set; }

		public CloudFileBrowser(string Token, string AccountId)
		{
			InitializeComponent();
			DataContext = CloudFiles;
			SendCloudRequest(Token, AccountId);
		}

		private async void SendCloudRequest(string Token, string AccountId)
		{
			var Request = new RestRequest("fortnite/api/cloudstorage/user/" + AccountId, Method.Get);
			Request.AddHeader("Authorization", "bearer " + Token);

			var Client = new RestClient("https://fortnite-public-service-prod11.ol.epicgames.com");
			var Response = await Client.ExecuteAsync(Request);

            var Array = JArray.Parse(Response.Content);
            foreach (var Obj in Array.Root.Children())
                Application.Current.Dispatcher.Invoke((Action<JToken>)(CloudFiles.Add), Obj);
		}

		private void DataGridRow_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			var Row = (DataGridRow)(sender);
			var Item = (JToken)(Row.Item);
			SelectedUniqueFilename = (string)(Item["uniqueFilename"]);
			DialogResult = true;
			Close();
		}

		private void DataGridRow_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key != Key.Enter)
				return;

			var Row = (DataGridRow)(sender);
			var Item = (JToken)(Row.Item);
			SelectedUniqueFilename = (string)(Item["uniqueFilename"]);
			DialogResult = true;
			Close();
		}
	}
}
