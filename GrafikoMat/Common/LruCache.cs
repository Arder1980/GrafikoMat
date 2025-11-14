using System;
using System.Collections.Generic;

namespace GrafikoMat.Common
{
    /// <summary>
    /// Interfejs dla słownika z podstawowymi operacjami.
    /// </summary>
    public interface IDictionaryLike<TKey, TValue>
    {
        TValue this[TKey key] { get; set; }
        bool TryGetValue(TKey key, out TValue value);
        bool ContainsKey(TKey key);
        int Count { get; }
    }

    /// <summary>
    /// Adapter dla Dictionary aby był kompatybilny z IDictionaryLike.
    /// </summary>
    public class DictionaryAdapter<TKey, TValue> : IDictionaryLike<TKey, TValue> where TKey : notnull
    {
        private readonly Dictionary<TKey, TValue> _dictionary;

        public DictionaryAdapter(Dictionary<TKey, TValue> dictionary)
        {
            _dictionary = dictionary ?? throw new ArgumentNullException(nameof(dictionary));
        }

        public TValue this[TKey key]
        {
            get => _dictionary[key];
            set => _dictionary[key] = value;
        }

        public bool TryGetValue(TKey key, out TValue value) => _dictionary.TryGetValue(key, out value!);
        public bool ContainsKey(TKey key) => _dictionary.ContainsKey(key);
        public int Count => _dictionary.Count;
    }

    /// <summary>
    /// Implementacja LRU (Least Recently Used) cache z ograniczonym rozmiarem.
    /// Automatycznie usuwa najmniej używane elementy gdy osiągnięty zostanie limit.
    /// </summary>
    public class LruCache<TKey, TValue> : IDictionaryLike<TKey, TValue> where TKey : notnull
    {
        private readonly int _maxSize;
        private readonly Dictionary<TKey, LinkedListNode<CacheItem>> _cache;
        private readonly LinkedList<CacheItem> _lruList;

        public LruCache(int maxSize)
        {
            if (maxSize <= 0)
                throw new ArgumentException("Max size must be positive", nameof(maxSize));

            _maxSize = maxSize;
            _cache = new Dictionary<TKey, LinkedListNode<CacheItem>>(maxSize);
            _lruList = new LinkedList<CacheItem>();
        }

        public int Count => _cache.Count;

        public TValue this[TKey key]
        {
            get
            {
                if (!_cache.TryGetValue(key, out var node))
                    throw new KeyNotFoundException($"Key '{key}' not found in cache");

                // Przenieś na początek (najbardziej ostatnio używany)
                _lruList.Remove(node);
                _lruList.AddFirst(node);

                return node.Value.Value;
            }
            set
            {
                if (_cache.TryGetValue(key, out var node))
                {
                    // Aktualizuj istniejącą wartość
                    node.Value.Value = value;
                    _lruList.Remove(node);
                    _lruList.AddFirst(node);
                }
                else
                {
                    // Dodaj nowy element
                    if (_cache.Count >= _maxSize)
                    {
                        // Usuń najmniej ostatnio używany element
                        var lruNode = _lruList.Last;
                        if (lruNode != null)
                        {
                            _lruList.RemoveLast();
                            _cache.Remove(lruNode.Value.Key);
                        }
                    }

                    var newNode = new LinkedListNode<CacheItem>(new CacheItem(key, value));
                    _lruList.AddFirst(newNode);
                    _cache[key] = newNode;
                }
            }
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            if (_cache.TryGetValue(key, out var node))
            {
                // Przenieś na początek (najbardziej ostatnio używany)
                _lruList.Remove(node);
                _lruList.AddFirst(node);

                value = node.Value.Value;
                return true;
            }

            value = default!;
            return false;
        }

        public bool ContainsKey(TKey key)
        {
            return _cache.ContainsKey(key);
        }

        public void Clear()
        {
            _cache.Clear();
            _lruList.Clear();
        }

        public IEnumerable<TKey> Keys => _cache.Keys;

        public IEnumerable<TValue> Values
        {
            get
            {
                foreach (var node in _lruList)
                {
                    yield return node.Value;
                }
            }
        }

        private class CacheItem
        {
            public TKey Key { get; }
            public TValue Value { get; set; }

            public CacheItem(TKey key, TValue value)
            {
                Key = key;
                Value = value;
            }
        }
    }
}
