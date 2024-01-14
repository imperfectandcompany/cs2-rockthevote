﻿using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using static CounterStrikeSharp.API.Core.Listeners;

namespace cs2_rockthevote
{
    public class RockTheVote : BasePlugin, IPluginConfig<Config>
    {
        public override string ModuleName => "RockTheVote";
        public override string ModuleVersion => "0.0.3";
        public override string ModuleAuthor => "abnerfs";
        public override string ModuleDescription => "You know what it is, rtv";

        CCSGameRules? _gameRules = null;
        ServerManager ServerManager = new();
        NominationManager NominationManager = new();
        AsyncVoteManager Rtv = null;
        List<string> Maps = new();

        public Config? Config { get; set; }


        public bool WarmupRunning
        {
            get
            {
                if (_gameRules is null)
                    SetGameRules();

                return _gameRules is not null && _gameRules.WarmupPeriod;
            }
        }

        void LoadMaps()
        {
            Maps = new List<string>();
            string mapsFile = Path.Combine(ModuleDirectory, "maplist.txt");
            if (!File.Exists(mapsFile))
                throw new FileNotFoundException(mapsFile);

            Maps = File.ReadAllText(mapsFile)
                .Replace("\r\n", "\n")
                .Split("\n")
                .Select(x => x.Trim())
                .Where(x => !x.StartsWith("//"))
                .ToList();
        }

        public override void Load(bool hotReload)
        {
            Init();
            RegisterListener<OnMapStart>((_mapname) => Init());
        }

        void Init()
        {
            NominationManager = new();
            LoadMaps();
            _gameRules = null;
            AddTimer(1.0F, SetGameRules);
            if (Config is not null)
            {
                AsyncVoteValidator validator = new(Config!.RtvVotePercentage, ServerManager);
                Rtv = new AsyncVoteManager(validator);
            }
        }

        void SetGameRules() => _gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules!;

        bool ValidateCommand(CCSPlayerController? player)
        {
            if (player is null || !player.IsValid) return false;

            if (WarmupRunning && Config!.DisableVotesInWarmup)
            {
                player.PrintToChat("[RockTheVote] Command disabled during warmup.");
                return false;
            }

            if (ServerManager.ValidPlayerCount < Config!.RtvMinPlayers)
            {
                player.PrintToChat($"[RockTheVote] Minimum players to use this command is {Config.RtvMinPlayers}");
                return false;
            }

            return true;
        }

        [GameEventHandler(HookMode.Pre)]
        public HookResult EventPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo @eventInfo)
        {
            var userId = @event.Userid.UserId!.Value;
            Rtv.RemoveVote(userId);
            return HookResult.Continue;
        }

        void NominateHandler(CCSPlayerController? player, string map)
        {
            if (string.IsNullOrEmpty(map))
            {
                player!.PrintToChat($"[RockTheVote] Usage: nominate <map-name>");
            }
            else if (Maps.FirstOrDefault(x => x.ToLower() == map) is not null)
            {
                if (map == Server.MapName)
                {
                    player!.PrintToChat($"[RockTheVote] You can't nominate the current map");
                    return;
                }

                NominationManager.Nominate(player.UserId!.Value, map);
                Server.PrintToChatAll($"[RockTheVote] Player {player.PlayerName} nominated map {map}");
            }
            else
            {
                player!.PrintToChat($"[RockTheVote] Invalid map");
            }
        }

        [ConsoleCommand("nominate", "nominate a map to rtv")]
        public void OnNominate(CCSPlayerController? player, CommandInfo command)
        {
            if (!ValidateCommand(player))
                return;

            string map = command.GetArg(1).Trim().ToLower();
            NominateHandler(player, map);
        }

        IList<T> Shuffle<T>(Random rng, IList<T> array)
        {
            int n = array.Count;
            while (n > 1)
            {
                int k = rng.Next(n--);
                T temp = array[n];
                array[n] = array[k];
                array[k] = temp;
            }
            return array;
        }


        [ConsoleCommand("rtv", "Votes to rock the vote")]
        public void OnRTV(CCSPlayerController? player, CommandInfo? command)
        {
            if (!ValidateCommand(player))
                return;

            VoteResult result = Rtv.AddVote(player!.UserId!.Value);
            switch (result)
            {
                case VoteResult.Added:
                    Server.PrintToChatAll($"[RockTheVote] {player.PlayerName} wants to rock the vote ({Rtv.VoteCount} voted, {Rtv.RequiredVotes} needed)");
                    break;
                case VoteResult.AlreadyAddedBefore:
                    player.PrintToChat($"[RockTheVote] You already rocked the vote ({Rtv.VoteCount} voted, {Rtv.RequiredVotes} needed)");
                    break;
                case VoteResult.VotesReached:
                    Server.PrintToChatAll("[RockTheVote] Number of votes reached, the vote for the next map will start");
                    var mapsScrambled = Shuffle(new Random(), Maps.Where(x => x != Server.MapName).ToList());
                    var maps = NominationManager.Votes().Concat(mapsScrambled).Distinct().ToList();
                    VoteManager manager = new(maps!, this, 30, ServerManager.ValidPlayerCount);
                    manager.StartVote();
                    break;
            }
        }

        [GameEventHandler(HookMode.Post)]
        public HookResult OnChat(EventPlayerChat @event, GameEventInfo info)
        {
            var player = Utilities.GetPlayerFromUserid(@event.Userid);
            if (!ValidateCommand(player))
                return HookResult.Continue;

            var text = @event.Text.Trim().ToLower();
            if (@event.Text.Trim() == "rtv")
            {
                OnRTV(player, null);
            }
            else if (text.StartsWith("nominate"))
            {

                var split = text.Split("nominate");
                var map = split.Length > 1 ? split[1].Trim() : "";
                NominateHandler(player, map);
            }


            return HookResult.Continue;
        }

        public void OnConfigParsed(Config config)
        {
            Config = config;
            Init();
        }
    }
}