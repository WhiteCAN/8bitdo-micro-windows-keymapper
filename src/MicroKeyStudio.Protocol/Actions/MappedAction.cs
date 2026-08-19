namespace MicroKeyStudio.Protocol.Actions;

public abstract record MappedAction
{
    public sealed record KeyboardKey(string Key) : MappedAction;
    public sealed record KeyChord(IReadOnlyList<string> Keys) : MappedAction
    {
        public bool Equals(KeyChord? other)
        {
            return other is not null && Keys.SequenceEqual(other.Keys);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (string key in Keys)
            {
                hash.Add(key);
            }

            return hash.ToHashCode();
        }
    }
    public sealed record MouseAction(string Action) : MappedAction;
    public sealed record MediaKey(string Key) : MappedAction;
    public sealed record Macro(string MacroId) : MappedAction;
    public sealed record Disabled : MappedAction;
    public sealed record PassThrough : MappedAction;
}
