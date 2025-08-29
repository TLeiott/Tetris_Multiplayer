namespace TetrisMultiplayer.Model
{
    // TODO: Datenmodelle implementieren
    public class Player { }

    public class PlayerState
    {
        public string PlayerId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Hp { get; set; }
        public bool IsSpectator { get; set; }
    }
}
