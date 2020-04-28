using System;
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
                CampaignGameStarter campaignStarter = (CampaignGameStarter)gameStarter;
                campaignStarter.AddBehavior(new CompleteMainQuestsCampaignBehavior());
                campaignStarter.AddBehavior(new KingdomMakerCampaignBehavior());
            }
        }
    }

    internal class CompleteMainQuestsCampaignBehavior : CampaignBehaviorBase
    {
        private int attemptToFindSpecialQuests = 0;
        /* Should be greater than 1 */
        private const int maxAttempts1 = 3;

        public override void RegisterEvents()
        {
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, CompleteMainQuestAction);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private void CompleteMainQuestAction()
        {
            if (attemptToFindSpecialQuests < maxAttempts1 &&
                (StoryMode.StoryMode.Current.MainStoryLine.TutorialPhase.IsCompleted || StoryMode.StoryMode.Current.MainStoryLine.TutorialPhase.IsSkipped))
            {
                ++attemptToFindSpecialQuests;

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

                            attemptToFindSpecialQuests = 0;
                            break;
                        }
                    }
                }

                if (attemptToFindSpecialQuests == maxAttempts1)
                {
                    CampaignEvents.RemoveListeners(this);
                }
            }
        }
    }

    internal class KingdomMakerCampaignBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, SettlementOwnerChangedAction);
            CampaignEvents.OnSettlementLeftEvent.AddNonSerializedListener(this, SettlementLeftAction);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private void SettlementLeftAction(MobileParty party, Settlement settlement)
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
            object objectManager = Game.Current.ObjectManager;
            if (objectManager == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("ERROR: MBObjectManager is null"));
                return;
            }
            Type objectManagerType = objectManager.GetType();
            if (objectManagerType == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("ERROR: objectManagerType is null"));
                return;
            }
            MethodInfo createObjectMethod = objectManagerType.GetMethod("CreateObject", new Type[2] { typeof(Type), typeof(string) });
            if (createObjectMethod == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("ERROR: createObjectMethod is null"));
                return;
            }
            Kingdom kingdom = (Kingdom)createObjectMethod.Invoke(objectManager, new object[2] { typeof(Kingdom), "playerland_kingdom" });
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
}