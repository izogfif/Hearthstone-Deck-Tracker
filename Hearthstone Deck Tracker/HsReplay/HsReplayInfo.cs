namespace Hearthstone_Deck_Tracker.HsReplay
{
	public class HsReplayInfo
	{
		public HsReplayInfo()
		{
			
		}

		public HsReplayInfo(string id)
		{
			Id = id;
		}

		public string Id { get; set; }

		public int UploadTries { get; set; }

		public bool Uploaded => !string.IsNullOrEmpty(Id);

		public string Url => $"{Constants.BaseUrl}/games/replay/{Id}";

		public void UploadTry() => UploadTries++;
	}
}
