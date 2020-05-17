using System;
using System.Collections.Generic;
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
        private ICollection<CampaignBehaviorBase> CampaignBehaviors;

        protected override void OnGameStart(Game game, IGameStarter gameStarter)
        {
            if (game.GameType is Campaign)
            {
                CampaignGameStarter campaignStarter = (CampaignGameStarter)gameStarter;
                campaignStarter.AddBehavior(new CompleteMainQuestsCampaignBehavior());
                campaignStarter.AddBehavior(new KingdomMakerCampaignBehavior());

                this.CampaignBehaviors = campaignStarter.CampaignBehaviors;
            }
        }

        public override void OnGameInitializationFinished(Game game)
        {
            if (game.GameType is Campaign)
            {
                List<Type> stopBehaviors = new List<Type> {
                    typeof(StoryMode.Behaviors.SecondPhaseCampaignBehavior),
                    typeof(StoryMode.Behaviors.ThirdPhaseCampaignBehavior),
                };
                // TODO automate finding Behaviors
                foreach (CampaignBehaviorBase cb in CampaignBehaviors)
                {
                    if (stopBehaviors.Contains(cb.GetType()))
                    {
                        CampaignEvents.RemoveListeners(cb);
                    }
                }
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
                            q.CompleteQuestWithCancel();

                            attemptToFindSpecialQuests = 0;
                            break;
                        }
                    }
                }

                if (attemptToFindSpecialQuests == maxAttempts1)
                {
                    if (StoryMode.StoryMode.Current != null
                        && StoryMode.StoryMode.Current.MainStoryLine.ThirdPhase != null
                        && StoryMode.StoryMode.Current.MainStoryLine.ThirdPhase.OppositionKingdoms != null)
                    {
                        Kingdom kingdom;
                        while ((kingdom = StoryMode.StoryMode.Current.MainStoryLine.ThirdPhase.OppositionKingdoms.FirstOrDefault()) != null)
                        {
                            StoryMode.StoryMode.Current.MainStoryLine.ThirdPhase.RemoveOppositionKingdom(kingdom);
                        }
                    }

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
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, SettlementEnteredAction);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private void SettlementEnteredAction(MobileParty party, Settlement settlement, Hero hero)
        {
            Clan clan = Clan.PlayerClan;
            if (party != null && settlement != null && Hero.MainHero == party.LeaderHero
                && clan != null && clan == settlement.OwnerClan && settlement.IsFortification)
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
            Game game = Game.Current;
            Type gameType = game.GetType();
            if (gameType == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("ERROR: gameType is null"));
                return;
            }
            MethodInfo getObjectManagerMethod = gameType.GetMethod("get_ObjectManager", new Type[] { });
            if (getObjectManagerMethod == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("ERROR: getObjectManagerMethod is null"));
                return;
            }
            object objectManager = getObjectManagerMethod.Invoke(game, new object[] { });
            if (objectManager == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("ERROR: objectManager is null"));
                return;
            }
            Type objectManagerType = objectManager.GetType();
            if (objectManagerType == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("ERROR: objectManagerType is null"));
                return;
            }

            MethodInfo createObjectMethod = objectManagerType.GetMethods()
                .FirstOrDefault(m =>
                    m.Name == "CreateObject"
                    && m.IsGenericMethod
                    && m.GetGenericArguments().Length == 1
                    && m.GetParameters().Length == 1
                    && m.GetParameters()[0].ParameterType == typeof(string));
            if (createObjectMethod == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("ERROR: createObjectMethod is null"));
                return;
            }

            MethodInfo genericCreateObjectMethod = createObjectMethod.MakeGenericMethod(typeof(Kingdom));

            Kingdom kingdom = genericCreateObjectMethod.Invoke(objectManager, new object[1] { "dynamic_kingdom_" + CampaignTime.Now.ElapsedSecondsUntilNow }) as Kingdom;
            if (kingdom == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("ERROR: Object kingdom is null"));
                return;
            }

            TextObject informalName = new TextObject("{CLAN_NAME}", null);
            SetTextVariable(informalName, "CLAN_NAME", Clan.PlayerClan.Name);
            TextObject kingdomName = new TextObject("Kingdom of the {CLAN_NAME}", null);
            SetTextVariable(kingdomName, "CLAN_NAME", Clan.PlayerClan.Name);
            kingdom.InitializeKingdom(kingdomName, informalName, Clan.PlayerClan.Culture, Clan.PlayerClan.Banner, Clan.PlayerClan.Color, Clan.PlayerClan.Color2, Clan.PlayerClan.InitialPosition);

            foreach (Kingdom k in Kingdom.All)
            {
                if (k != null && k != kingdom && Clan.PlayerClan.IsAtWarWith(k))
                {
                    DeclareWarAction.Apply(k, kingdom);
                }
            }

            ChangeKingdomAction.ApplyByJoinToKingdom(Clan.PlayerClan, kingdom, true);
            kingdom.RulingClan = Clan.PlayerClan;
        }

        private void SetTextVariable(TextObject text, string tag, TextObject variable)
        {
            SetTextVariable_internal<TextObject>(text, tag, variable);
        }

        private void SetTextVariable_internal<T>(TextObject text, string tag, object variable)
        {
            Type textType = text.GetType();
            if (textType == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("ERROR: textType is null"));
                return;
            }
            MethodInfo setTextVariableMethod = textType.GetMethod("SetTextVariable", new Type[] { typeof(string), typeof(T) });
            if (setTextVariableMethod == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("ERROR: setTextVariableMethod is null"));
                return;
            }
            setTextVariableMethod.Invoke(text, new object[] { tag, variable });
        }
    }
}
