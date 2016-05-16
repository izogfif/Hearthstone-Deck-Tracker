using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using Hearthstone_Deck_Tracker.Enums;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Stats;

namespace Hearthstone_Deck_Tracker.HsReplay.API
{
	public class UploadMetaData
	{
		private readonly GameStats _game;
		private readonly GameMetaData _gameMetaData;
		public readonly string[] Log;
		//[ApiField("friendly_player_id")]
		//public string FriendlyPlayerId { get; set; }

		//[ApiField("scenario_id")]
		//public string ScenarioId { get; set; }


		private UploadMetaData(string[] log, GameMetaData gameMetaData, GameStats game)
		{
			Log = log;
			_gameMetaData = gameMetaData;
			_game = game;
			FillPlayerData();
		}

		[ApiField("game_server_address")]
		public string ServerIp => _gameMetaData?.ServerAddress?.Split(':').FirstOrDefault();

		[ApiField("game_server_port")]
		public string ServerPort => _gameMetaData?.ServerAddress?.Split(':').LastOrDefault();

		[ApiField("game_server_game_id")]
		public string GameId => _gameMetaData?.GameId;

		[ApiField("game_server_client_id")]
		public string ClientId => _gameMetaData?.ClientId;

		[ApiField("game_server_reconnecting")]
		public string Reconnected => _gameMetaData?.Reconnected ?? false ? "true" : null;

		[ApiField("game_server_spectate_key")]
		public string SpectateKey => _gameMetaData?.SpectateKey;

		[ApiField("match_start_timestamp")]
		public string TimeStamp => _game?.StartTime != DateTime.MinValue ? _game?.StartTime.ToString("o") : null;

		[ApiField("hearthstone_build")]
		public int? HearthstoneBuild => _gameMetaData?.HearthstoneBuild;

		[ApiField("game_type")]
		public int? BnetGameType => _game != null ? (int)HearthDbConverter.GetGameType(_game.GameMode, _game.Format) : (int?)null;

		[ApiField("is_spectated_game")]
		public string IsSpectatedGame => _game?.GameMode == GameMode.Spectator ? "true" : null;

		[ApiField("player_1_rank")]
		public int? Player1Rank { get; set; }

		[ApiField("player_1_legend_rank")]
		public int? Player1LegendRank { get; set; }

		[ApiField("player_1_deck_list")]
		public string Player1DeckList { get; set; }

		[ApiField("player_2_rank")]
		public int? Player2Rank { get; set; }

		[ApiField("player_2_legendrank")]
		public int? Player2LegendRank { get; set; }

		[ApiField("player_2_deck_list")]
		public string Player2DeckList { get; set; }

		private void FillPlayerData()
		{
			var player1Name = GetPlayer1Name();
			if(player1Name == _game?.PlayerName)
			{
				if(_game?.Rank > 0)
					Player1Rank = _game?.Rank;
				if(_game?.LegendRank > 0)
					Player1LegendRank = _game?.LegendRank;
				if(_game?.PlayerCards.Sum(x => x.Count) == 30 && _game?.PlayerCards.Sum(x => x.Unconfirmed) == 0)
					Player1DeckList = string.Join(",", _game?.PlayerCards.SelectMany(x => Enumerable.Repeat(x.Id, x.Count)));
				if(_game?.OpponentRank > 0)
					Player2Rank = _game?.OpponentRank;
				if(_game?.OpponentCards.Sum(x => x.Count) == 30 && _game?.OpponentCards.Sum(x => x.Unconfirmed) == 0)
					Player2DeckList = string.Join(",", _game?.OpponentCards.SelectMany(x => Enumerable.Repeat(x.Id, x.Count)));
			}
			else if(player1Name == _game?.OpponentName)
			{
				if(_game?.Rank > 0)
					Player2Rank = _game?.Rank;
				if(_game?.LegendRank > 0)
					Player2LegendRank = _game?.LegendRank;
				if(_game?.PlayerCards.Sum(x => x.Count) == 30 && _game?.PlayerCards.Sum(x => x.Unconfirmed) == 0)
					Player2DeckList = string.Join(",", _game?.PlayerCards.SelectMany(x => Enumerable.Repeat(x.Id, x.Count)));
				if(_game?.OpponentRank > 0)
					Player1Rank = _game?.OpponentRank;
				if(_game?.OpponentCards.Sum(x => x.Count) == 30 && _game?.OpponentCards.Sum(x => x.Unconfirmed) == 0)
					Player1DeckList = string.Join(",", _game?.OpponentCards.SelectMany(x => Enumerable.Repeat(x.Id, x.Count)));
			}
		}

		private string GetPlayer1Name()
		{
			foreach(var line in Log)
			{
				var match = Regex.Match(line, @"TAG_CHANGE Entity=(?<name>(.+)) tag=CONTROLLER value=1");
				if(!match.Success)
					continue;
				return match.Groups["name"].Value;
			}
			return null;
		}

		public static UploadMetaData Generate(string[] logLines, GameMetaData gameMetaData, GameStats game) 
			=> new UploadMetaData(logLines, gameMetaData, game);

		public string ToQueryString()
			=> string.Join("&",
				GetType().GetProperties().Select(x => new
				{
					Field = ((ApiFieldAttribute) x.GetCustomAttributes(typeof(ApiFieldAttribute), false).Single()).Name,
					Value = x.GetValue(this)
				})
				.Where(x => x.Value != null).Select(x => $"{x.Field}={HttpUtility.UrlEncode(x.Value.ToString())}"));
	}

	public class ApiFieldAttribute : Attribute
	{
		public ApiFieldAttribute(string name)
		{
			Name = name;
		}

		public string Name { get; }
	}
}