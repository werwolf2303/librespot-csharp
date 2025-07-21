using System;
using System.Collections.Generic;

namespace lib.common
{
    public class FisherYatesShuffle<I>
    {
        private Random random;
        private volatile int _currentSeed;
        private volatile int _sizeForSeed = -1;

        public FisherYatesShuffle(Random random)
        {
            this.random = random;
        }

        private static int[] GetShuffleExchanges(int size, int seed)
        {
            int[] exchanges = new int[size - 1];
            Random rand = new Random(seed);
            for (int i = size - 1; i > 0; i--)
            {
                int n = rand.Next(i + 1);
                exchanges[size - 1 - i] = n;
            }
            
            return exchanges;
        }

        public void Shuffle(List<I> list, bool saveSeed)
        {
            Shuffle(list, 0, list.Count, saveSeed);
        }

        public void Shuffle(List<I> list, int from, int to, bool saveSeed)
        {
            int seed = random.Next();
            if (saveSeed) _currentSeed = seed;

            int size = to - from;
            if (saveSeed) _sizeForSeed = size;
            
            int[] exchanges = GetShuffleExchanges(size, seed);
            for (int i = size - 1; i > 0; i--)
            {
                int n = exchanges[size - 1 - i];
                Utils.Swap(list, from + n, from + i);
            }
        }

        public void Unshuffle(List<I> list)
        {
            Unshuffle(list, 0, list.Count);
        }

        public void Unshuffle(List<I> list, int from, int to)
        {
            if (_currentSeed == 0) throw new Exception("Current seed is zero!");
            if (_sizeForSeed != to - from) throw new Exception("Size mismatch! Cannot unshuffle.");

            int size = to - from;
            int[] exchanges = GetShuffleExchanges(size, _currentSeed);
            for (int i = 1; i < size; i++)
            {
                int n = exchanges[size - i - 1];
                Utils.Swap(list, from + n, from + i);
            }

            _currentSeed = 0;
            _sizeForSeed = -1;
        }

        public bool CanUnshuffle(int size)
        {
            return _currentSeed != 0 && _sizeForSeed == size;
        }
    }
}