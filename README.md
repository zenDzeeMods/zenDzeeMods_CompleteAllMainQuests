# zenDzeeMods_CompleteAllMainQuests
The mod automatically completes all main story quests and allows you to create your own kingdom. 

## How to build sources
Open zenDzeeMods_CompleteAllMainQuests.csproj in the notepad and edit property `<MNB2DIR>` according to location of your game.
Now you can open zenDzeeMods_CompleteAllMainQuests.sln in VisualStudio and build it.

## Mod features:
1. Gracefully allows you to complete a tutorial or skip it. After that you can create your clan.
2. Automatically completes all the main story quests by cancelling them. There a lot of quests, so it may take some time, about 1 in-game day.
3. Adds the ability to create your own kingdom or join to any existing kingdoms.
4. Can be installed and uninstalled in the mid game.
5. Uses only the basic functionality of the game framework, which makes the mod compatible with almost everything.

## Game version compatibility:
Beta and stable versions of the game. Future versions should be also compatible.

## Mods compatibility:
Theoretically, all mods should be compatible, except mods with the similar functionality.

## How to create your own kingdom:
Preconditions: you should not be a vassal of a kingdom.
1. Become the owner of any fortified settlement (town or keep). It doesn't matter how you got the settlement, captured or traded.
2. Save and Load. (This step is very important. See known issues for explanation)
3. Done.

If you already had a settlement before installing the mod, simply enter and leave the settlement, and this will trigger an event to create a kingdom.

## Known Issues:
CampaignSystem of the game registers new kingdoms only when you start or load the game. Without registration, you cannot gain influence.
