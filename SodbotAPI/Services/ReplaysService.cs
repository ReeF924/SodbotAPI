using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SodbotAPI.DB;
using SodbotAPI.DB.Models;
using SodbotAPI.DB.Models.ReplaysDtos;


namespace SodbotAPI.Services;

public class ReplaysService : SodbotService
{
    public ReplaysService(IConfiguration config)
    {
        this.Context = new AppDbContext(config);
    }

    public List<Replay> GetReplays()
    {
        var replays = this.Context.Replays.ToList();

        return replays;
    }

    public List<Replay> GetReplaysWithPlayers()
    {
        var replays = this.Context.Replays.Include(r => r.ReplayPlayers).ToList();

        return replays;
    }

    public Replay? GetReplay(int id)
    {
        return this.Context.Replays.Include(r => r.ReplayPlayers).FirstOrDefault(r => r.Id == id);
    }

    public Replay? AddReplay(ReplayDto input, bool immediateSave = true)
    {
        //gets the right elo to update in case the player doesn't exist
        var eloProp = GetEloProperty(input.ReplayPlayers.Count, input.Franchise);

        input.ReplayPlayers.ForEach(player =>
        {
            var existingPlayer = this.Context.Players.Find(player.PlayerId);
            if (existingPlayer is null)
            {
                // var elo = (double)eloProp.GetValue(existingPlayer)!;
                var p = new Player()
                {
                    Id = player.PlayerId,
                    Nickname = player.Nickname,
                    SdElo = null,
                    SdTeamGameElo = null,
                    WarnoElo = null,
                    WarnoTeamGameElo = null
                };
                eloProp.SetValue(p, 1200 + (player.Elo - 1200) / 5);

                this.Context.Players.Add(p);
            }
            else if (eloProp.GetValue(existingPlayer) is null)
            {
                eloProp.SetValue(existingPlayer, 1200 + (player.Elo - 1200) / 5);
            }
        });


        var replayType = input.ReplayType;

        if (replayType is null)
        {
            var channel = this.Context.Channels.Find(input.UploadedIn);

            replayType = channel?.SkillLevel ?? SkillLevel.others;
        }

        Replay replay = new()
        {
            Id = 0,
            SessionId = input.SessionId,
            UploadedIn = input.UploadedIn,
            UploadedBy = input.UploadedBy,
            UploadedAt = input.UploadedAt,
            Franchise = input.Franchise,
            Version = input.Version,
            IsTeamGame = input.IsTeamGame,
            Map = input.Map,
            MapType = input.MapType,
            VictoryCondition = input.VictoryCondition,
            DurationSec = input.DurationSec,
            SkillLevel = replayType.Value,
        };

        var replayPlayers = input.ReplayPlayers.Select(r => new ReplayPlayer()
        {
            PlayerId = r.PlayerId,
            Nickname = r.Nickname,
            Elo = r.Elo,
            MapSide = r.MapSide,
            Victory = r.Victory,
            Division = r.Division,
            Faction = r.Faction,
            Income = r.Income,
            DeckCode = r.DeckCode
        }).ToList();

        replay.ReplayPlayers = replayPlayers;

        this.Context.Replays.Add(replay);

        if (immediateSave)
            this.Context.SaveChanges();

        return replay;
    }
    
    public static PropertyInfo GetEloProperty(int playerCount, Franchise franchise)
    {
        bool isTeamGame = playerCount > 2;

        //returns property name depending on the franchise and if it's a team game (number of players > 2)
        var eloPropName = franchise == Franchise.sd2
            ? (isTeamGame ? "SdTeamGameElo" : "SdElo")
            : (isTeamGame ? "WarnoTeamGameElo" : "WarnoElo");


        var playerType = typeof(Player);

        //no need for nullable type, will always be found
        return playerType.GetProperty(eloPropName)!;
    }
}