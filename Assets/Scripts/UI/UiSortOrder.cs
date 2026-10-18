namespace Gladiator.UI
{
    /// <summary>Canvas sorting orders, lowest first.</summary>
    public static class UiSortOrder
    {
        public const int WorldOverlay = 0;   // damage numbers, indicators
        public const int Hud = 10;
        public const int LevelUp = 20;
        public const int Menus = 30;
        public const int Debug = 100;
    }
}
