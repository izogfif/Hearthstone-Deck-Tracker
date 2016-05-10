#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Stats;
using Hearthstone_Deck_Tracker.Utility.Logging;
using Newtonsoft.Json;
using static Hearthstone_Deck_Tracker.HsReplay.Constants;

#endregion

namespace Hearthstone_Deck_Tracker.HsReplay.API
{
	internal class LogUploader
	{
		public static async Task<UploadResult> Upload(string[] logLines, GameMetaData gameMetaData, GameStats game)
		{
			Log.Info("Uploading...");
			try
			{
				var metaData = UploadMetaData.Generate(logLines, gameMetaData, game);
				var log = string.Join(Environment.NewLine, logLines);
				var url = UploadUrl + "?" + metaData.ToQueryString();
				var response = await Web.PostAsync(url, log, true, ApiManager.ApiKeyHeader, await ApiManager.GetUploadTokenHeader());
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
			return await Upload(content.Split(new []{Environment.NewLine}, StringSplitOptions.RemoveEmptyEntries).ToArray(), null, null);
		}
	}
}