#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using HearthDb.Enums;
using HearthMirror.Objects;
using Hearthstone_Deck_Tracker.Enums;
using Hearthstone_Deck_Tracker.Enums.Hearthstone;
using Hearthstone_Deck_Tracker.Hearthstone.Entities;
using Hearthstone_Deck_Tracker.Replay;
using Hearthstone_Deck_Tracker.Stats;
using Hearthstone_Deck_Tracker.Utility.Logging;
using Hearthstone_Deck_Tracker.Windows;
using MahApps.Metro.Controls.Dialogs;
using System.IO;
using System.Diagnostics;
using Newtonsoft.Json;

#endregion

namespace Hearthstone_Deck_Tracker.Hearthstone
{
	public class GameV2 : IGame
	{
		public readonly List<long> IgnoredArenaDecks = new List<long>();
		private GameMode _currentGameMode = GameMode.None;
		private bool? _spectator;
		private MatchInfo _matchInfo;
		private Mode _currentMode;

		public GameV2()
		{
			Player = new Player(this, true);
			Opponent = new Player(this, false);
			IsInMenu = true;
			OpponentSecrets = new OpponentSecrets(this);
			Reset();
		}

		public List<string> PowerLog { get; } = new List<string>();
		public Deck IgnoreIncorrectDeck { get; set; }
		public GameTime GameTime { get; } = new GameTime();
		public bool IsMinionInPlay => Entities.FirstOrDefault(x => (x.Value.IsInPlay && x.Value.IsMinion)).Value != null;

		public bool IsOpponentMinionInPlay
			=> Entities.FirstOrDefault(x => (x.Value.IsInPlay && x.Value.IsMinion && x.Value.IsControlledBy(Opponent.Id))).Value != null;

		public int OpponentMinionCount => Entities.Count(x => (x.Value.IsInPlay && x.Value.IsMinion && x.Value.IsControlledBy(Opponent.Id)));
		public int PlayerMinionCount => Entities.Count(x => (x.Value.IsInPlay && x.Value.IsMinion && x.Value.IsControlledBy(Player.Id)));

		public Player Player { get; set; }
		public Player Opponent { get; set; }
		public bool IsInMenu { get; set; }
		public bool IsUsingPremade { get; set; }
		public int OpponentSecretCount { get; set; }
		public bool IsRunning { get; set; }
		public Region CurrentRegion { get; set; }
		public GameStats CurrentGameStats { get; set; }
		public OpponentSecrets OpponentSecrets { get; set; }
		public List<Card> DrawnLastGame { get; set; }
		public Dictionary<int, Entity> Entities { get; } = new Dictionary<int, Entity>();
		public GameMetaData MetaData { get; } = new GameMetaData();
		internal List<Tuple<int, List<string>>> StoredPowerLogs { get; } = new List<Tuple<int, List<string>>>();
		internal Dictionary<int, string> StoredPlayerNames { get; } = new Dictionary<int, string>();
		internal GameStats StoredGameStats { get; set; }

		public Mode CurrentMode
		{
			get { return _currentMode; }
			set
			{
				_currentMode = value;
				Log.Info(value.ToString());
			}
		}

		private FormatType _currentFormat = FormatType.FT_UNKNOWN;
		public Format? CurrentFormat
		{
			get
			{
				if(_currentFormat == FormatType.FT_UNKNOWN)
					_currentFormat = (FormatType)HearthMirror.Reflection.GetFormat();
				return HearthDbConverter.GetFormat(_currentFormat);
			}
		}

		public Mode PreviousMode { get; set; }

		public bool SavedReplay { get; set; }

		public Entity PlayerEntity => Entities.FirstOrDefault(x => x.Value?.IsPlayer ?? false).Value;

		public Entity OpponentEntity => Entities.FirstOrDefault(x => x.Value != null && x.Value.HasTag(GameTag.PLAYER_ID) && !x.Value.IsPlayer).Value;

		public Entity GameEntity => Entities.FirstOrDefault(x => x.Value?.Name == "GameEntity").Value;

		public bool IsMulliganDone
		{
			get
			{
				var player = Entities.FirstOrDefault(x => x.Value.IsPlayer);
				var opponent = Entities.FirstOrDefault(x => x.Value.HasTag(GameTag.PLAYER_ID) && !x.Value.IsPlayer);
				if(player.Value == null || opponent.Value == null)
					return false;
				return player.Value.GetTag(GameTag.MULLIGAN_STATE) == (int)Mulligan.DONE
					   && opponent.Value.GetTag(GameTag.MULLIGAN_STATE) == (int)Mulligan.DONE;
			}
		}

		public bool Spectator => _spectator ?? (bool)(_spectator = HearthMirror.Reflection.IsSpectating());

		public GameMode CurrentGameMode
		{
			get
			{
				if(Spectator)
					return GameMode.Spectator;
				if(_currentGameMode == GameMode.None)
					_currentGameMode = HearthDbConverter.GetGameMode((GameType)HearthMirror.Reflection.GetGameType());
				return _currentGameMode;
			}
		}

		public MatchInfo MatchInfo => _matchInfo ?? (_matchInfo = HearthMirror.Reflection.GetMatchInfo());
		private bool _matchInfoCacheInvalid = true;

		internal async void CacheMatchInfo()
		{
			if(!_matchInfoCacheInvalid)
				return;
			_matchInfoCacheInvalid = false;
			MatchInfo matchInfo;
			while((matchInfo = HearthMirror.Reflection.GetMatchInfo()) == null || matchInfo.LocalPlayer == null || matchInfo.OpposingPlayer == null)
				await Task.Delay(1000);
			Log.Info($"{matchInfo.LocalPlayer.Name} vs {matchInfo.OpposingPlayer.Name}");
			_matchInfo = matchInfo;
			Player.Name = matchInfo.LocalPlayer.Name;
			Opponent.Name = matchInfo.OpposingPlayer.Name;
			Player.Id = matchInfo.LocalPlayer.Id;
			Opponent.Id = matchInfo.OpposingPlayer.Id;
		}

		internal void InvalidateMatchInfoCache() => _matchInfoCacheInvalid = true;

		public void Reset(bool resetStats = true)
		{
			Log.Info("-------- Reset ---------");

			ReplayMaker.Reset();
			Player.Reset();
			Opponent.Reset();
			if(!_matchInfoCacheInvalid && MatchInfo?.LocalPlayer != null && MatchInfo.OpposingPlayer != null)
			{
				Player.Name = MatchInfo.LocalPlayer.Name;
				Opponent.Name = MatchInfo.OpposingPlayer.Name;
				Player.Id = MatchInfo.LocalPlayer.Id;
				Opponent.Id = MatchInfo.OpposingPlayer.Id;
			}
			Entities.Clear();
			SavedReplay = false;
			OpponentSecretCount = 0;
			OpponentSecrets.ClearSecrets();
			_spectator = null;
			_currentGameMode = GameMode.None;
			_currentFormat = FormatType.FT_UNKNOWN;
			if(!IsInMenu && resetStats)
				CurrentGameStats = new GameStats(GameResult.None, "", "") {PlayerName = "", OpponentName = "", Region = CurrentRegion};
			PowerLog.Clear();

			if(Core.Game != null && Core.Overlay != null)
			{
				Core.UpdatePlayerCards(true);
				Core.UpdateOpponentCards(true);
			}
            _entitiesOnBoard = new List<Entity>();
            _idsOfEntitiesOnBoard = new List<int>();
            if (logFile != null)
                logFile.Dispose();
            string logFileName = Path.GetTempPath() + "\\log.txt";
            logFile = new StreamWriter(new FileStream(logFileName, FileMode.Create));
        }

		public void StoreGameState()
		{
			if(MetaData.ServerInfo.GameHandle == 0)
				return;
			Log.Info($"Storing PowerLog for gameId={MetaData.ServerInfo.GameHandle}");
			StoredPowerLogs.Add(new Tuple<int, List<string>>(MetaData.ServerInfo.GameHandle, new List<string>(PowerLog)));
			if(Player.Id != -1 && !StoredPlayerNames.ContainsKey(Player.Id))
				StoredPlayerNames.Add(Player.Id, Player.Name);
			if(Opponent.Id != -1 && !StoredPlayerNames.ContainsKey(Opponent.Id))
				StoredPlayerNames.Add(Opponent.Id, Opponent.Name);
			if(StoredGameStats == null)
				StoredGameStats = CurrentGameStats;
		}

		public string GetStoredPlayerName(int id)
		{
			string name;
			StoredPlayerNames.TryGetValue(id, out name);
			return name;
		}

		internal void ResetStoredGameState()
		{
			StoredPowerLogs.Clear();
			StoredPlayerNames.Clear();
			StoredGameStats = null;
		}

        [Serializable]
        class MinionInfo
        {
            public int Position;
            public int PlayerId;
            public string CardId;
            public int Id;
            public string Name;
            public string LocalizedName;
            public int BaseManaCost;
            public int Health;
            public int Attack;
            public int AttackBonus;
            public int MaxHealth;
            public int TemporaryAttackBonus;
            public int HealthBonus;
            public bool Frozen;
            public bool Windfury;
            public bool Taunt;
            public bool Charge;
            public int NumberOfAttacks;
            public bool DivineShield;
            public bool Stealth;
            public bool SummoningSickness;
            public List<Entity> Enchantments;
            private bool IsTagNonZero(Entity minion, GameTag tag)
            {
                return minion.HasTag(tag) && minion.GetTag(tag) != 0;
            }
            public MinionInfo(Entity minion, List<Entity> curEnchantments)
            {
                Debug.Assert(minion.HasTag(GameTag.ZONE_POSITION));
                Position = minion.GetTag(GameTag.ZONE_POSITION);
                PlayerId = minion.GetTag(GameTag.CONTROLLER);
                CardId = minion.CardId;
                Id = minion.Id;
                Name = minion.Name;
                LocalizedName = minion.LocalizedName;
                BaseManaCost = minion.Card.Cost;
                Health = minion.Health;
                Attack = minion.Attack;
                AttackBonus = Attack - minion.Card.Attack;
                MaxHealth = minion.GetTag(GameTag.HEALTH);
                TemporaryAttackBonus = 0; //Maybe we shouldn't use it
                HealthBonus = MaxHealth - minion.Card.Health;
                Frozen = IsTagNonZero(minion, GameTag.FROZEN);
                Windfury = IsTagNonZero(minion, GameTag.WINDFURY);
                Taunt = IsTagNonZero(minion, GameTag.TAUNT);
                Charge = IsTagNonZero(minion, GameTag.CHARGE);
                NumberOfAttacks = minion.GetTag(GameTag.NUM_ATTACKS_THIS_TURN);
                DivineShield = IsTagNonZero(minion, GameTag.DIVINE_SHIELD);
                Stealth = IsTagNonZero(minion, GameTag.STEALTH);
                SummoningSickness = IsTagNonZero(minion, GameTag.EXHAUSTED);
                if (curEnchantments == null)
                    return;
                Enchantments = curEnchantments;
                //if (minion.Card.Mechanics.Contains("Deathrattle") && IsTagNonZero(minion, GameTag.SILENCED))
                //{
                //    //Remove the original deathrattle of the minion
                //    Deathrattle = false;
                //}
                //foreach (Entity enchantment in curEnchantments)
                //{
                //    switch (enchantment.CardId)
                //    {
                //        case HearthDb.CardIds.NonCollectible.Hunter.ExplorersHat_ExplorersHatEnchantment:
                //            //Deathrattles.Add()
                //            break;
                //        //case CardIds.DeathrattleSummonCardIds
                //        default:
                //            break;
                //    }
                //}
            }
        };
        [Serializable]
        class HeroInfo
        {
            public int Health;
            public int MaxHealth;
            public int Armor;
            public int Mana;
            public int MaxMana;
            public int LockedMana;
            public HeroInfo(Entity entity)
            {
                Debug.Assert(entity.IsHero);
                Health = entity.Health;
                MaxHealth = entity.MaxHealth;
                Armor = entity.GetTag(GameTag.ARMOR);
                MaxMana = entity.GetTag(GameTag.RESOURCES);
                Mana = entity.GetTag(GameTag.RESOURCES) + entity.GetTag(GameTag.TEMP_RESOURCES) - entity.GetTag(GameTag.RESOURCES_USED) - entity.GetTag(GameTag.OVERLOAD_LOCKED);
                LockedMana = entity.GetTag(GameTag.OVERLOAD_LOCKED);
            }
        }
        [Serializable]
        class WeaponInfo
        {
            public int BaseAttack;
            public int Attack;
            public int BaseHealth;
            public int Health;
            public int MaxHealth;
        }

        public void DumpBoard()
        {
            DumpBoard(Path.GetTempPath() + "\\log.json");
        }
        public void DumpBoard(string filename)
        {
            /*try
            {
                ReplayMaker.SaveAsJson(filename);
            }
            catch
            {

            }*/
            ////String sText = "";
            Entity hero = null;
            Entity weapon = null;
            List<MinionInfo> minions = new List<MinionInfo>();
            Dictionary<int, List<Entity>> enchantments = new Dictionary<int, List<Entity>>();
            foreach (Entity entity in this.Entities.Values)
            {
                if (!entity.IsInPlay)
                    continue;
                if (!entity.IsEnchantment)
                    continue;
                int attachedTo = entity.GetTag(GameTag.ATTACHED);
                if (!enchantments.ContainsKey(attachedTo))
                    enchantments.Add(attachedTo, new List<Entity>());
                enchantments[attachedTo].Add(entity);
            }
            List<Entity> allEntitiesInPlay = new List<Entity>();
            foreach (int entityId in _idsOfEntitiesOnBoard)
            {
                Debug.Assert(Entities.ContainsKey(entityId));
                Entity entity = Entities[entityId];
                Debug.Assert(entity.IsInPlay && (entity.IsMinion || entity.IsHero || entity.IsHeroPower || entity.IsWeapon || entity.IsEnchantment || entity.IsSpell));
                if (entity.IsMinion)
                {
                    List<Entity> curEnchantments = enchantments.ContainsKey(entity.Id) ? enchantments[entity.Id] : null;
                    minions.Add(new MinionInfo(entity, curEnchantments));
                }
                allEntitiesInPlay.Add(entity);
            }

            Entity playerEntity = null;
            Entity opponentEntity = null;
            Dictionary<int, Entity> cardsInHand = new Dictionary<int, Entity>();
            foreach (Entity entity in Entities.Values)
            {
                if (entity.IsControlledBy(Player.Id) && entity.IsInHand)
                {
                    Debug.Assert(entity.HasTag(GameTag.ZONE_POSITION));
                    cardsInHand[entity.GetTag(GameTag.ZONE_POSITION)] = entity;
                }
                if (entity.IsPlayer)
                {
                    //Debug.Assert(entity.IsControlledBy(Player.Id));
                    playerEntity = entity;
                }
                else if (entity.IsOpponent)
                {
                    //Debug.Assert(entity.IsControlledBy(Opponent.Id));
                    opponentEntity = entity;
                }
            }

            var cardsInHandAndBoard = new {
                player = new
                {
                    playerClass = Player.Class,
                    name = Player.Name,
                    entity = playerEntity
                },
                opponent = new
                {
                    playerClass = Opponent.Class,
                    name = Opponent.Name,
                    cardsInHand = Opponent.HandCount,
                    entity = opponentEntity
                },
                hand = from pair in cardsInHand
                       orderby pair.Key ascending
                       select pair.Value,
                board = allEntitiesInPlay
            };
            using (var json = new MemoryStream())
            {
                using (var sw = new StreamWriter(json))
                {
                    sw.Write(JsonConvert.SerializeObject(cardsInHandAndBoard, Formatting.Indented));
                    sw.Flush();
                    using (var fileStream = new FileStream(Path.GetTempPath() + "\\board_state.json", FileMode.Create))
                    {
                        json.Seek(0, SeekOrigin.Begin);
                        json.CopyTo(fileStream);
                        fileStream.Flush();
                    }
                }
            }
        }
        private List<Entity> _entitiesOnBoard;
        private List<int> _idsOfEntitiesOnBoard;
        public void PutEntityOnBoard(Entity entity)
        {
            Debug.Assert(_idsOfEntitiesOnBoard.Contains(entity.Id) == _entitiesOnBoard.Contains(entity));
            Debug.Assert(!_idsOfEntitiesOnBoard.Contains(entity.Id));
            if (_entitiesOnBoard.Contains(entity))
            {
                //Something is wrong
                float fAux = 1.0f;
                fAux += 1.0f;
                _entitiesOnBoard.Remove(entity);
                _idsOfEntitiesOnBoard.Remove(entity.Id);
            }
            _entitiesOnBoard.Add(entity);
            _idsOfEntitiesOnBoard.Add(entity.Id);
        }
        public void RemoveEntityFromBoard(Entity entity)
        {
            Debug.Assert(_idsOfEntitiesOnBoard.Contains(entity.Id) == _entitiesOnBoard.Contains(entity));
            //Debug.Assert(_idsOfEntitiesOnBoard.Contains(entity.Id));
            if (!_entitiesOnBoard.Contains(entity))
            {
                //Something is wrong
                float fAux = 1.0f;
                fAux += 1.0f;
                return;
            }
            _entitiesOnBoard.Remove(entity);
            _idsOfEntitiesOnBoard.Remove(entity.Id);
        }
        private StreamWriter logFile;
        public void WriteInLog(string str)
        {
            logFile.WriteLine(str);
            logFile.Flush();
        }
        #region Database - Obsolete

        [Obsolete("Use Hearthstone.Database.GetCardFromId", true)]
		public static Card GetCardFromId(string cardId) => Database.GetCardFromId(cardId);

		[Obsolete("Use Hearthstone.Database.GetCardFromName", true)]
		public static Card GetCardFromName(string name, bool localized = false) => Database.GetCardFromName(name, localized);

		[Obsolete("Use Hearthstone.Database.GetActualCards", true)]
		public static List<Card> GetActualCards() => Database.GetActualCards();

		[Obsolete("Use Hearthstone.Database.GetHeroNameFromId", true)]
		public static string GetHeroNameFromId(string id, bool returnIdIfNotFound = true)
			=> Database.GetHeroNameFromId(id, returnIdIfNotFound);

		[Obsolete("Use Hearthstone.Database.IsActualCard", true)]
		public static bool IsActualCard(Card card) => Database.IsActualCard(card);

		#endregion
	}
}