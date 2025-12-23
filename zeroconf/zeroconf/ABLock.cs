/*
 * The MIT License
 *
 * Copyright 2021 erhannis.
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */

using System.Threading;

namespace zeroconf.zeroconf
{
    public sealed class ABLock
    {
        private readonly object sync = new object();
        private long a1 = 0;
        private long a2 = 0;
        private long b = 0;

        /// <summary>
        /// For every call to LockA1, there must be exactly one subsequent call to UnlockA1.
        /// Attempted locks on A1 are not blocked by anything.
        /// An existing lock on A1 blocks new locks on B.
        /// An existing lock on A1 DOES NOT block new locks on A1 or A2.
        /// </summary>
        public void LockA1()
        {
            lock (sync)
            {
                a1++;
                Monitor.PulseAll(sync);
            }
        }

        public void UnlockA1()
        {
            lock (sync)
            {
                a1--;
                Monitor.PulseAll(sync);
            }
        }

        /// <summary>
        /// For every call to LockA2, there must be a subsequent call to UnlockA2.
        /// Attempted locks on A2 are blocked by existing locks on B, and nothing else.
        /// An existing lock on A2 blocks new locks on B.
        /// An existing lock on A2 DOES NOT block new locks on A1 or A2.
        /// </summary>
        public void LockA2()
        {
            lock (sync)
            {
                while (b > 0)
                    Monitor.Wait(sync);

                a2++;
                Monitor.PulseAll(sync);
            }
        }

        public void UnlockA2()
        {
            lock (sync)
            {
                a2--;
                Monitor.PulseAll(sync);
            }
        }

        /// <summary>
        /// For every call to LockB, there must be a subsequent call to UnlockB.
        /// Attempted locks on B are blocked by existing locks on A1 and A2, and nothing else.
        /// An existing lock on B blocks new locks on A2.
        /// An existing lock on B DOES NOT block new locks on A1 or B.
        /// </summary>
        public void LockB()
        {
            lock (sync)
            {
                while (a1 > 0 || a2 > 0)
                    Monitor.Wait(sync);

                b++;
                Monitor.PulseAll(sync);
            }
        }

        public void UnlockB()
        {
            lock (sync)
            {
                b--;
                Monitor.PulseAll(sync);
            }
        }
    }
}