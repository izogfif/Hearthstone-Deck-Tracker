using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using Hearthstone_Deck_Tracker.Enums;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.HsReplay.Converter;
using Hearthstone_Deck_Tracker.Stats;

namespace Hearthstone_Deck_Tracker.HsReplay.API
{
	public class UploadMetaData
	{
		private readonly GameStats _game;
		private readonly GameMetaData _gameMetaData;
		public readonly string[] Log;

		private UploadMetaData(string[] log, GameMetaData gameMetaData, GameStats game)
		{
			Log = log;
			_gameMetaData = gameMetaData;
			_game = game;
			FillPlayerData();
		}

		[ApiField("server_ip")]
		public string ServerIp => _gameMetaData?.ServerAddress?.Split(':').FirstOrDefault();

		[ApiField("server_port")]
		public string ServerPort => _gameMetaData?.ServerAddress?.Split(':').LastOrDefault();

		[ApiField("game_id")]
		public string GameId => _gameMetaData?.GameId;

		[ApiField("client_id")]
		public string ClientId => _gameMetaData?.ClientId;

		[ApiField("reconnecting")]
		public string Reconnected => _gameMetaData?.Reconnected ?? false ? "true" : null;

		[ApiField("spectate_key")]
		public string SpectateKey => _gameMetaData?.SpectateKey;

		[ApiField("match_start_timestamp")]
		public string TimeStamp => _game?.StartTime != DateTime.MinValue ? _game?.StartTime.ToString("o") : null;

		[ApiField("hearthstone_build")]
		public int? HearthstoneBuild => _gameMetaData?.HearthstoneBuild ?? _game?.HearthstoneBuild ?? (_game != null ? BuildDates.GetByDate(_game.StartTime) : null);

		[ApiField("game_type")]
		public int? BnetGameType => _game != null ? (int)HearthDbConverter.GetGameType(_game.GameMode, _game.Format) : (int?)null;

		[ApiField("spectator_mode")]
		public string IsSpectatedGame => _game?.GameMode == GameMode.Spectator ? "true" : null;

		[ApiField("friendly_player")]
		public int? FriendlyPlayerId => _game?.FriendlyPlayerId;

		[ApiField("scenario_id")]
		public int? ScenarioId => _game?.ScenarioId;

		[ApiField("player1_rank")]
		public int? Player1Rank { get; set; }

		[ApiField("player1_legend_rank")]
		public int? Player1LegendRank { get; set; }

		[ApiField("player1_deck")]
		public string Player1DeckList { get; set; }

		[ApiField("player1_cardback")]
		public int? Player1Cardback { get; set; }

		[ApiField("player2_rank")]
		public int? Player2Rank { get; set; }

		[ApiField("player2_legendrank")]
		public int? Player2LegendRank { get; set; }

		[ApiField("player2_deck")]
		public string Player2DeckList { get; set; }

		[ApiField("player2_cardback")]
		public int?	 Player2Cardback { get; set; }

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
				if(_game?.PlayerCardbackId > 0)
					Player1Cardback = _game.PlayerCardbackId;
				if(_game?.OpponentCardbackId > 0)
					Player1Cardback = _game.OpponentCardbackId;
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