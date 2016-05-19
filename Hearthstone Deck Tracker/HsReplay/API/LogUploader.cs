#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
		private static readonly List<UploaderItem> InProgress = new List<UploaderItem>();

		public static async Task<UploadResult> Upload(string[] logLines, GameMetaData gameMetaData, GameStats game)
		{
			var log = string.Join(Environment.NewLine, logLines);
			var item = new UploaderItem(log.GetHashCode());
			if(InProgress.Contains(item))
			{
				Log.Info($"{item.Hash} already in progress. Waiting for it to complete...");
				InProgress.Add(item);
				return await item.Result;
			}
			InProgress.Add(item);
			Log.Info($"Uploading {item.Hash}...");
			UploadResult result = null;
			try
			{
				var metaData = UploadMetaData.Generate(logLines, gameMetaData, game);
				var url = UploadUrl + "?" + metaData.ToQueryString();
				Log.Info("Url: " + url);
				var response = await Web.PostAsync(url, log, true, ApiManager.ApiKeyHeader, await ApiManager.GetUploadTokenHeader());
				Log.Info(response.StatusCode.ToString());
				using(var responseStream = response.GetResponseStream())
				using(var reader = new StreamReader(responseStream))
				{
					dynamic json = JsonConvert.DeserializeObject(reader.ReadToEnd());
					string id = json.replay_uuid;
					if(string.IsNullOrEmpty(id))
						throw new Exception("Server returned empty uuid. " + json.msg);
					result = UploadResult.Successful(id);
					if(game != null)
					{
						game.HsReplay = new HsReplayInfo(result.ReplayId);
						if(DefaultDeckStats.Instance.DeckStats.Any(x => x.DeckId == game.DeckId))
							DefaultDeckStats.Save();
						else
							DeckStatsList.Save();
					}
				}
			}
			catch(Exception e)
			{
				Log.Error(e);
			}
			if(result == null)
				result = UploadResult.Failed;
			Log.Info($"{item.Hash} upload done: {(result.Success ? "Success" : "Failed")}");
			foreach(var waiting in InProgress.Where(x => x.Hash == item.Hash))
				waiting.Complete(result);
			InProgress.RemoveAll(x => x.Hash == item.Hash);
			return result;
		}

		public static async Task<UploadResult> FromFile(string filePath)
		{
			string content;
			using(var sr = new StreamReader(filePath))
				content = sr.ReadToEnd();
			return await Upload(content.Split(new []{Environment.NewLine}, StringSplitOptions.RemoveEmptyEntries).ToArray(), null, null);
		}

		public class UploaderItem
		{
			private readonly TaskCompletionSource<UploadResult> _tcs = new TaskCompletionSource<UploadResult>();

			public int Hash { get; }

			public UploaderItem(int hash)
			{
				Hash = hash;
			}

			public override bool Equals(object obj)
			{
				var uObj = obj as UploaderItem;
				return uObj != null && Equals(uObj);
			}

			public override int GetHashCode() => Hash;

			public bool Equals(UploaderItem obj) => obj.Hash == Hash;

			public void Complete(UploadResult result) => _tcs.SetResult(result);

			public Task<UploadResult> Result => _tcs.Task;
		}
	}
}