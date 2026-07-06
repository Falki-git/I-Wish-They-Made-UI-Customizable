namespace CustomizableUI.Groups
{
    /// <summary>
    /// The persisted (savable) state of a single top-level HUD group. Position is stored as
    /// plain floats rather than a UnityEngine.Vector3 so save data doesn't depend on how a
    /// particular JSON library chooses to (de)serialize Unity's value types.
    /// </summary>
    public class GroupLayout
    {
        public string Key;
        public float PositionX;
        public float PositionY;
        public float PositionZ;
        public bool IsActive;
        public bool AttachToNavball;
        public float Scale = 1f;
        public bool ScaleWithNavball;
    }
}
