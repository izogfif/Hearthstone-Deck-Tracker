#region

using System;
using System.IO;
using System.Threading.Tasks;
using Hearthstone_Deck_Tracker.Utility.Logging;
using Newtonsoft.Json;
using static Hearthstone_Deck_Tracker.HsReplay.Constants;

#endregion

namespace Hearthstone_Deck_Tracker.HsReplay.API
{
	internal class LogUploader
	{
		public static async Task<UploadResult> Upload(string log)
		{
			Log.Info("Uploading...");
			try
			{
				var response = await Web.PostAsync(UploadUrl, log, ApiManager.ApiKeyHeader, await ApiManager.GetUploadTokenHeader());
				Log.Info(response.StatusCode.ToString());
				using(var responseStream = response.GetResponseStream())
				using(var reader = new StreamReader(responseStream))
				{
					dynamic json = JsonConvert.DeserializeObject(reader.ReadToEnd());
					var id = json.replay_uuid;
					Log.Info("Success!");
					return UploadResult.Successful(id);
				}
			}
			catch(Exception e)
			{
				Log.Error(e);
			}
			return UploadResult.Failed;
		}

		public static async Task<UploadResult> FromFile(string filePath)
		{
			string content;
			using(var sr = new StreamReader(filePath))
				content = sr.ReadToEnd();
			return await Upload(content);
		}
	}
}