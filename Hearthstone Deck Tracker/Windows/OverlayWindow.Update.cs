using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HearthDb;
using Hearthstone_Deck_Tracker.Enums;
using Hearthstone_Deck_Tracker.Enums.Hearthstone;
using Hearthstone_Deck_Tracker.Utility;
using Hearthstone_Deck_Tracker.Utility.BoardDamage;
using Hearthstone_Deck_Tracker.Utility.Logging;
using static System.Windows.Visibility;
using static HearthDb.Enums.GameTag;
using static Hearthstone_Deck_Tracker.Controls.Overlay.WotogCounterStyle;
using System.Collections.Generic;
using System.Collections;
using Hearthstone_Deck_Tracker.Hearthstone.Entities;

namespace Hearthstone_Deck_Tracker.Windows
{
	public partial class OverlayWindow
	{
		public void Update(bool refresh)
		{
			if (refresh)
			{
				ListViewPlayer.Items.Refresh();
				ListViewOpponent.Items.Refresh();
				Topmost = false;
				Topmost = true;
				Log.Info("Refreshed overlay topmost status");
			}

			var opponentHandCount = _game.Opponent.HandCount;
			for (var i = 0; i < 10; i++)
			{
				if (i < opponentHandCount)
				{
					var entity = _game.Opponent.Hand.FirstOrDefault(x => x.GetTag(ZONE_POSITION) == i + 1);
					if(entity == null)
						continue;
					_cardMarks[i].Text = !Config.Instance.HideOpponentCardAge ? entity.Info.Turn.ToString() : "";
					if(!Config.Instance.HideOpponentCardMarks)
					{
						_cardMarks[i].Mark = entity.Info.CardMark;
						_cardMarks[i].SetCostReduction(entity.Info.CostReduction);
					}
					else
						_cardMarks[i].Mark = CardMark.None;
					_cardMarks[i].Visibility = (_game.IsInMenu || (Config.Instance.HideOpponentCardAge && Config.Instance.HideOpponentCardMarks))
												   ? Hidden : Visible;
				}
				else
					_cardMarks[i].Visibility = Collapsed;
			}

			var oppBoard = Core.Game.Opponent.Board.Where(x => x.IsMinion).OrderBy(x => x.GetTag(ZONE_POSITION)).ToList();
			var playerBoard = Core.Game.Player.Board.Where(x => x.IsMinion).OrderBy(x => x.GetTag(ZONE_POSITION)).ToList();
			UpdateMouseOverDetectionRegions(oppBoard, playerBoard);
			if(!_game.IsInMenu && _game.IsMulliganDone && User32.IsHearthstoneInForeground() && IsVisible)
				DetectMouseOver(playerBoard, oppBoard);
			else
				FlavorTextVisibility = Collapsed;

			StackPanelPlayer.Opacity = Config.Instance.PlayerOpacity / 100;
			StackPanelOpponent.Opacity = Config.Instance.OpponentOpacity / 100;
			StackPanelSecrets.Opacity = Config.Instance.SecretsOpacity / 100;
			Opacity = Config.Instance.OverlayOpacity / 100;

			if (!_playerCardsHidden)
			{
				StackPanelPlayer.Visibility = (Config.Instance.HideDecksInOverlay || (Config.Instance.HideInMenu && _game.IsInMenu)) && !_uiMovable
												  ? Collapsed : Visible;
			}

			if (!_opponentCardsHidden)
			{
				StackPanelOpponent.Visibility = (Config.Instance.HideDecksInOverlay || (Config.Instance.HideInMenu && _game.IsInMenu))
												&& !_uiMovable ? Collapsed : Visible;
			}

			CanvasPlayerChance.Visibility = Config.Instance.HideDrawChances ? Collapsed : Visible;
			LblPlayerFatigue.Visibility = Config.Instance.HidePlayerFatigueCount ? Collapsed : Visible;
			CanvasPlayerCount.Visibility = Config.Instance.HidePlayerCardCount ? Collapsed : Visible;
            LblPlayerTotalStrength.Visibility = Visible;
            LblPlayerDeadDeathrattleMinions.Visibility = Visible;

			CanvasOpponentChance.Visibility = Config.Instance.HideOpponentDrawChances ? Collapsed : Visible;
			LblOpponentFatigue.Visibility = Config.Instance.HideOpponentFatigueCount ? Collapsed : Visible;
			CanvasOpponentCount.Visibility = Config.Instance.HideOpponentCardCount ? Collapsed : Visible;

			if (_game.IsInMenu && !_uiMovable)
				HideTimers();

			ListViewOpponent.Visibility = Config.Instance.HideOpponentCards ? Collapsed : Visible;
			ListViewPlayer.Visibility = Config.Instance.HidePlayerCards ? Collapsed : Visible;

			var gameStarted = !_game.IsInMenu && _game.Entities.Count >= 67 && _game.Player.PlayerEntities.Any();
			SetCardCount(_game.Player.HandCount, !gameStarted ? 30 : _game.Player.DeckCount);
            UpdatePlayerTotalStrength();
            UpdatePlayerListOfDeadDeathrattleMinions();

            SetOpponentCardCount(_game.Opponent.HandCount, !gameStarted ? 30 : _game.Opponent.DeckCount);
            UpdateOpponentTotalStrength();
            UpdateOpponentListOfDeadDeathrattleMinions();


			LblWins.Visibility = Config.Instance.ShowDeckWins && _game.IsUsingPremade ? Visible : Collapsed;
			LblDeckTitle.Visibility = Config.Instance.ShowDeckTitle && _game.IsUsingPremade ? Visible : Collapsed;
			LblWinRateAgainst.Visibility = Config.Instance.ShowWinRateAgainst && _game.IsUsingPremade
											   ? Visible : Collapsed;

			var showWarning = !_game.IsInMenu && DeckList.Instance.ActiveDeckVersion != null && DeckManager.NotFoundCards.Any() && DeckManager.IgnoredDeckId != DeckList.Instance.ActiveDeckVersion.DeckId;
			StackPanelWarning.Visibility = showWarning ? Visible : Collapsed;
			if(showWarning)
			{
				LblWarningCards.Text = string.Join(", ", DeckManager.NotFoundCards.Take(Math.Min(DeckManager.NotFoundCards.Count, 3)).Select(c => c.LocalizedName));
				if(DeckManager.NotFoundCards.Count > 3)
					LblWarningCards.Text += ", ...";
			}

			if (_game.IsInMenu)
			{
				if (Config.Instance.AlwaysShowGoldProgress)
				{
					UpdateGoldProgress();
					GoldProgressGrid.Visibility = Visible;
				}
			}
			else
				GoldProgressGrid.Visibility = Collapsed;

			UpdateIcons();

			SetDeckTitle();
			SetWinRates();

			UpdateElementSizes();
			UpdateElementPositions();


			if (Core.Windows.PlayerWindow.Visibility == Visible)
				Core.Windows.PlayerWindow.Update();
			if (Core.Windows.OpponentWindow.Visibility == Visible)
				Core.Windows.OpponentWindow.Update();
		}

        private bool DfsDamage(List<Entity> hand, List<Entity> entitiesPlayed, bool auchenaiOnboard, int doubleDamage, int spellDamage, int manaAvailable, int opponentHealth,
            Entity maximumSpellDamageMinionOnTheBoard, ref int minimumOpponentHealth, ref List<Entity> entitiesPlayedToMinimumOpponentHealth)
        {
            foreach (Entity entity in hand)
            {
                int cardCost = entity.GetTag(COST);
                bool newAuchenaiOnBoard = auchenaiOnboard;
                int newDoubleDamage = doubleDamage;
                int newSpellDamage = spellDamage;
                int newManaAvailable = manaAvailable;
                int newOpponentHealth = opponentHealth;
                if (cardCost <= manaAvailable)
                {
                    //Try to play this entity
                    //First, if this is SpellPower minion, try to play it
                    if (entity.GetTag(SPELLPOWER) > 0)
                    {
                        UpdateMaximumSpellDamageMinionOnTheBoard(entity, ref maximumSpellDamageMinionOnTheBoard);
                        newManaAvailable -= cardCost;
                        newSpellDamage += entity.GetTag(SPELLPOWER);
                    }
                    else //Check cards individually
                    {
                        switch (entity.CardId)
                        {
                            case "EX1_564": //Faceless Manipulator
                            {
                                //Try to duplicate Prophet Velen if possible, otherwise maximum spell damage minion
                                if (doubleDamage > 0)
                                {
                                    newManaAvailable -= cardCost;
                                    ++newDoubleDamage;
                                    break;
                                }
                                if (maximumSpellDamageMinionOnTheBoard != null)
                                {
                                    newManaAvailable -= cardCost;
                                    newSpellDamage += maximumSpellDamageMinionOnTheBoard.GetTag(SPELLPOWER);
                                    break;
                                }
                                //Don't play it otherwise
                                continue;
                            }
                            case "EX1_350": //Prophet Velen
                            {
                                newManaAvailable -= cardCost;
                                ++newDoubleDamage;
                                break;
                            }
                            case "CS1_130": //Holy smite
                            {
                                newManaAvailable -= cardCost;
                                newOpponentHealth -= (spellDamage + 2) * (1 << doubleDamage);
                                break;
                            }
                            case "DS1_233": //Mind blast
                            {
                                newManaAvailable -= cardCost;
                                newOpponentHealth -= (spellDamage + 5) * (1 << doubleDamage);
                                break;
                            }
                            case "AT_055": //Flash heal
                            {
                                //No need to use healing spell if there is no friendly Auchenai Soulpriest on the board
                                if (!auchenaiOnboard)
                                    continue;
                                newManaAvailable -= cardCost;
                                newOpponentHealth -= (spellDamage + 5) * (1 << doubleDamage);
                                break;
                            }
                            case "GVG_012": //Light of the Naaru
                            {
                                if (!auchenaiOnboard)
                                    continue;
                                newManaAvailable -= cardCost;
                                newOpponentHealth -= (spellDamage + 3) * (1 << doubleDamage);
                                break;
                            }
                            case "EX1_591": //Auchenai Soulpriest
                            {
                                if (auchenaiOnboard) //There is no need to put second Auchenai Soulrpiest if we already own friendly one
                                    continue;
                                newManaAvailable -= cardCost;
                                newAuchenaiOnBoard = true;
                                break;
                            }
                            default:
                                continue;
                        }
                    }
                    List<Entity> modifiedEntitiesPlayed = entitiesPlayed.ToList();
                    modifiedEntitiesPlayed.Add(entity);
                    if (minimumOpponentHealth > newOpponentHealth)
                    {
                        minimumOpponentHealth = newOpponentHealth;
                        entitiesPlayedToMinimumOpponentHealth = modifiedEntitiesPlayed;
                    }
                    if (newOpponentHealth <= 0)
                        return true;

                    List<Entity> modifiedHand = hand.ToList();
                    modifiedHand.Remove(entity);
                    if (DfsDamage(modifiedHand, modifiedEntitiesPlayed, newAuchenaiOnBoard, newDoubleDamage, newSpellDamage, newManaAvailable, newOpponentHealth, maximumSpellDamageMinionOnTheBoard,
                        ref minimumOpponentHealth, ref entitiesPlayedToMinimumOpponentHealth))
                        return true;
                }
            }
            return false;
        }
        private void UpdateMaximumSpellDamageMinionOnTheBoard(Entity entity, ref Entity maximumSpellDamageMinionOnTheBoard)
        {
            if (entity.GetTag(SPELLPOWER) > 0)
            {
                if (maximumSpellDamageMinionOnTheBoard == null)
                    maximumSpellDamageMinionOnTheBoard = entity;
                else if (entity.GetTag(SPELLPOWER) > maximumSpellDamageMinionOnTheBoard.GetTag(SPELLPOWER))
                    maximumSpellDamageMinionOnTheBoard = entity;
            }
        }

        private void UpdateIcons()
		{
			IconBoardAttackPlayer.Visibility = Config.Instance.HidePlayerAttackIcon || _game.IsInMenu ? Collapsed : Visible;
			IconBoardAttackOpponent.Visibility = Config.Instance.HideOpponentAttackIcon || _game.IsInMenu ? Collapsed : Visible;

            bool specialCaseForSpellPriestFound = false;
			// do the calculation if at least one of the icons is visible
			if (_game.Entities.Count > 67 && (IconBoardAttackPlayer.Visibility == Visible || IconBoardAttackOpponent.Visibility == Visible))
			{
				var board = new BoardState();
                if (_game.Player.Class == "Priest")
                { 
                    bool damagingSpellsFound = false;
                    bool healingSpellsFound = false;
                    bool auchenaiFound = false;
                    //First, find spells in the player's hand
                    foreach (Entity entity in _game.Player.Hand)
                    {
                        switch (entity.CardId)
                        {
                            case "CS1_130": //Holy smite
                                damagingSpellsFound = true;
                                break;
                            case "AT_055": //Flash heal
                                healingSpellsFound = true;
                                break;
                            case "DS1_233": //Mind blast
                                damagingSpellsFound = true;
                                break;
                            case "GVG_012": //Light of the Naaru
                                healingSpellsFound = true;
                                break;
                            case "EX1_591": //Auchenai Soulpriest
                                auchenaiFound = true;
                                break;
                        }
                    }
                    if (damagingSpellsFound || healingSpellsFound && auchenaiFound) //There is something to do
                    {
                        List<Entity> entitiesPlayedToMinimumOpponentHealth = new List<Entity>();
                        int doubleDamage = 0;
                        int spellDamage = 0;
                        bool auchenaiOnBoard = false;
                        bool prophetVelenOnBoard = false;
                        Entity maximumSpellDamageMinionOnTheBoard = null;
                        foreach (Entity entity in _game.Player.Board)
                            UpdateMaximumSpellDamageMinionOnTheBoard(entity, ref maximumSpellDamageMinionOnTheBoard);
                        foreach (Entity entity in _game.Opponent.Board)
                            UpdateMaximumSpellDamageMinionOnTheBoard(entity, ref maximumSpellDamageMinionOnTheBoard);
                        //foreach (Entity entity in _game.Player.Hand)
                        //{
                        //    if (entity.GetTag(COST) != 0)
                        //        continue;
                        //    if (entity.CardId == "EX1_350") //Prophet Velen
                        //    {
                        //        ++doubleDamage;
                        //        prophetVelenOnBoard = true;
                        //    }
                        //    UpdateMaximumSpellDamageMinionOnTheBoard(entity, ref maximumSpellDamageMinionOnTheBoard);
                        //    spellDamage += entity.GetTag(SPELLPOWER);
                        //    if (entity.CardId == "EX1_591") //Auchenai Soulpriest
                        //        auchenaiOnBoard = true;
                        //}
                        //if (!prophetVelenOnBoard) //Maybe opponent has it?
                        //{
                        //    foreach (Entity entity in _game.Entities.Values)
                        //    {
                        //        if (entity.IsInPlay && entity.CardId == "EX1_350")
                        //        {
                        //            prophetVelenOnBoard = true;
                        //            break;
                        //        }
                        //    }
                        //}
                        ////If there is Prophet Velen on the board and we have 0-cost Faceless Manipulator(s), duplicate Velen
                        //foreach (Entity entity in _game.Player.Hand)
                        //{
                        //    if (entity.GetTag(COST) != 0)
                        //        continue;
                        //    if (entity.CardId == "EX1_564") //Faceless Manipulator
                        //        ++doubleDamage;
                        //}
                        //Add spell power bonus from all friendly minions on the board
                        foreach (Entity entity in _game.Player.Board)
                            spellDamage += entity.GetTag(SPELLPOWER);
                        //Now, that we've already put all useful 0-mana cost cards on the board, let's calculate how much damage we can deal
                        int currentMana = _game.PlayerEntity.GetTag(RESOURCES) + _game.PlayerEntity.GetTag(TEMP_RESOURCES) - _game.PlayerEntity.GetTag(RESOURCES_USED) - _game.PlayerEntity.GetTag(OVERLOAD_LOCKED);
                        List<Entity> hand = _game.Player.Hand.ToList();
                        int currentOpponentHealth = board.Opponent.Hero?.Health ?? 0;
                        int minimumOpponentHealth = currentOpponentHealth;
                        DfsDamage(hand, new List<Entity>(), auchenaiOnBoard, doubleDamage, spellDamage, currentMana, board.Opponent.Hero?.Health ?? 0, maximumSpellDamageMinionOnTheBoard, ref minimumOpponentHealth, ref entitiesPlayedToMinimumOpponentHealth);
                        specialCaseForSpellPriestFound = true;
                        int playerDamage = board.Player?.Damage ?? 0;
                        if (minimumOpponentHealth <= playerDamage)
                        {
                            String cardsPlayed = (playerDamage) + " + " + (currentOpponentHealth - minimumOpponentHealth) + ":\n";
                            foreach (Entity playedCard in entitiesPlayedToMinimumOpponentHealth)
                            {
                                if (!cardsPlayed.EndsWith(":\n"))
                                    cardsPlayed += "+\n";
                                cardsPlayed += playedCard.LocalizedName;
                            }
                            //cardsPlayed = "Found lethal: attack with all minions for " + playerDamage + " and then play\n" + cardsPlayed + " for " + (currentOpponentHealth - minimumOpponentHealth) + " damage";
                            TextBlockPlayerAttack.Text = cardsPlayed;
                            //TextBlockPlayerAttack.Height = System.Double.NaN;
                            //TextBlockPlayerAttack.TextWrapping = TextWrapping.Wrap;
                            //TextBlockPlayerAttack.MaxHeight = Double.PositiveInfinity;
                            TextBlockPlayerAttack.FontSize = 7;
                        }
                        else
                        {
                            TextBlockPlayerAttack.Text = (minimumOpponentHealth - playerDamage).ToString();
                            TextBlockPlayerAttack.FontSize = 21;
                        }
                    }
                }
                if (!specialCaseForSpellPriestFound)
                {
                    int damageToLethal = (board.Opponent.Hero?.Health ?? 0) - (board.Player?.Damage ?? 0);
                    TextBlockPlayerAttack.Text = damageToLethal.ToString();
                }
                TextBlockOpponentAttack.Text = board.Opponent.Damage.ToString();
			}


			var showPlayerCthunCounter = WotogCounterHelper.ShowPlayerCthunCounter;
			var showPlayerSpellsCounter = WotogCounterHelper.ShowPlayerSpellsCounter;
			if(showPlayerCthunCounter)
			{
				var proxy = WotogCounterHelper.PlayerCthunProxy;
				WotogIconsPlayer.Attack = (proxy?.Attack ?? 6).ToString();
				WotogIconsPlayer.Health = (proxy?.Health ?? 6).ToString();
			}
			if(showPlayerSpellsCounter)
				WotogIconsPlayer.Spells = _game.Player.SpellsPlayedCount.ToString();
			WotogIconsPlayer.WotogCounterStyle = showPlayerCthunCounter && showPlayerSpellsCounter ? Full : (showPlayerCthunCounter ? Cthun : (showPlayerSpellsCounter ? Spells : None));

			var showOpponentCthunCounter = WotogCounterHelper.ShowOpponentCthunCounter;
			var showOpponentSpellsCounter = WotogCounterHelper.ShowOpponentSpellsCounter;
			if(showOpponentCthunCounter)
			{
				var proxy = WotogCounterHelper.OpponentCthunProxy;
				WotogIconsOpponent.Attack = (proxy?.Attack ?? 6).ToString();
				WotogIconsOpponent.Health = (proxy?.Health ?? 6).ToString();
			}
			if(showOpponentSpellsCounter)
				WotogIconsOpponent.Spells = _game.Opponent.SpellsPlayedCount.ToString();
			WotogIconsOpponent.WotogCounterStyle = showOpponentCthunCounter && showOpponentSpellsCounter ? Full : (showOpponentCthunCounter ? Cthun : (showOpponentSpellsCounter ? Spells : None));

		}

		private void UpdateGoldProgress()
		{
			var region = (int)_game.CurrentRegion - 1;
			if (region < 0)
				return;
			var wins = Config.Instance.GoldProgress[region];
			if (wins >= 0)
				LblGoldProgress.Text = $"Wins: {wins}/3 ({Config.Instance.GoldProgressTotal[region]}/100G)";
		}


		public void UpdatePosition()
		{
			//hide the overlay depenting on options
			ShowOverlay(
						!((Config.Instance.HideInBackground && !User32.IsHearthstoneInForeground())
						  || (Config.Instance.HideOverlayInSpectator && _game.CurrentGameMode == GameMode.Spectator) || Config.Instance.HideOverlay
						  || ForceHidden || Helper.GameWindowState == WindowState.Minimized));


			var hsRect = User32.GetHearthstoneRect(true);

			//hs window has height 0 if it just launched, screwing things up if the tracker is started before hs is. 
			//this prevents that from happening. 
			if (hsRect.Height == 0 || (Visibility != Visible && Core.Windows.CapturableOverlay == null))
				return;

			var prevWidth = Width;
			var prevHeight = Height;
			SetRect(hsRect.Top, hsRect.Left, hsRect.Width, hsRect.Height);
			if (Width != prevWidth)
				OnPropertyChanged(nameof(BoardWidth));
			if (Height != prevHeight)
			{
				OnPropertyChanged(nameof(BoardHeight));
				OnPropertyChanged(nameof(MinionMargin));
				OnPropertyChanged(nameof(MinionWidth));
				OnPropertyChanged(nameof(CardWidth));
				OnPropertyChanged(nameof(CardHeight));
			}

			UpdateElementSizes();
			UpdateElementPositions();

			try
			{
				if(Visibility == Visible)
					UpdateCardTooltip();
			}
			catch (Exception ex)
			{
				Log.Error(ex);
			}
		}

		internal void UpdateTurnTimer(TimerState timerState)
		{
			if((timerState.PlayerSeconds <= 0 && timerState.OpponentSeconds <= 0) || _game.CurrentMode != Mode.GAMEPLAY)
				return;
			ShowTimers();
			var seconds = (int)Math.Abs(timerState.Seconds);
			LblTurnTime.Text = double.IsPositiveInfinity(timerState.Seconds) ? "\u221E" : $"{seconds / 60 % 60:00}:{seconds % 60:00}";
			LblTurnTime.Fill = timerState.Seconds < 0 ? Brushes.LimeGreen : Brushes.White;
			LblPlayerTurnTime.Text = $"{timerState.PlayerSeconds / 60 % 60:00}:{timerState.PlayerSeconds % 60:00}";
			LblOpponentTurnTime.Text = $"{timerState.OpponentSeconds / 60 % 60:00}:{timerState.OpponentSeconds % 60:00}";
		}

		public void UpdateScaling()
		{
			StackPanelPlayer.RenderTransform = new ScaleTransform(Config.Instance.OverlayPlayerScaling / 100,
																  Config.Instance.OverlayPlayerScaling / 100);
			StackPanelOpponent.RenderTransform = new ScaleTransform(Config.Instance.OverlayOpponentScaling / 100,
																	Config.Instance.OverlayOpponentScaling / 100);
			StackPanelSecrets.RenderTransform = new ScaleTransform(Config.Instance.SecretsPanelScaling, Config.Instance.SecretsPanelScaling);
		}

		private void UpdateElementPositions()
		{
			Canvas.SetTop(BorderStackPanelPlayer, Height * Config.Instance.PlayerDeckTop / 100);
			Canvas.SetLeft(BorderStackPanelPlayer, Width * Config.Instance.PlayerDeckLeft / 100 - StackPanelPlayer.ActualWidth * Config.Instance.OverlayPlayerScaling / 100);
			Canvas.SetTop(BorderStackPanelOpponent, Height * Config.Instance.OpponentDeckTop / 100);
			Canvas.SetLeft(BorderStackPanelOpponent, Width * Config.Instance.OpponentDeckLeft / 100);
			Canvas.SetTop(StackPanelSecrets, Height * Config.Instance.SecretsTop / 100);
			Canvas.SetLeft(StackPanelSecrets, Width * Config.Instance.SecretsLeft / 100);
			Canvas.SetTop(LblTurnTime, Height * Config.Instance.TimersVerticalPosition / 100 - 5);
			Canvas.SetLeft(LblTurnTime, Width * Config.Instance.TimersHorizontalPosition / 100);
			Canvas.SetTop(LblOpponentTurnTime, Height * Config.Instance.TimersVerticalPosition / 100 - Config.Instance.TimersVerticalSpacing);
			Canvas.SetLeft(LblOpponentTurnTime, Width * Config.Instance.TimersHorizontalPosition / 100 + Config.Instance.TimersHorizontalSpacing);
			Canvas.SetTop(LblPlayerTurnTime, Height * Config.Instance.TimersVerticalPosition / 100 + Config.Instance.TimersVerticalSpacing);
			Canvas.SetLeft(LblPlayerTurnTime, Width * Config.Instance.TimersHorizontalPosition / 100 + Config.Instance.TimersHorizontalSpacing);
			Canvas.SetTop(WotogIconsPlayer, Height * Config.Instance.WotogIconsPlayerVertical / 100);
			Canvas.SetLeft(WotogIconsPlayer, Helper.GetScaledXPos(Config.Instance.WotogIconsPlayerHorizontal / 100, (int)Width, ScreenRatio));
			Canvas.SetTop(WotogIconsOpponent, Height * Config.Instance.WotogIconsOpponentVertical / 100);
			Canvas.SetLeft(WotogIconsOpponent, Helper.GetScaledXPos(Config.Instance.WotogIconsOpponentHorizontal / 100, (int)Width, ScreenRatio));
			Canvas.SetTop(IconBoardAttackPlayer, Height * Config.Instance.AttackIconPlayerVerticalPosition / 100);
			Canvas.SetLeft(IconBoardAttackPlayer, Helper.GetScaledXPos(Config.Instance.AttackIconPlayerHorizontalPosition / 100, (int)Width, ScreenRatio));
			Canvas.SetTop(IconBoardAttackOpponent, Height * Config.Instance.AttackIconOpponentVerticalPosition / 100);
			Canvas.SetLeft(IconBoardAttackOpponent, Helper.GetScaledXPos(Config.Instance.AttackIconOpponentHorizontalPosition / 100, (int)Width, ScreenRatio));
			Canvas.SetTop(RectGoldDisplay, Height - RectGoldDisplay.ActualHeight);
			Canvas.SetLeft(RectGoldDisplay, Width - RectGoldDisplay.ActualWidth - GoldFrameOffset);
			Canvas.SetTop(GoldProgressGrid, Height - RectGoldDisplay.ActualHeight + (GoldFrameHeight - GoldProgressGrid.ActualHeight) / 2);
			Canvas.SetLeft(GoldProgressGrid, Width - RectGoldDisplay.ActualWidth - GoldFrameOffset - GoldProgressGrid.ActualWidth - 10);
			Canvas.SetTop(GridOpponentBoard, Height / 2 - GridOpponentBoard.ActualHeight - Height * 0.045);
			Canvas.SetTop(GridPlayerBoard, Height / 2 - Height * 0.03);
			if (Config.Instance.ShowFlavorText)
			{
				Canvas.SetTop(GridFlavorText, Height - GridFlavorText.ActualHeight - 10);
				Canvas.SetLeft(GridFlavorText, Width - GridFlavorText.ActualWidth - 10);
			}
			var handCount = _game.Opponent.HandCount > 10 ? 10 : _game.Opponent.HandCount;
			for (int i = 0; i < handCount; i++)
			{
				Canvas.SetLeft(_cardMarks[i], Helper.GetScaledXPos(_cardMarkPos[handCount - 1][i].X, (int)Width, ScreenRatio) - _cardMarks[i].ActualWidth / 2);
				Canvas.SetTop(_cardMarks[i], Math.Max(_cardMarkPos[handCount - 1][i].Y * Height - _cardMarks[i].ActualHeight / 3, 5));
			}
		}

		private double _wotogSize;
		private void UpdateElementSizes()
		{
			OnPropertyChanged(nameof(PlayerStackHeight));
			OnPropertyChanged(nameof(PlayerListHeight));
			OnPropertyChanged(nameof(OpponentStackHeight));
			OnPropertyChanged(nameof(OpponentListHeight));
			//Gold progress
			RectGoldDisplay.Height = GoldFrameHeight;
			RectGoldDisplay.Width = GoldFrameWidth;
			GoldProgressGrid.Height = GoldFrameHeight;
			GPLeftCol.Width = new GridLength(GoldFrameHeight);
			GPRightCol.Width = new GridLength(GoldFrameHeight);
			LblGoldProgress.Margin = new Thickness(GoldFrameHeight * 1.2, 0, GoldFrameHeight * 0.8, 0);
			LblGoldProgress.FontSize = Height * 0.017;
			
			//Scale attack icons, with height
			var atkWidth = (int)Math.Round(Height * 0.0695, 0);
			var atkFont = (int)Math.Round(Height * 0.0204, 0);
			var atkFontMarginTop = (int)Math.Round(Height * 0.0038, 0);
			IconBoardAttackPlayer.Width = atkWidth;
			IconBoardAttackPlayer.Height = atkWidth;
			//TextBlockPlayerAttack.Width = atkWidth;
			//TextBlockPlayerAttack.Height = atkWidth;
			//TextBlockPlayerAttack.FontSize = atkFont;
			IconBoardAttackOpponent.Width = atkWidth;
			IconBoardAttackOpponent.Height = atkWidth;
			TextBlockOpponentAttack.Width = atkWidth;
			TextBlockOpponentAttack.Height = atkWidth;
			TextBlockOpponentAttack.FontSize = atkFont;
			TextBlockPlayerAttack.Margin = new Thickness(0, atkFontMarginTop, 0, 0);
			TextBlockOpponentAttack.Margin = new Thickness(0, atkFontMarginTop, 0, 0);

			var wotogSize = Math.Min(1, Height / 1800);
			if(_wotogSize != wotogSize)
			{
				WotogIconsPlayer.RenderTransform = new ScaleTransform(wotogSize, wotogSize);
				WotogIconsOpponent.RenderTransform = new ScaleTransform(wotogSize, wotogSize);
				_wotogSize = wotogSize;
			}
		}

		public void UpdateStackPanelAlignment()
		{
			OnPropertyChanged(nameof(PlayerStackPanelAlignment));
			OnPropertyChanged(nameof(OpponentStackPanelAlignment));
		}

		public void UpdateCardFrames()
		{
			CanvasOpponentChance.GetBindingExpression(Panel.BackgroundProperty)?.UpdateTarget();
			CanvasOpponentCount.GetBindingExpression(Panel.BackgroundProperty)?.UpdateTarget();
			CanvasPlayerChance.GetBindingExpression(Panel.BackgroundProperty)?.UpdateTarget();
			CanvasPlayerCount.GetBindingExpression(Panel.BackgroundProperty)?.UpdateTarget();
		}

		public double GoldFrameHeight => Height * 25 / 768;
		public double GoldFrameWidth => 6 * GoldFrameHeight;
		public double GoldFrameOffset => 85 / 25 * GoldFrameHeight;

	}
}
