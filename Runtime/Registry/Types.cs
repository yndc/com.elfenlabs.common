namespace Elfenlabs.Registry
{
    public struct RuntimeIndex<T>
    {
        public int Value;

        public RuntimeIndex(int value)
        {
            Value = value;
        }

        public static implicit operator int(RuntimeIndex<T> index) => index.Value;
        public static implicit operator RuntimeIndex<T>(int value) => new RuntimeIndex<T>(value);
    }
}