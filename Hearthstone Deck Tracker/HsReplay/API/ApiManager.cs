using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Hearthstone_Deck_Tracker.Controls.Error;
using Hearthstone_Deck_Tracker.HsReplay.Enums;
using Hearthstone_Deck_Tracker.Utility.Logging;
using Newtonsoft.Json;
using static Hearthstone_Deck_Tracker.HsReplay.Constants;

namespace Hearthstone_Deck_Tracker.HsReplay.API
{
	internal class ApiManager
	{
		private const string ApiKey = "089b2bc6-3c26-4aab-adbe-bcfd5bb48671";
		private const string ApiKeyHeaderName = "x-hsreplay-api-key";

		public static Header ApiKeyHeader => new Header(ApiKeyHeaderName, ApiKey);
		public static async Task<Header> GetUploadTokenHeader() => new Header("Authorization", "Token " + await GetUploadToken());


		internal static void DeleteUploadToken()
		{
			if(File.Exists(UploadTokenFilePath))
			{
				try
				{
					File.Delete(UploadTokenFilePath);
					_uploadToken = null;
				}
				catch(Exception e)
				{
					Log.Error(e);
				}
			}
		}

		internal static string UploadToken => _uploadToken;

		private static string _uploadToken;
		private static async Task<string> GetUploadToken()
		{
			if(!string.IsNullOrEmpty(_uploadToken))
				return _uploadToken;
			string token;
			try
			{
				if(File.Exists(UploadTokenFilePath))
				{
					using(var reader = new StreamReader(UploadTokenFilePath))
						token = reader.ReadToEnd();
					if(!string.IsNullOrEmpty(token))
					{
						Log.Info("Loaded upload-token from file.");
						_uploadToken = token;
						return token;
					}
				}
			}
			catch(Exception e)
			{
				Log.Error(e);
			}
			try
			{
				var content = JsonConvert.SerializeObject(new {api_key = ApiKey});
				var response = await Web.PostJsonAsync($"{TokensUrl}/", content, false);
				using(var responseStream = response.GetResponseStream())
				using(var reader = new StreamReader(responseStream))
				{
					dynamic json = JsonConvert.DeserializeObject(reader.ReadToEnd());
					token = (string)json.key;
				}
				if(string.IsNullOrEmpty(token))
					throw new Exception("Reponse contained no upload-token.");
			}
			catch(Exception e)
			{
				Log.Error(e);
				throw new Exception("Webrequest to obtain upload-token failed.", e);
			}
			try
			{
				using(var writer = new StreamWriter(UploadTokenFilePath))
					writer.Write(token);
			}
			catch(Exception e)
			{
				Log.Error(e);
			}
			if(string.IsNullOrEmpty(token))
				throw new Exception("Could not obtain an upload-token.");
			Log.Info("Obtained new upload-token.");
			_uploadToken = token;
			return token;
		}

		private static async Task<string> GetAccountUrl() => $"{TokensUrl}/{await GetUploadToken()}/";

		public static async Task ClaimAccount()
		{
			try
			{
				var token = await GetUploadToken();
				Log.Info("Getting claim url...");
				var response = await Web.PostAsync(ClaimAccountUrl, string.Empty, false, new Header("Authorization", $"Token {token}"));
				using(var responseStream = response.GetResponseStream())
				using(var reader = new StreamReader(responseStream))
				{
					dynamic json = JsonConvert.DeserializeObject(reader.ReadToEnd());
					Log.Info("Opening browser to claim account...");
					Process.Start($"{BaseUrl}{json.url}");
				}
			}
			catch(Exception e)
			{
				Log.Error(e);
				ErrorManager.AddError("Error claiming account", e.Message);
			}
		}

		public static async Task UpdateAccountStatus()
		{
			Log.Info("Checking account status...");
			var response = await Web.GetAsync(await GetAccountUrl());
			if(response.StatusCode == HttpStatusCode.OK)
			{
				try
				{
					using(var responseStream = response.GetResponseStream())
					using(var reader = new StreamReader(responseStream))
					{
						dynamic json = JsonConvert.DeserializeObject(reader.ReadToEnd());
						var user = json.user;
						Account.Id = user != null ? user.id : 0;
						Account.Username = user != null ? user.username : string.Empty;
						Account.Status = user != null ? AccountStatus.Registered : AccountStatus.Anonymous;
					}
				}
				catch(Exception e)
				{
					Log.Error(e);
				}
			}
			Log.Info($"Response={response.StatusCode}, Id={Account.Id}, Username={Account.Username}, Status={Account.Status}");
		}
	}
}