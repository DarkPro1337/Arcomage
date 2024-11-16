namespace Arcomage.Scripts;

public enum CardType
{
    Brick,
    Gem,
    Recruit,
    None
}

public enum CardsUse
{
    Attack,
    Defence,
    Resource
}

public enum CardFeature
{
    PlayAgain,
    DrawDiscard,
    NotDiscardable
}

public enum EffectType
{
    Gain,
    Lose,
    Set,
    Damage,
    Swap
}

public enum TargetType
{
    Self,
    Opponent,
    All,
    AllExceptSelf,
    LowestWall,
    HighestWall,
    LowestTower,
    HighestTower
}

public enum ConditionType
{
    LessThan,
    GreaterThan,
    Equals,
    NotEquals,
    GreaterThanOrEqual,
    LessThanOrEqual
}

public enum ResourceTypes
{
    Tower,
    Wall,
    Quarry,
    Magic,
    Dungeon,
    Bricks,
    Gems,
    Recruits,
    OpponentTower,
    OpponentWall,
    OpponentQuarry,
    OpponentMagic,
    OpponentDungeon,
    OpponentBricks,
    OpponentGems,
    OpponentRecruits,
    LowestWall,
    HighestWall,
    HighestTower,
    LowestTower,
    HighestQuarry,
    HighestBricks,
    HighestMagic,
    HighestGems,
    HighestDungeon,
    HighestRecruits,
    LowestQuarry,
    LowestBricks,
    LowestMagic,
    LowestGems,
    LowestDungeon,
    LowestRecruits
}

public enum ActionType
{
    Default,
    Conditional,
    Swap,
}