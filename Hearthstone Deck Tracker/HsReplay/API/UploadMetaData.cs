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
	public class UploadMetaData : QueryStringable
	{
		private readonly GameStats _game;
		private readonly GameMetaData _gameMetaData;
		private int? _friendlyPlayerId;
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
		public int? FriendlyPlayerId => _game?.FriendlyPlayerId ?? _friendlyPlayerId;

		[ApiField("scenario_id")]
		public int? ScenarioId => _game?.ScenarioId;

		[ApiField("player_1")]
		public Player Player1 { get; set; } = new Player();

		[ApiField("player_2")]
		public Player Player2 { get; set; } = new Player();


		public class Player : QueryStringable
		{
			[ApiField("rank")]
			public int? Rank{ get; set; }

			[ApiField("legendrank")]
			public int? LegendRank { get; set; }

			[ApiField("stars")]
			public int? Stars { get; set; }

			[ApiField("wins")]
			public int? Wins { get; set; }

			[ApiField("losses")]
			public int? Losses { get; set; }

			[ApiField("deck")]
			public string DeckList { get; set; }

			[ApiField("cardback")]
			public int? Cardback { get; set; }
		}

		private void FillPlayerData()
		{
			var friendly = new Player();
			var opposing = new Player();

			if(_game?.Rank > 0)
				friendly.Rank = _game.Rank;
			if(_game?.LegendRank > 0)
				friendly.LegendRank = _game.LegendRank;
			if(_game?.PlayerCardbackId > 0)
				friendly.Cardback = _game.PlayerCardbackId;
			if(_game?.Stars > 0)
				friendly.Stars = _game.Stars;
			if(_game?.PlayerCards.Sum(x => x.Count) == 30 && _game?.PlayerCards.Sum(x => x.Unconfirmed) == 0)
				friendly.DeckList = string.Join(",", _game?.PlayerCards.SelectMany(x => Enumerable.Repeat(x.Id, x.Count)));

			if(_game?.OpponentRank > 0)
				opposing.Rank = _game.OpponentRank;
			if(_game?.OpponentLegendRank > 0)
				opposing.LegendRank = _game.OpponentLegendRank;
			if(_game?.OpponentCardbackId > 0)
				opposing.Cardback = _game.OpponentCardbackId;
			if(_game?.OpponentCards.Sum(x => x.Count) == 30 && _game?.OpponentCards.Sum(x => x.Unconfirmed) == 0)
				opposing.DeckList = string.Join(",", _game?.OpponentCards.SelectMany(x => Enumerable.Repeat(x.Id, x.Count)));

			if(_game?.FriendlyPlayerId > 0)
			{
				Player1 = _game.FriendlyPlayerId == 1 ? friendly : opposing;
				Player2 = _game.FriendlyPlayerId == 2 ? friendly : opposing;
			}
			else
			{
				var player1Name = GetPlayer1Name();
				if(player1Name == _game?.PlayerName)
				{
					_friendlyPlayerId = 1;
					Player1 = friendly;
					Player2 = opposing;
				}
				else if(player1Name == _game?.OpponentName)
				{
					_friendlyPlayerId = 2;
					Player2 = friendly;
					Player1 = opposing;
				}
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

	}

	public class QueryStringable
	{
		private IEnumerable<string> GetQueryFields()
		{
			foreach(var prop in GetType().GetProperties().Where(x => x.GetCustomAttributes(typeof(ApiFieldAttribute), false).Any()))
			{
				var value = prop.GetValue(this);
				if(value == null)
					continue;
				var field = ((ApiFieldAttribute)prop.GetCustomAttributes(typeof(ApiFieldAttribute), false).First()).Name;
				var sub = value as QueryStringable;
				if(sub != null)
				{
					foreach(var subField in sub.GetQueryFields())
						yield return $"{field}.{subField}";
				}
				else
					yield return $"{field}={HttpUtility.UrlEncode(value.ToString())}";
			}
		}

		public string ToQueryString() => string.Join("&", GetQueryFields());
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