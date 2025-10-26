using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThreadSync
{
    public class TrackedMutex
    {
        public Mutex Mutex { get; }
        public bool IsLocked { get; private set; }

        public TrackedMutex()
        {
            Mutex = new Mutex();
            IsLocked = false;
        }

        public bool TryAcquire()
        {
            lock (this)
            {
                if (!IsLocked && Mutex.WaitOne(0))
                {
                    IsLocked = true;
                    return true;
                }
                return false;
            }
        }

        public void Release()
        {
            lock (this)
            {
                if (IsLocked)
                {
                    Mutex.ReleaseMutex();
                    IsLocked = false;
                }
            }
        }
    }
}
