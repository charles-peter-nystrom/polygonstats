﻿using System;
using System.Text;
using System.Net.Sockets;
using NetCoreServer;
using System.Text.Json;
using POGOProtos.Rpc;
using Google.Protobuf.Collections;
using static System.Linq.Queryable;
using static System.Linq.Enumerable;
using PolygonStats.Models;
using PolygonStats.Configuration;
using System.Collections.Generic;
using Serilog;
using Microsoft.EntityFrameworkCore;
using PolygonStats.RawWebhook;
using System.Globalization;
using System.Threading;
using PolyConfig = PolygonStats.Configuration.ConfigurationManager;

namespace PolygonStats
{
    class ClientSession : TcpSession
    {
        private StringBuilder messageBuffer = new StringBuilder();
        private string accountName = null;
        private MySQLConnectionManager connectionManager = new MySQLConnectionManager();
        private int dbSessionId = -1;
        private int accountId;

        private int messageCount = 0;
        private ILogger logger;

        private DateTime lastMessageDateTime = DateTime.UtcNow;
        private WildPokemonProto lastEncounterPokemon = null;
        private Dictionary<ulong, DateTime> holoPokemon = new Dictionary<ulong, DateTime>();

        public ClientSession(TcpServer server) : base(server)
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");
            if (ConfigurationManager.Shared.Config.Debug.ToFiles)
            {
                LoggerConfiguration configuration = new LoggerConfiguration()
                    .WriteTo.File($"logs/sessions/{Id}.log", rollingInterval: RollingInterval.Day);
                configuration = ConfigurationManager.Shared.Config.Debug.Debug
                    ? configuration.MinimumLevel.Debug()
                    : configuration.MinimumLevel.Information();
                logger = configuration.CreateLogger();
            } else
            {
                logger = Log.Logger;
            }
        }

        public bool isConnected() => (DateTime.UtcNow - lastMessageDateTime).TotalMinutes <= 20;

        protected override void OnConnected()
        {
            this.Socket.ReceiveBufferSize = 8192 * 4;
            this.Socket.ReceiveTimeout = 10000;
        }

        protected override void OnDisconnected()
        {
            Log.Information($"User {this.accountName} with sessionId {Id} has disconnected.");

            // Add ent time to session
            if (ConfigurationManager.Shared.Config.MySql.Enabled)
            {
                if(dbSessionId != -1)
                {
                    using var context = connectionManager.GetContext(); Session dbSession = connectionManager.GetSession(context, dbSessionId);
                    dbSession.EndTime = lastMessageDateTime;
                    context.SaveChanges();
                }
            }

        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            lastMessageDateTime = DateTime.UtcNow;
            string currentMessage = Encoding.UTF8.GetString(buffer, (int)offset, (int)size);

            if (ConfigurationManager.Shared.Config.Debug.DebugMessages)
            {
                logger.Debug($"Message #{++messageCount} was received!");
            }

            messageBuffer.Append(currentMessage);
            var jsonStrings = messageBuffer.ToString().Split("\n", StringSplitOptions.RemoveEmptyEntries);
            messageBuffer.Clear();
            if (ConfigurationManager.Shared.Config.Debug.DebugMessages)
            {
                logger.Debug($"Message was splitted into {jsonStrings.Length} jsonObjects.");
            }
            for(int index = 0; index < jsonStrings.Length; index++)
            {
                string jsonString = jsonStrings[index];
                string trimedJsonString = jsonString.Trim('\r', '\n');
                if(!trimedJsonString.StartsWith("{"))
                {
                    if (ConfigurationManager.Shared.Config.Debug.DebugMessages)
                    {
                        logger.Debug("Json string didnt start with a {.");
                    }
                    continue;
                }
                if(!trimedJsonString.EndsWith("}"))
                {
                    if (ConfigurationManager.Shared.Config.Debug.DebugMessages)
                    {
                        logger.Debug("Json string didnt end with a }.");
                    }
                    if(index == jsonStrings.Length - 1){
                        messageBuffer.Append(jsonString);
                    }
                    continue;
                }
                try
                {
                    MessageObject message = JsonSerializer.Deserialize<MessageObject>(trimedJsonString);

                    if (ConfigurationManager.Shared.Config.Debug.DebugMessages)
                    {
                        logger.Debug($"Handle JsonObject #{index} with {message.payloads.Count} payloads.");
                    }
                    foreach (Payload payload in message.payloads)
                    {
                        if(payload.account_name == null || payload.account_name.Equals("null"))
                        {
                            continue;
                        }
                        AddAccountAndSessionIfNeeded(payload);
                        HandlePayload(payload);
                        if (ConfigurationManager.Shared.Config.RawData.Enabled) {
                            RawWebhookManager.shared.AddRawData(new RawDataMessage()
                            {
                                origin = payload.account_name,
                                rawData = new RawData()
                                {
                                    type = payload.type,
                                    lat = payload.lat,
                                    lng = payload.lng,
                                    timestamp = payload.timestamp,
                                    raw = true,
                                    payload = payload.proto
                                }
                            });
                        }
                    }
                }
                catch (JsonException)
                {
                    if(index == jsonStrings.Length - 1){
                        messageBuffer.Append(jsonString);
                    }
                }
            }

            if (ConfigurationManager.Shared.Config.Debug.DebugMessages)
            {
                logger.Debug($"Message #{messageCount} was handled!");
            }
        }

        private void AddAccountAndSessionIfNeeded(Payload payload) {
            if (this.accountName != payload.account_name)
            {
                this.accountName = payload.account_name;
                GetStatEntry();

                if (ConfigurationManager.Shared.Config.MySql.Enabled)
                {
                    using(var context = connectionManager.GetContext()) {
                        Account account = context.Accounts.Where(a => a.Name == this.accountName).FirstOrDefault<Account>();
                        if (account == null)
                        {
                            account = new Account();
                            account.Name = this.accountName;
                            account.HashedName = "";
                            context.Accounts.Add(account);
                        }
                        Log.Information($"User {this.accountName} with sessionId {Id} has connected.");
                        Session dbSession = new Session { StartTime = DateTime.UtcNow, LogEntrys = new List<LogEntry>() };
                        account.Sessions.Add(dbSession);
                        context.SaveChanges();

                        dbSessionId = dbSession.Id;
                        accountId = account.Id;
                    }
                }
            }
        }

        private Stats GetStatEntry() => ConfigurationManager.Shared.Config.Http.Enabled ? StatManager.sharedInstance.getEntry(accountName) : null;

        private void HandlePayload(Payload payload)
        {
            logger.Debug($"Payload with type {payload.getMethodType().ToString("g")}");
            switch (payload.getMethodType())
            {
                case Method.CheckAwardedBadges:
                    CheckAwardedBadgesOutProto badge = CheckAwardedBadgesOutProto.Parser.ParseFrom(payload.getDate());
                    logger.Debug($"Proto: {JsonSerializer.Serialize(badge)}");
                    break;
                case Method.Encounter:
                    EncounterOutProto encounterProto = EncounterOutProto.Parser.ParseFrom(payload.getDate());
                    logger.Debug($"Proto: {JsonSerializer.Serialize(encounterProto)}");
                    ProcessEncounter(payload.account_name, encounterProto, payload);
                    break;
                case Method.CatchPokemon:
                    CatchPokemonOutProto catchPokemonProto = CatchPokemonOutProto.Parser.ParseFrom(payload.getDate());
                    logger.Debug($"Proto: {JsonSerializer.Serialize(catchPokemonProto)}");
                    ProcessCaughtPokemon(catchPokemonProto);
                    break;
                case Method.GymFeedPokemon:
                    GymFeedPokemonOutProto feedPokemonProto = GymFeedPokemonOutProto.Parser.ParseFrom(payload.getDate());
                    if (feedPokemonProto.Result == GymFeedPokemonOutProto.Types.Result.Success)
                    {
                        ProcessFeedBerry(payload.account_name, feedPokemonProto);
                    }
                    break;
                case Method.CompleteQuest:
                    CompleteQuestOutProto questProto = CompleteQuestOutProto.Parser.ParseFrom(payload.getDate());
                    if (questProto.Status == CompleteQuestOutProto.Types.Status.Success)
                    {
                        ProcessQuestRewards(payload.account_name, questProto.Quest.Quest.QuestRewards);
                    }
                    break;
                case Method.CompleteQuestStampCard:
                    CompleteQuestStampCardOutProto completeQuestStampCardProto = CompleteQuestStampCardOutProto.Parser.ParseFrom(payload.getDate());
                    if (completeQuestStampCardProto.Status == CompleteQuestStampCardOutProto.Types.Status.Success)
                    {
                        ProcessQuestRewards(payload.account_name, completeQuestStampCardProto.Reward);
                    }
                    break;
                case Method.GetHatchedEggs:
                    GetHatchedEggsOutProto getHatchedEggsProto = GetHatchedEggsOutProto.Parser.ParseFrom(payload.getDate());
                    if (getHatchedEggsProto.Success)
                    {
                        ProcessHatchedEggReward(payload.account_name, getHatchedEggsProto);
                    }
                    break;
                case Method.GetMapObjects:
                    GetMapObjectsOutProto mapProto = GetMapObjectsOutProto.Parser.ParseFrom(payload.getDate());
                    if (mapProto.Status == GetMapObjectsOutProto.Types.Status.Success)
                    {
                        if (ConfigurationManager.Shared.Config.RocketMap.enabled)
                        {
                            RocketMap.RocketMapManager.shared.AddCells(mapProto.MapCell.ToList());
                            RocketMap.RocketMapManager.shared.AddWeather(mapProto.ClientWeather.ToList(), (int) mapProto.TimeOfDay);
                            RocketMap.RocketMapManager.shared.AddSpawnpoints(mapProto);
                            foreach (var mapCell in mapProto.MapCell)
                            {
                                RocketMap.RocketMapManager.shared.AddForts(mapCell.Fort.ToList());
                            }
                        }
                    }
                    break;
                case Method.FortDetails:
                    FortDetailsOutProto fortDetailProto = FortDetailsOutProto.Parser.ParseFrom(payload.getDate());
                    if (ConfigurationManager.Shared.Config.RocketMap.enabled)
                    {
                        RocketMap.RocketMapManager.shared.UpdateFortInformations(fortDetailProto);
                    }
                    break;
                case Method.GymGetInfo:
                    GymGetInfoOutProto gymProto = GymGetInfoOutProto.Parser.ParseFrom(payload.getDate());
                    if (gymProto.Result == GymGetInfoOutProto.Types.Result.Success)
                    {
                        if (ConfigurationManager.Shared.Config.RocketMap.enabled)
                        {
                            RocketMap.RocketMapManager.shared.UpdateGymDetails(gymProto);
                        }
                    }
                    break;
                case Method.FortSearch:
                    FortSearchOutProto fortSearchProto = FortSearchOutProto.Parser.ParseFrom(payload.getDate());
                    if (fortSearchProto.Result == FortSearchOutProto.Types.Result.Success)
                    {
                        ProcessSpinnedFort(payload.account_name, fortSearchProto);
                        if (ConfigurationManager.Shared.Config.RocketMap.enabled)
                        {
                            RocketMap.RocketMapManager.shared.AddQuest(fortSearchProto);
                        }
                    }
                    break;
                case Method.EvolvePokemon:
                    EvolvePokemonOutProto evolvePokemon = EvolvePokemonOutProto.Parser.ParseFrom(payload.getDate());
                    if(evolvePokemon.Result == EvolvePokemonOutProto.Types.Result.Success)
                    {
                        ProcessEvolvedPokemon(payload.account_name, evolvePokemon);
                    }
                    break;
                case Method.GetHoloholoInventory:
                    GetHoloholoInventoryOutProto holoInventory = GetHoloholoInventoryOutProto.Parser.ParseFrom(payload.getDate());
                    logger.Debug($"Proto: {JsonSerializer.Serialize(holoInventory)}");
                    ProcessHoloHoloInventory(payload.account_name, holoInventory);
                    break;
                case Method.InvasionBattleUpdate:
                    UpdateInvasionBattleOutProto updateBattle = UpdateInvasionBattleOutProto.Parser.ParseFrom(payload.getDate());
                    ProcessUpdateInvasionBattle(payload.account_name, updateBattle);
                    break;
                case Method.InvasionEncounter:
                    InvasionEncounterOutProto invasionEncounter = InvasionEncounterOutProto.Parser.ParseFrom(payload.getDate());
                    if (invasionEncounter.EncounterPokemon != null) {
                        this.lastEncounterPokemon = new WildPokemonProto()
                        {
                            Pokemon = invasionEncounter.EncounterPokemon
                        };
                    }
                    break;
                case Method.AttackRaid:
                    AttackRaidBattleOutProto attackRaidBattle = AttackRaidBattleOutProto.Parser.ParseFrom(payload.getDate());
                    ProcessAttackRaidBattle(payload.account_name, attackRaidBattle);
                    break;
                case Method.GetPlayer:
                    GetPlayerOutProto player = GetPlayerOutProto.Parser.ParseFrom(payload.getDate());
                    ProcessPlayer(payload.account_name, player, int.Parse(payload.level));
                    break;
                default:
                    break;
            }
        }

        private void ProcessEncounter(string account_name, EncounterOutProto encounterProto, Payload payload)
        {
            if (encounterProto.Pokemon == null || encounterProto.Pokemon.Pokemon == null)
            {
                return;
            }

            if (ConfigurationManager.Shared.Config.RocketMap.enabled)
            {
                RocketMap.RocketMapManager.shared.AddEncounter(encounterProto, payload);
            }

            if (!ConfigurationManager.Shared.Config.Encounter.Enabled) {
                return;
            }
            lastEncounterPokemon = encounterProto.Pokemon;
            EncounterManager.shared.AddEncounter(encounterProto);
        }

        private void ProcessAttackRaidBattle(string account_name, AttackRaidBattleOutProto attackRaidBattle)
        {
            if (attackRaidBattle.Result != AttackRaidBattleOutProto.Types.Result.Success)
            {
                return;
            }
            if (attackRaidBattle.BattleUpdate == null
                || attackRaidBattle.BattleUpdate.BattleLog == null
                || attackRaidBattle.BattleUpdate.BattleLog.BattleActions == null
                || attackRaidBattle.BattleUpdate.BattleLog.BattleActions.Count == 0)
            {
                return;
            }

            BattleActionProto lastEntry = attackRaidBattle.BattleUpdate.BattleLog.BattleActions[attackRaidBattle.BattleUpdate.BattleLog.BattleActions.Count - 1];
            if (lastEntry.BattleResults == null)
            {
                return;
            }

            // Get user
            BattleParticipantProto ownParticipant = lastEntry.BattleResults.Attackers.FirstOrDefault(attacker => attacker.TrainerPublicProfile.Name == account_name);
            if (ownParticipant != null)
            {
                int index = lastEntry.BattleResults.Attackers.IndexOf(ownParticipant);

                if (lastEntry.BattleResults.PostRaidEncounter != null && lastEntry.BattleResults.PostRaidEncounter.Count > 0)
                {
                    lastEncounterPokemon = new WildPokemonProto() {
                        Pokemon = lastEntry.BattleResults.PostRaidEncounter.First().Pokemon
                    };
                }

                if (ConfigurationManager.Shared.Config.Http.Enabled)
                {
                    Stats entry = GetStatEntry();
                    entry.AddXp(lastEntry.BattleResults.PlayerXpAwarded[index]);
                    int stardust = 0;
                    stardust += lastEntry.BattleResults.RaidItemRewards[index].LootItem.Sum(loot => loot.Stardust ? loot.Count : 0);
                    stardust += lastEntry.BattleResults.DefaultRaidItemRewards[index].LootItem.Sum(loot => loot.Stardust ? loot.Count : 0);
                    entry.AddStardust(stardust);
                }

                if (ConfigurationManager.Shared.Config.MySql.Enabled)
                {
                    int stardust = 0;
                    if (lastEntry.BattleResults.RaidItemRewards.Count > index)
                    {
                        stardust += lastEntry.BattleResults.RaidItemRewards[index].LootItem.Sum(loot => loot.Stardust ? loot.Count : 0);
                    }
                    if (lastEntry.BattleResults.DefaultRaidItemRewards.Count > index)
                    {
                        stardust += lastEntry.BattleResults.DefaultRaidItemRewards[index].LootItem.Sum(loot => loot.Stardust ? loot.Count : 0);
                    }
                    int xp = 0;
                    if(lastEntry.BattleResults.PlayerXpAwarded.Count > index)
                    {
                        xp = lastEntry.BattleResults.PlayerXpAwarded[index];
                    }

                    connectionManager.AddRaidToDatabase(dbSessionId, xp, stardust);
                }
            }
        }

        private void ProcessUpdateInvasionBattle(string account_name, UpdateInvasionBattleOutProto updateBattle)
        {
            if (updateBattle.Status != InvasionStatus.Types.Status.Success || updateBattle.Rewards == null)
            {
                return;
            }

            if (ConfigurationManager.Shared.Config.Http.Enabled)
            {
                Stats entry = GetStatEntry();
                foreach (LootItemProto loot in updateBattle.Rewards.LootItem)
                {
                    switch (loot.TypeCase)
                    {
                        case LootItemProto.TypeOneofCase.Experience:
                            entry.AddXp(loot.Count);
                            break;
                        case LootItemProto.TypeOneofCase.Stardust:
                            entry.AddStardust(loot.Count);
                            break;
                        default:
                            break;
                    }
                }
            }

            if (ConfigurationManager.Shared.Config.MySql.Enabled)
            {
                connectionManager.AddRocketToDatabase(dbSessionId, updateBattle);
            }
        }

        private void ProcessHoloHoloInventory(string account_name, GetHoloholoInventoryOutProto holoInventory)
        {
            if (!ConfigurationManager.Shared.Config.MySql.Enabled)
            {
                return;
            }
            if (holoInventory.InventoryDelta == null || holoInventory.InventoryDelta.InventoryItem == null)
            {
                return;
            }

            foreach (InventoryItemProto item in holoInventory.InventoryDelta.InventoryItem)
            {
                if (item.InventoryItemData != null)
                { 
                    if (item.InventoryItemData.Pokemon != null)
                    {
                        PokemonProto pokemon = item.InventoryItemData.Pokemon;

                        using var context = connectionManager.GetContext();
                        int effected = context.Database.ExecuteSqlRaw($"UPDATE `SessionLogEntry` SET PokemonName=\"{pokemon.PokemonId.ToString("G")}\", Attack={pokemon.IndividualAttack}, Defense={pokemon.IndividualDefense}, Stamina={pokemon.IndividualStamina} WHERE PokemonUniqueId={pokemon.Id} AND `timestamp` BETWEEN (DATE_SUB(UTC_TIMESTAMP(),INTERVAL 3 MINUTE)) AND (DATE_ADD(UTC_TIMESTAMP(),INTERVAL 2 MINUTE)) ORDER BY Id");
                        if (effected > 0 && pokemon.IndividualAttack == 15 && pokemon.IndividualDefense == 15 && pokemon.IndividualStamina == 15)
                        {
                            if (!holoPokemon.ContainsKey(pokemon.Id))
                            {
                                holoPokemon.Add(pokemon.Id, DateTime.Now);
                                context.Database.ExecuteSqlRaw($"UPDATE `Session` SET MaxIV=MaxIV+1, LastUpdate=UTC_TIMESTAMP() WHERE Id={dbSessionId} ORDER BY Id");
                            }
                            else
                            {
                                foreach (ulong id in holoPokemon.Keys.ToList())
                                {
                                    if ((DateTime.Now - holoPokemon[id]).TotalMinutes > 10)
                                    {
                                        holoPokemon.Remove(id);
                                    }
                                }
                            }
                        }
                    }
                    if (item.InventoryItemData.PlayerStats != null) {
                        connectionManager.UpdateLevelAndExp(accountId, item.InventoryItemData.PlayerStats);
                    }
                }
            }
        }

        private void ProcessEvolvedPokemon(string account_name, EvolvePokemonOutProto evolvePokemon)
        {
            if (ConfigurationManager.Shared.Config.Http.Enabled)
            {
                Stats entry = GetStatEntry();
                entry.AddXp(evolvePokemon.ExpAwarded);
            }

            if (ConfigurationManager.Shared.Config.MySql.Enabled)
            {
                connectionManager.AddEvolvePokemonToDatabase(dbSessionId, evolvePokemon);
            }
        }

        private void ProcessFeedBerry(string account_name, GymFeedPokemonOutProto feedPokemonProto)
        {
            if (ConfigurationManager.Shared.Config.Http.Enabled)
            {
                Stats entry = GetStatEntry();
                entry.AddXp(feedPokemonProto.XpAwarded);
                entry.AddStardust(feedPokemonProto.StardustAwarded);
            }

            if (ConfigurationManager.Shared.Config.MySql.Enabled)
            {
                connectionManager.AddFeedBerryToDatabase(dbSessionId, feedPokemonProto);
            }
        }

        private void ProcessSpinnedFort(string account_name, FortSearchOutProto fortSearchProto)
        {
            if (ConfigurationManager.Shared.Config.Http.Enabled)
            {
                Stats entry = GetStatEntry();
                entry.AddSpinnedPokestop();
                entry.AddXp(fortSearchProto.XpAwarded);
            }

            if (ConfigurationManager.Shared.Config.MySql.Enabled)
            {
                connectionManager.AddSpinnedFortToDatabase(dbSessionId, fortSearchProto);
            }
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Chat TCP session caught an error with code {error}");
        }

        private void ProcessQuestRewards(string acc, RepeatedField<QuestRewardProto> rewards)
        {
            if (ConfigurationManager.Shared.Config.Http.Enabled)
            {
                Stats entry = GetStatEntry();
                foreach (QuestRewardProto reward in rewards)
                {
                    if (reward.RewardCase == QuestRewardProto.RewardOneofCase.Exp)
                    {
                        entry.AddXp(reward.Exp);
                    }
                    if (reward.RewardCase == QuestRewardProto.RewardOneofCase.Stardust)
                    {
                        entry.AddStardust(reward.Stardust);
                    }
                }
            }

            if (ConfigurationManager.Shared.Config.MySql.Enabled)
            {
                connectionManager.AddQuestToDatabase(dbSessionId, rewards);
            }
        }
        private void ProcessHatchedEggReward(string acc, GetHatchedEggsOutProto getHatchedEggsProto)
        {
            if (getHatchedEggsProto.HatchedPokemon.Count <= 0)
            {
                return;
            }
            if (ConfigurationManager.Shared.Config.Http.Enabled)
            {
                Stats entry = GetStatEntry();

                entry.AddXp(getHatchedEggsProto.ExpAwarded.Sum());
                entry.AddStardust(getHatchedEggsProto.StardustAwarded.Sum());

                foreach (PokemonProto pokemon in getHatchedEggsProto.HatchedPokemon)
                {
                    if (pokemon.PokemonDisplay != null && pokemon.PokemonDisplay.Shiny)
                    {
                        entry.ShinyPokemon++;
                    }
                }
            }

            if (ConfigurationManager.Shared.Config.MySql.Enabled)
            {
                connectionManager.AddHatchedEggToDatabase(dbSessionId, getHatchedEggsProto);
            }
        }
        private void ProcessPlayer(string acc, GetPlayerOutProto player, int level)
        {
            if (!player.Success)
            {
                return;
            }

            if (ConfigurationManager.Shared.Config.MySql.Enabled)
            {
                connectionManager.AddPlayerInfoToDatabase(accountId, player, level);
            }
        }

        public void ProcessCaughtPokemon(CatchPokemonOutProto caughtPokemon)
        {
            Stats entry = GetStatEntry();
            switch (caughtPokemon.Status)
            {
                case CatchPokemonOutProto.Types.Status.CatchSuccess:
                    if (entry != null)
                    {
                        entry.CaughtPokemon++;
                        if (caughtPokemon.PokemonDisplay != null && caughtPokemon.PokemonDisplay.Shiny)
                        {
                            entry.ShinyPokemon++;
                        }

                        entry.AddXp(caughtPokemon.Scores.Exp.Sum());
                        entry.AddStardust(caughtPokemon.Scores.Stardust.Sum());
                    }

                    if (ConfigurationManager.Shared.Config.MySql.Enabled)
                    {
                        connectionManager.AddPokemonToDatabase(dbSessionId, caughtPokemon, null);
                    }
                    break;
                case CatchPokemonOutProto.Types.Status.CatchFlee:
                    if (entry != null)
                    {
                        entry.AddXp(caughtPokemon.Scores.Exp.Sum());
                        entry.FleetPokemon++;
                    }

                    if (ConfigurationManager.Shared.Config.MySql.Enabled)
                    {
                        connectionManager.AddPokemonToDatabase(dbSessionId, caughtPokemon, lastEncounterPokemon);
                    }
                    break;
            }
        }
    }
}
