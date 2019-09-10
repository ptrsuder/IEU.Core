namespace ImageEnhancingUtility
{
    public class KeyValue<TKey, TValue>
    {
        public TKey Key { get; set; }
        public TValue Value { get; set; }

        public KeyValue() { }

        public KeyValue(TKey key, TValue val)
        {
            this.Key = key;
            this.Value = val;
        }
    }
}
