#region

using System.IO;

#endregion

namespace Hearthstone_Deck_Tracker.HsReplay
{
	internal class Constants
	{
		public const string BaseUrl = "http://hsreplay.net";
		public const string BaseUploadUrl = "https://upload.hsreplay.net";
		private const string BaseApi = "/api/v1";
		private const string RawUploadApi = "/replay/upload/raw";
		private const string TokenApi = "/tokens";
		private const string ClaimAccountApi = "/claim_account/";
		private const string UploadTokenFile = "hsreplay_token";

		public static string BaseApiUrl => BaseUrl + BaseApi;
		public static string BaseUploadApiUrl => BaseUploadUrl + BaseApi;
		public static string UploadUrl => BaseUploadApiUrl + RawUploadApi;
		public static string TokensUrl => BaseApiUrl + TokenApi;
		public static string UploadTokenFilePath => Path.Combine(Config.Instance.DataDir, UploadTokenFile);
		public static string ClaimAccountUrl => BaseApiUrl + ClaimAccountApi;
	}
}