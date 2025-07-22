using System;
using System.Runtime.CompilerServices;
using lib.common;
using log4net;

namespace player.playback
{
    public class PlayerQueue : IDisposable
    {
        private static ILog LOGGER = LogManager.GetLogger(typeof(PlayerQueue));
        private ScheduledExecutorService _executorService;
        private PlayerQueueEntry _head = null;

        internal PlayerQueue()
        {
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        internal PlayerQueueEntry Head()
        {
            return _head;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        internal PlayerQueueEntry Prev()
        {
            if (_head == null || _head._prev == null) return null;
            else return _head._prev;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        internal void Add(PlayerQueueEntry entry)
        {
            if (_head == null) _head = entry;
            else _head.SetNext(entry);
            _executorService.schedule(new ScheduledExecutorService.ScheduledFuture<int>(() =>
            {
                entry.Run();
                return 0;
            }, 100, ScheduledExecutorService.TimeUnit.MILLISECONDS));
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        internal void Swap(PlayerQueueEntry oldEntry, PlayerQueueEntry newEntry)
        {
            if (_head == null) return;

            bool swapped;
            if (_head == oldEntry)
            {
                _head = newEntry;
                _head._next = oldEntry._next;
            }
        }

        public abstract class Entry
        {
            public PlayerQueueEntry Next = null;
            public PlayerQueueEntry Prev = null;

            public void SetNext(PlayerQueueEntry entry)
            {
                if (Next == null)
                {
                    Next = entry;
                    entry.Prev = (PlayerQueueEntry)this;
                } else Next.SetNext(entry);
            }

            public bool Remove(PlayerQueueEntry entry)
            {
                if (Next == null) return false;
                if (Next == entry)
                {
                    PlayerQueueEntry tmp = Next;
                    Next = tmp.Next;
                    tmp.Dispose();
                    return true;
                } else return Next.Remove(entry);
            }

            public bool Swap(PlayerQueueEntry oldEntry, PlayerQueueEntry newEntry)
            {
                if (Next == null) return false;
                if (Next == oldEntry)
                {
                    Next = newEntry;
                    Next.Prev = oldEntry.Prev;
                    Next.Next = oldEntry.Next;
                    return true;
                } else return Next.Swap(oldEntry, newEntry);
            }

            public void Clear()
            {
                if (Prev != null)
                {
                    Entry tmp = Prev;
                    Prev = null;
                    if (tmp != this) tmp.Clear();
                }

                if (Next != null)
                {
                    Entry tmp = Next;
                    Next = null;
                    if (tmp != this) tmp.Clear();
                }
                
                ((PlayerQueueEntry)this).Dispose();
            }
        }
    }
}