using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace GameStash.Web.GUI.Helpers
{
    public static class ConcurrentBagExtensions
    {
        public static void RemoveWhere<T>(this ConcurrentBag<T> bag, Func<T, bool> predicate)
        {
            var itemsToKeep = new List<T>();
            while (bag.TryTake(out T item))
            {
                if (!predicate(item))
                {
                    itemsToKeep.Add(item);
                }
            }
            foreach (var item in itemsToKeep)
            {
                bag.Add(item);
            }
        }

        public static void RemoveItem<T>(this ConcurrentBag<T> bag, T item)
        {
            var itemsToKeep = new List<T>();
            while (bag.TryTake(out T bagItem))
            {
                if (!EqualityComparer<T>.Default.Equals(bagItem, item))
                {
                    itemsToKeep.Add(bagItem);
                }
            }
            foreach (var i in itemsToKeep)
            {
                bag.Add(i);
            }
        }

        public static void RemoveItems<T>(this ConcurrentBag<T> bag, IEnumerable<T> items)
        {
            var itemsToKeep = new List<T>();
            while (bag.TryTake(out T bagItem))
            {
                if (!items.Contains(bagItem))
                {
                    itemsToKeep.Add(bagItem);
                }
            }
            foreach (var i in itemsToKeep)
            {
                bag.Add(i);
            }
        }
    }
}
