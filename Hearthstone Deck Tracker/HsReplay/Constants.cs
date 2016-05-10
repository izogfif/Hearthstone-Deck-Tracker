#region

using System.IO;

#endregion

namespace Hearthstone_Deck_Tracker.HsReplay
{
	internal class Constants
	{
		public const string BaseUrl = "https://upload.hsreplay.net";
		private const string BaseApi = "/api/v1";
		private const string RawUploadApi = "/replay/upload/raw";
		private const string GenerateUploadTokenApi = "/agents/generate_single_site_upload_token/";
		private const string UploadTokenFile = "hsreplay_token";

		public static string BaseApiUrl => BaseUrl + BaseApi;
		public static string UploadUrl => BaseApiUrl + RawUploadApi;
		public static string GenerateUploadTokenUrl => BaseApiUrl + GenerateUploadTokenApi;
		public static string UploadTokenFilePath => Path.Combine(Config.Instance.DataDirPath, UploadTokenFile);
	}
}