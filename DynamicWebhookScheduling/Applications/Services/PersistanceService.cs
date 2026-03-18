using DynamicWebhookScheduling.Model;

namespace DynamicWebhookScheduling.Applications.Services
{
    public class FileRecordMeta
    {
        public static readonly int ID_SIZE = 64;
        public static readonly int DATE_SIZE = 64;
        public static readonly int STATE_SIZE = 1;
        public static readonly int DATA_OFFSET = 64;
        public static readonly int DATA_LENGTH = 64;
        public static readonly int TOTAL_SIZE = ID_SIZE + DATE_SIZE + STATE_SIZE + DATA_OFFSET + DATA_LENGTH;

        public byte[] Bytes { get; } = new byte[TOTAL_SIZE];

        public FileRecordMeta(int id, DateTime date, bool state)
        {
            var idBytes = BitConverter.GetBytes(id);
            var dateBytes = BitConverter.GetBytes(date.ToBinary());
            var stateBytes = BitConverter.GetBytes(state);

            idBytes.CopyTo(this.Bytes, 0);
            dateBytes.CopyTo(this.Bytes, ID_SIZE);
            stateBytes.CopyTo(this.Bytes, ID_SIZE + DATE_SIZE);
        }
    }

    public class Data
    {
        public byte[] Bytes { get; }
    }

    public class Id : IDisposable
    {
        private readonly bool IsUseReplaceAble;
        private readonly Action<bool> _commit;
        private readonly SemaphoreSlim semaphoreSlim;
        private bool _isDisposed = false;

        public int Value { get; }

        public Id(bool isUseReplaceAble, int value, SemaphoreSlim semaphoreSlim, Action<bool> commit)
        {
            this.IsUseReplaceAble = isUseReplaceAble;
            this.Value = value;
            this.semaphoreSlim = semaphoreSlim;
            this._commit = commit;
        }

        public void Commit()
        {
            if (this._isDisposed) return;

            this._commit(this.IsUseReplaceAble);
            this.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this._isDisposed)
            {
                if (disposing)
                {
                    this.semaphoreSlim.Release();
                }
                this._isDisposed = true;
            }
        }
    }

    public class IdManager
    {
        private static readonly Lock @lock = new();
        private static readonly List<int> ReplaceAble = [];
        private static int CURRENT_ID = 0;
        private static readonly SemaphoreSlim semaphoreSlim = new(1, 1);
        
        private static readonly Action<bool> _commit = (bool IsUseReplaceAble) =>
        {
            lock (@lock) {
                if (!IsUseReplaceAble)
                    Interlocked.Increment(ref CURRENT_ID);
                else
                    ReplaceAble.RemoveAt(0);
            }
        };

        public static Id GetId()
        {
            semaphoreSlim.Wait();

            var isUseReplaceAble = ReplaceAble.Count > 0;
            var id = isUseReplaceAble ? ReplaceAble[0] : CURRENT_ID;

            return new Id(isUseReplaceAble, id, semaphoreSlim, _commit);
        }

        public static void AddReplaceAble(int id)
        {
            lock (@lock)
            {
                ReplaceAble.Add(id);
            }
        }
    }

    public class PersistanceService
    {
        private const string FILE_NAME = "jobs.bin";
        private const string FILE_DATA = "data.bin";
        private static readonly SemaphoreSlim _fileLock = new(1, 1);

        public async Task<List<Job>> LoadJobs()
        {
            throw new NotImplementedException();
        }

        public async Task SaveJob(Job job, CancellationToken token)
        {
            await _fileLock.WaitAsync(token);
            try
            {
                using var fs2 = new FileStream(FILE_DATA, FileMode.Append);
                var currBytePosition = fs2.Position;
                using var fs = new FileStream(FILE_NAME, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);

                using var id = IdManager.GetId();
                var record = new FileRecordMeta(id.Value, job.RunAt, false);

                fs.Position = FileRecordMeta.TOTAL_SIZE * id.Value;

                await fs.WriteAsync(record.Bytes, token);

                if (!token.IsCancellationRequested)
                {
                    id.Commit();
                }
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public async Task DeleteJob(Job job)
        {
            if (job.Id == null) return;

            await _fileLock.WaitAsync();
            try
            {
                using var fs = new FileStream(FILE_NAME, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
 
                var record = new FileRecordMeta((int)job.Id, job.RunAt, true);
                fs.Position = FileRecordMeta.TOTAL_SIZE * (int)job.Id;

                await fs.WriteAsync(record.Bytes);
                
                IdManager.AddReplaceAble((int)job.Id);
            }
            finally { 
                _fileLock.Release(); 
            }
        }
    }
}
