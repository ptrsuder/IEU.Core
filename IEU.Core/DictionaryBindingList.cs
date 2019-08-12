using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace ImageEnhancingUtility.Train
{
    public class DictionaryBindingList<TKey, TValue> : BindingList<KeyValue<TKey, TValue>>
    {
        public readonly IDictionary<TKey, TValue> Dictionary;
        public DictionaryBindingList()
        {
            Dictionary = new Dictionary<TKey, TValue>();
        }


        public void Add(TKey key, TValue value)
        {
            if (Dictionary.ContainsKey(key))
            {
                int position = IndexOf(key);
                Dictionary.Remove(key);
                Remove(key);
                InsertItem(position, new KeyValue<TKey, TValue>(key, value));
                return;
            }
            base.Add(new KeyValue<TKey, TValue>(key, value));
        }

        public void Remove(TKey key)
        {
            var item = this.First(x => x.Key.Equals(key));
            base.Remove(item);
        }

        protected override void InsertItem(int index, KeyValue<TKey, TValue> item)
        {
            Dictionary.Add(item.Key, item.Value);
            base.InsertItem(index, item);
        }

        protected override void RemoveItem(int index)
        {
            Dictionary.Remove(this[index].Key);
            base.RemoveItem(index);
        }

        public int IndexOf(TKey key)
        {
            var item = this.FirstOrDefault(x => x.Key.Equals(key));
            return item.Equals(null) ? -1 : base.IndexOf(item);
        }
    }
}
