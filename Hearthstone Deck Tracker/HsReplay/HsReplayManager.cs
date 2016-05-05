#region

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Stats;
using Hearthstone_Deck_Tracker.Utility.Logging;

#endregion

namespace Hearthstone_Deck_Tracker.HsReplay
{
	internal class HsReplayManager
	{
		public static async Task<bool> Setup()
		{
			try
			{
				//await ApiManager.UpdateAccountStatus();
				return true;
			}
			catch(Exception e)
			{
				Log.Error(e);
				return false;
			}
		}

		public static async Task UploadLog(List<string> powerLog, GameStats currentGameStats, GameMetaData metaData)
		{
			//TODO
		}
	}
}