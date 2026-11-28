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

    public enum BulletPathType
    {
        LinearRay = 0,
        HeartCardioidBurst = 1,
        CurvedArc = 2,
        ArchimedeanSpiral = 3,
        Space = 4
    }

    public enum PathPatternType
    {
        LinearRay = 0,
        SineWave = 1,
        Spiral = 2,
        CurvedArc = 3,
        RoseCurve = 4,
        LogarithmicSpiral = 5,
        DampedWave = 6,
        ExpandingHelix = 7,
        ZigZagWave = 8,
        HeartBurst = 9,
        StarburstPolygon = 10,
        BoomerangHook = 11
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
        SpreadPlusOne = 1 << 0, // Each stack adds +1 bullet spread angle
        DoubleWay = 1 << 1,     // Each stack adds +1 parallel firing lane
        RapidFire = 1 << 2,
        DamageUp = 1 << 3
    }
}
