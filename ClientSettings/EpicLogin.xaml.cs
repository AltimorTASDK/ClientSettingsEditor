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
using System.Windows.Shapes;
using System.Diagnostics;
using System.Net;
using System.IO;
using CefSharp;
using CefSharp.Wpf;
using RestSharp;
using Newtonsoft.Json.Linq;

namespace ClientSettings
{
	/// <summary>
	/// Interaction logic for EpicLogin.xaml
	/// </summary>
	public partial class EpicLogin : Window
	{
		public string Token;
		public string AccountId;

		private class UeCallbacks
		{
			public UeCallbacks(EpicLogin PassedWindow)
			{
				Prompt = new SignInPrompt(PassedWindow);
			}

			public class SignInPrompt
			{
				private EpicLogin LoginWindow;

				public SignInPrompt(EpicLogin PassedWindow)
				{
					LoginWindow = PassedWindow;
				}
				
				private IJavascriptCallback LoggingInCallback;

				public void requestexchangecodesignin(string code, bool unknown)
				{
					LoggingInCallback.ExecuteAsync(true);

					var Request = new RestRequest("account/api/oauth/token", Method.POST);
					Request.AddHeader("Authorization", "basic ZWM2ODRiOGM2ODdmNDc5ZmFkZWEzY2IyYWQ4M2Y1YzY6ZTFmMzFjMjExZjI4NDEzMTg2MjYyZDM3YTEzZmM4NGQ=");
					Request.AddParameter("grant_type", "exchange_code");
					Request.AddParameter("exchange_code", code);
					Request.AddParameter("includePerms", true);
					Request.AddParameter("token_type", "eg1");

					var Client = new RestClient("https://account-public-service-prod03.ol.epicgames.com");
					Client.ExecuteAsync(Request, Response =>
					{
						var Obj = JObject.Parse(Response.Content);
						LoginWindow.Token = (string)(Obj["access_token"]);
						LoginWindow.AccountId = (string)(Obj["account_id"]);
						Application.Current.Dispatcher.Invoke(() => LoginWindow.DialogResult = true);
						Application.Current.Dispatcher.BeginInvoke((Action)(LoginWindow.Close));
					});
				}

				public void requestforgotpassword()
				{
				}

				public void requestofflinesignin()
				{
				}

				public void requestcreateaccount()
				{
				}

				public void onsigninerror(string error)
				{
				}

				public void setloggingincallback(IJavascriptCallback callback)
				{
					LoggingInCallback = callback;
				}
			}

			SignInPrompt Prompt;
			public SignInPrompt signinprompt { get { return Prompt; } }

			public class Environment
			{
			}

			Environment Env = new Environment();
			public Environment environment { get { return Env; } }

			public class Common
			{
				public void launchexternalurl(string url)
				{
					// Make sure it's actually a URL
					try
					{
						var uri = new Uri(url);
						if (uri.GetLeftPart(UriPartial.Scheme) == "https://")
							Process.Start(uri.AbsoluteUri);
					}
					catch (UriFormatException)
					{
					}
				}
			}

			Common Cmn = new Common();
			public Common common { get { return Cmn; } }
		}

		private class ResourceSchemeHandler : ResourceHandler
		{
			public override bool ProcessRequestAsync(IRequest request, ICallback callback)
			{
				Task.Run(() =>
				{
					var uri = new Uri(request.Url);

					try
					{
						var resource = Application.GetResourceStream(new Uri(uri.AbsolutePath, UriKind.Relative));
						MimeType = resource.ContentType;
						StatusCode = (int)(HttpStatusCode.OK);
						Stream = resource.Stream;
						ResponseLength = Stream.Length;

						callback.Continue();
					}
					catch (IOException)
					{
						callback.Cancel();
					}
				});

				return true;
			}
		}

		private class ResourceSchemeHandlerFactory : ISchemeHandlerFactory
		{
			public IResourceHandler Create(IBrowser browser, IFrame frame, string schemeName, IRequest request)
			{
				return new ResourceSchemeHandler();
			}
		}

		private class RenderProcessMessageHandler : IRenderProcessMessageHandler
		{
			public void OnContextCreated(IWebBrowser browserControl, IBrowser browser, IFrame frame)
			{
				frame.ExecuteJavaScriptAsync(@"
				document.addEventListener('DOMContentLoaded', function (event) {
					var script = document.createElement('script');
					script.type = 'text/javascript';
					script.src = 'res://localhost/web/login.js';
					document.body.appendChild(script);
				});");
			}

			public void OnContextReleased(IWebBrowser browserControl, IBrowser browser, IFrame frame)
			{
			}

			public void OnFocusedNodeChanged(IWebBrowser browserControl, IBrowser browser, IFrame frame, IDomNode node)
			{
			}
		}

		private class MenuHandler : IContextMenuHandler
		{
			public void OnBeforeContextMenu(IWebBrowser browserControl, IBrowser browser, IFrame frame, IContextMenuParams parameters, IMenuModel model)
			{
				model.Clear();
			}

			public bool OnContextMenuCommand(IWebBrowser browserControl, IBrowser browser, IFrame frame, IContextMenuParams parameters, CefMenuCommand commandId, CefEventFlags eventFlags)
			{
				return false;
			}

			public void OnContextMenuDismissed(IWebBrowser browserControl, IBrowser browser, IFrame frame)
			{
			}

			public bool RunContextMenu(IWebBrowser browserControl, IBrowser browser, IFrame frame, IContextMenuParams parameters, IMenuModel model, IRunContextMenuCallback callback)
			{
				return false;
			}
		}

		public EpicLogin()
		{
			var settings = new CefSettings();
			settings.RegisterScheme(new CefCustomScheme
			{
				SchemeName = "res",
				SchemeHandlerFactory = new ResourceSchemeHandlerFactory(),
				IsSecure = true
			});

			if (!Cef.IsInitialized)
				Cef.Initialize(settings);

			InitializeComponent();

			Browser.MenuHandler = new MenuHandler();
			Browser.JavascriptObjectRepository.Register("ue", new UeCallbacks(this));
			Browser.RenderProcessMessageHandler = new RenderProcessMessageHandler();
		}

		private void Button_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}

		private void DragBar_MouseDown(object sender, MouseButtonEventArgs e)
		{
			if (e.ChangedButton == MouseButton.Left)
				DragMove();
		}
	}
}
