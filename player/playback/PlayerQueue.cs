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
        private Object _funcLock = new Object();

        internal PlayerQueue(ScheduledExecutorService executorService)
        {
            _executorService = executorService;
        }
        
        internal PlayerQueueEntry Next()
        {
            lock (_funcLock)
            {
                if (_head == null || _head.Next == null) return null;
                else return _head.Next;
            }
        }
        
        internal PlayerQueueEntry Head()
        {
            lock (_funcLock)
            {
                return _head;
            }
        }
        
        internal PlayerQueueEntry Prev()
        {
            lock (_funcLock)
            {
                if (_head == null || _head.Prev == null) return null;
                else return _head.Prev;
            }
        }
        
        internal void Add(PlayerQueueEntry entry)
        {
            lock (_funcLock)
            {
                if (_head == null) _head = entry;
                else _head.SetNext(entry);
                _executorService.schedule(new ScheduledExecutorService.ScheduledFuture<int>(() =>
                {
                    entry.Run();
                    return 0;
                }, 100, ScheduledExecutorService.TimeUnit.MILLISECONDS));
                
                LOGGER.DebugFormat("{0} added to queue.", entry);
            }
        }
        
        internal void Swap(PlayerQueueEntry oldEntry, PlayerQueueEntry newEntry)
        {
            lock (_funcLock)
            {
                if (_head == null) return;

                bool swapped;
                if (_head == oldEntry)
                {
                    _head = newEntry;
                    _head.Next = oldEntry.Next;
                    _head.Next = oldEntry.Next;
                    _head.Prev = oldEntry.Prev;
                    swapped = true;
                }
                else swapped = _head.Swap(oldEntry, newEntry);

                oldEntry.Dispose();
                if (swapped)
                {
                    _executorService.schedule(new ScheduledExecutorService.ScheduledFuture<int>(() =>
                    {
                        newEntry.Run();
                        return 0;
                    }, 100, ScheduledExecutorService.TimeUnit.MILLISECONDS));
                }
            }
        }
        
        internal void Remove(PlayerQueueEntry entry)
        {
            lock (_funcLock)
            {
                if (_head == null) return;

                bool removed;
                if (_head == entry)
                {
                    PlayerQueueEntry tmp = _head;
                    _head = _head.Next;
                    tmp.Dispose();
                    removed = true;
                }
                else removed = _head.Remove(entry);
            }
        }

        internal bool Advance()
        {
            lock (_funcLock)
            {
                if (_head == null || _head.Next == null) return false;

                PlayerQueueEntry tmp = _head.Next;
                _head.Next = null;
                _head.Prev = null;
                if (!_head.CloseIfUseless()) tmp.Prev = _head;
                _head = tmp;
                return true;
            }
        }

        public void Dispose()
        {
            _head?.Clear();
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