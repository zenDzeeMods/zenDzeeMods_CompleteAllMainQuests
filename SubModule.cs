using Helpers;
using System;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace zenDzeeMods_CompleteAllMainQuests
{
    public class SubModule : MBSubModuleBase
    {
        protected override void OnGameStart(Game game, IGameStarter gameStarter)
        {
            if (game.GameType is Campaign)
            {
                CampaignGameStarter campaignStarter = gameStarter as CampaignGameStarter;
                if (campaignStarter == null)
                {
                    InformationManager.DisplayMessage(new InformationMessage("gameStarter is null"));
                    return;
                }
                campaignStarter.AddBehavior(new CompleteMainQuestsCampaignBehavior());
                campaignStarter.AddBehavior(new KingdomMakerCampaignBehavior());
            }
        }
    }

    internal class CompleteMainQuestsCampaignBehavior : CampaignBehaviorBase
    {
        private int hourlyCounter = 0;
        /* Should be greater than 1 */
        private const int maxAttempts1 = 3;

        bool pending_clean_quests = false;
        bool pending_clean_brother = false;

        Hero brother = null;

        public CompleteMainQuestsCampaignBehavior() : base() {
            Init();
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        public override void RegisterEvents()
        {
            //CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, this.Init);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, this.HourlyTick);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, this.DailyTick);
        }

        private void Init()
        {
            brother = StoryMode.StoryMode.Current.MainStoryLine.Brother;
            if (brother == null)
            {
                brother = Hero.MainHero.Siblings.First();
            }
            if (brother == null || brother.IsDead)
            {
                pending_clean_brother = true;
                Cleanup();
            }
            
            if (brother != null && brother.IsAlive)
            {
                SetDialogs();
            }

            InformationManager.DisplayMessage(new InformationMessage("Mod CompleteAllMainQuests is ready to start"));
        }

        private void Cleanup()
        {
            if (pending_clean_quests &&
                pending_clean_brother)
            {
                CampaignEvents.RemoveListeners(this);
            }
        }

        private void HourlyTick()
        {
            if (hourlyCounter < maxAttempts1 &&
                (StoryMode.StoryMode.Current.MainStoryLine.TutorialPhase.IsCompleted || StoryMode.StoryMode.Current.MainStoryLine.TutorialPhase.IsSkipped))
            {
                ++hourlyCounter;

                var qm = Campaign.Current.QuestManager;
                if (qm != null)
                {
                    var ql = qm.Quests;
                    foreach (QuestBase q in ql)
                    {
                        if (q != null && q.IsSpecialQuest)
                        {
                            InformationManager.DisplayMessage(new InformationMessage("Cancelling " + q.ToString()));
                            q.CompleteQuestWithCancel();

                            hourlyCounter = 0;
                            break;
                        }
                    }
                }

                if (hourlyCounter == maxAttempts1)
                {
                    CheckBrother();

                    pending_clean_quests = true;
                    Cleanup();
                }
            }
        }

        private void DailyTick()
        {
            CheckBrother();
        }

        private void CheckBrother()
        {
            if (brother == null || brother.IsDead)
            {
                pending_clean_brother = true;
                Cleanup();
                return;
            }

            if (Clan.PlayerClan != null &&
                (StoryMode.StoryMode.Current.MainStoryLine.TutorialPhase.IsCompleted || StoryMode.StoryMode.Current.MainStoryLine.TutorialPhase.IsSkipped))
            {
                if (brother.IsDisabled && Clan.PlayerClan.Settlements.Count(s => s.IsVillage) > 0)
                {
                    Settlement settlement = Clan.PlayerClan.Settlements.First(s => s.IsVillage);
                    brother.ChangeState(Hero.CharacterStates.Active);
                    brother.IsNoble = false;
                    EnterSettlementAction.ApplyForCharacterOnly(brother, settlement);

                    TextObject msg = new TextObject("Rumor says that your brother appeared in {SETTLEMENT}");
                    msg.SetTextVariable("SETTLEMENT", settlement.EncyclopediaLinkWithName);

                    InformationManager.AddQuickInformation(msg, 0, null, "event:/ui/notification/quest_start");
                    InformationManager.DisplayMessage(new InformationMessage(msg.ToString()));

                    Campaign.Current.VisualTrackerManager.RegisterObject(settlement);

                    pending_clean_brother = true;
                    Cleanup();
                }
            }
        }

        private void SetDialogs()
        {
            DialogFlow dialog = DialogFlow.CreateDialogFlow(QuestManager.HeroMainOptionsToken, 150)
                .BeginPlayerOptions()
                    .PlayerOption(new TextObject("I think we should keep together, we are family after all."), null)
                        .Condition(() => brother != null && !brother.IsNoble && Hero.OneToOneConversationHero == brother)
                        .NpcLine(new TextObject("Agree."))
                        .Consequence(ConsequenceReturnBrother)
                    .PlayerOption(new TextObject("Do you want to be my vassal?"), null)
                        .Condition(() => 
                            (
                                (brother != null && brother.IsNoble && Hero.OneToOneConversationHero == brother && brother.Clan == Clan.PlayerClan)
                                || (Hero.OneToOneConversationHero != null && Hero.OneToOneConversationHero.IsPlayerCompanion)
                            )
                            && Clan.PlayerClan.Kingdom != null && Clan.PlayerClan.Settlements.Any(s => s.IsFortification))
                        .NpcLine(new TextObject("Sure."))
                        .Consequence(() => ConsequenceMakeLord(Hero.OneToOneConversationHero))
                .EndPlayerOptions()
                .CloseDialog();

            Campaign.Current.ConversationManager.AddDialogFlow(dialog);
        }

        private void ConsequenceReturnBrother()
        {
            brother.IsNoble = true;
            brother.Clan = Clan.PlayerClan;
            AddHeroToPartyAction.Apply(brother, MobileParty.MainParty, true);
        }

        private void ConsequenceMakeLord(Hero hero)
        {
            Clan newClan = zenDzeeObjectManager.CreateObject<Clan>("players_lord_faction_" + hero.StringId);
            if (newClan == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("ERROR: Object newClan is null"));
                return;
            }

            TextObject text = new TextObject("{HERO.NAME}");
            StringHelpers.SetCharacterProperties("HERO", hero.CharacterObject, null, text);

            newClan.InitializeClan(text, text, Clan.PlayerClan.Culture, Banner.CreateRandomClanBanner());

            bool isPlayerParty = true;
            MobilePartyHelper.CreateNewClanMobileParty(hero, hero.Clan, out isPlayerParty);

            hero.IsNoble = true;
            hero.Clan = newClan;
            newClan.SetLeader(hero);

            ChangeKingdomAction.ApplyByJoinToKingdom(newClan, Clan.PlayerClan.Kingdom, true);

            Clan.PlayerClan.Influence += 500;
            newClan.Influence += 100;
        }
    }

    internal class KingdomMakerCampaignBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, SettlementOwnerChangedAction);
            CampaignEvents.OnSettlementLeftEvent.AddNonSerializedListener(this, SettlementEnteredAction);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private void SettlementEnteredAction(MobileParty party, Settlement settlement)
        {
            Clan clan = Clan.PlayerClan;
            if (Hero.MainHero == party.LeaderHero && clan != null && clan == settlement.OwnerClan && settlement.IsFortification)
            {
                if ((clan.Kingdom == null) &&
                    (StoryMode.StoryMode.Current.MainStoryLine.TutorialPhase.IsCompleted || StoryMode.StoryMode.Current.MainStoryLine.TutorialPhase.IsSkipped))
                {
                    MakeKingdom();
                }
            }
        }

        private void SettlementOwnerChangedAction(Settlement settlement, bool arg2, Hero arg3, Hero arg4, Hero arg5, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail arg6)
        {
            Clan clan = Clan.PlayerClan;
            if (clan != null && clan == settlement.OwnerClan && settlement.IsFortification)
            {
                if ((clan.Kingdom == null) &&
                    (StoryMode.StoryMode.Current.MainStoryLine.TutorialPhase.IsCompleted || StoryMode.StoryMode.Current.MainStoryLine.TutorialPhase.IsSkipped))
                {
                    MakeKingdom();
                }
            }
        }

        private void MakeKingdom()
        {
            Kingdom kingdom = zenDzeeObjectManager.CreateObject<Kingdom>("playerland_kingdom");
            if (kingdom == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("ERROR: Object kingdom is null"));
                return;
            }

            TextObject informalName = new TextObject("{CLAN_NAME}", null);
            informalName.SetTextVariable("CLAN_NAME", Clan.PlayerClan.Name);
            TextObject kingdomName = new TextObject("Kingdom of the {CLAN_NAME}", null);
            kingdomName.SetTextVariable("CLAN_NAME", Clan.PlayerClan.Name);
            kingdom.InitializeKingdom(kingdomName, informalName, Clan.PlayerClan.Culture, Clan.PlayerClan.Banner, Clan.PlayerClan.Color, Clan.PlayerClan.Color2, Clan.PlayerClan.InitialPosition);

            foreach(Kingdom k in Kingdom.All)
            {
                if (k != null && k != kingdom && Clan.PlayerClan.IsAtWarWith(k))
                {
                    DeclareWarAction.Apply(k, kingdom);
                }
            }

            ChangeKingdomAction.ApplyByJoinToKingdom(Clan.PlayerClan, kingdom, true);
            kingdom.RulingClan = Clan.PlayerClan;
        }
    }

    /**
     * MBObjectManager wrapper
     */
    internal class zenDzeeObjectManager
    {
        public static object GetManager()
        {
            Game game = Game.Current;
            Type gameType = game.GetType();
            if (gameType == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("ERROR: gameType is null"));
                return null;
            }
            MethodInfo getObjectManagerMethod = gameType.GetMethod("get_ObjectManager", new Type[] { });
            if (getObjectManagerMethod == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("ERROR: getObjectManagerMethod is null"));
                return null;
            }

            return getObjectManagerMethod.Invoke(game, new object[] { });
        }

        public static T CreateObject<T>(string stringId) where T : class
        {
            object objectManager = GetManager();
            if (objectManager == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("ERROR: objectManager is null"));
                return null;
            }
            Type objectManagerType = objectManager.GetType();
            if (objectManagerType == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("ERROR: objectManagerType is null"));
                return null;
            }
            MethodInfo createObjectMethod = objectManagerType.GetMethod("CreateObject", new Type[2] { typeof(Type), typeof(string) });
            if (createObjectMethod == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("ERROR: createObjectMethod is null"));
                return null;
            }

            return createObjectMethod.Invoke(objectManager, new object[2] { typeof(T), stringId }) as T;
        }
    }
}