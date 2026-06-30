using System;
using Chido.Core.Entities;

namespace Chido.Core.Battle;

public class BattleParticipant
{
    public IEntity Entity        { get; }
    public ulong?  DiscordUserId { get; } // 敵は null
    public int     Initiative    { get; private set; }
    public bool    HasActed      { get; set; }

    public bool IsPlayer => DiscordUserId.HasValue;

    public BattleParticipant(IEntity entity, ulong? discordUserId = null)
    {
        Entity        = entity;
        DiscordUserId = discordUserId;
    }

    /// <summary>Speed + Speed×20% 乱数でイニシアティブ決定</summary>
    public void RollInitiative(Random rng)
    {
        var jitter = rng.Next(0, Entity.Speed / 5 + 1);
        Initiative = Entity.Speed + jitter;
    }
}
