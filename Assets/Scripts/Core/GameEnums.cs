using System;

namespace LetsDash.Core
{
    public enum GameState
    {
        Menu,
        Loading,
        Countdown,
        Playing,
        Paused,
        GameOver,
        Victory
    }

    public enum PathPatternType
    {
        LinearRay = 0,
        SineWave = 1,
        Spiral = 2,
        CurvedArc = 3,
        RoseCurve = 4
    }

    public enum BulletVisualType
    {
        Bullet_1 = 0, // Small fast pellet
        Bullet_2 = 1, // Medium orb
        Bullet_3 = 2  // Large heavy orb
    }

    [Flags]
    public enum WeaponUpgradeFlags
    {
        None = 0,
        Spread2Way = 1 << 0,
        Spread3Way = 1 << 1,
        ParallelOffset = 1 << 2,
        RapidFire = 1 << 3,
        DamageUp = 1 << 4
    }
}
