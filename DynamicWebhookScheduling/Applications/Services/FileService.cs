using DynamicWebhookScheduling.Model;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.Json;

namespace DynamicWebhookScheduling.Applications.Services
{
    public class Record
    {
        public static readonly int ID_SIZE = 32;
        public static readonly int STATE_SIZE = 8;
        public static readonly int DATA_OFFSET = 64;
        public static readonly int DATA_LENGTH = 64;
        public static readonly int TOTAL_SIZE = ID_SIZE + DATA_OFFSET + DATA_LENGTH;
        public int Id { get; set; }
        public bool State { get; set; }
        public long DataOffset { get; set; }
        public long DataLength { get; set; }

        public byte[] Bytes { get
            {
                var newBytes = new byte[TOTAL_SIZE];

                var idBytes = BitConverter.GetBytes(this.Id);
                var stateBytes = BitConverter.GetBytes(this.State);
                var dataOffsetBytes = BitConverter.GetBytes(this.DataOffset);
                var dataLengthBytes = BitConverter.GetBytes(this.DataLength);


                idBytes.CopyTo(this.Bytes, 0);
                stateBytes.CopyTo(this.Bytes, ID_SIZE);
                dataOffsetBytes.CopyTo(this.Bytes, ID_SIZE + STATE_SIZE);
                dataLengthBytes.CopyTo(this.Bytes, ID_SIZE + STATE_SIZE + DATA_LENGTH);

                return newBytes;
            }
        }

        public Record(int id, bool state, long dataOffset, long dataLength)
        {
            this.Id = id;
            this.State = state;
            this.DataOffset = dataOffset;
            this.DataLength = dataLength;
        }

        public static Record FromBytes(byte[] bytes)
        {
            var idBytes = BitConverter.ToInt32(bytes, 0);
            var stateBytes = BitConverter.ToBoolean(bytes, ID_SIZE);
            var dataOffset = BitConverter.ToInt64(bytes, ID_SIZE + STATE_SIZE);
            var dataLength = BitConverter.ToInt64(bytes, ID_SIZE + STATE_SIZE + DATA_OFFSET);
            return new Record(idBytes, stateBytes, dataOffset, dataLength);
        }
    }

    public class RecordData
    {
        private readonly UTF32Encoding Encoding = new();
        private int DataLength { get
            {
                return this.Bytes.Length;
            }
        }

        public byte[] Bytes { get; }
        
        public RecordData(string data)
        {
            var dataBytes = this.Encoding.GetBytes(data);
            this.Bytes = new byte[dataBytes.Length];
            dataBytes.CopyTo(dataBytes, 0);
        }
    }

    public class Pointer : IDisposable
    {
        private readonly bool IsUseReplaceAble;
        private readonly Action<bool> _commit;
        private readonly SemaphoreSlim semaphoreSlim;
        private bool _isDisposed = false;

        public int Value { get; }

        public Pointer(bool isUseReplaceAble, int value, SemaphoreSlim semaphoreSlim, Action<bool> commit)
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

    public class PointerManager
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

        public static Pointer GetId()
        {
            semaphoreSlim.Wait();

            var isUseReplaceAble = ReplaceAble.Count > 0;
            var id = isUseReplaceAble ? ReplaceAble[0] : CURRENT_ID;

            return new Pointer(isUseReplaceAble, id, semaphoreSlim, _commit);
        }

        public static void AddReplaceAble(int id)
        {
            lock (@lock)
            {
                ReplaceAble.Add(id);
            }
        }
    }

    public class FileService
    {
        private const string FILE_NAME = "jobs.bin";
        private const string FILE_DATA = "data.bin";
        private static readonly SemaphoreSlim _fileLock = new(1, 1);

        public async Task<List<string>> LoadDatas()
        {
            using var fs = new FileStream(FILE_NAME, FileMode.OpenOrCreate, FileAccess.Read, FileShare.Read);
            var bytes = new byte[fs.Length];
            await fs.ReadExactlyAsync(bytes);
            for(var i = 0; i < bytes.Length / Record.TOTAL_SIZE; i++) {
                var recordBytes = new byte[Record.TOTAL_SIZE];
                Array.Copy(bytes, i * Record.TOTAL_SIZE, recordBytes, 0, Record.TOTAL_SIZE);
                var record = Record.FromBytes(recordBytes);
                if (record.State)
                {
                    using var fs2 = new FileStream(FILE_DATA, FileMode.OpenOrCreate, FileAccess.Read, FileShare.Read);
                    var dataBytes = new byte[record.DataLength];
                    await fs2.ReadExactlyAsync(dataBytes, ((int)record.DataOffset), ((int)record.DataLength));
                    var data = Encoding.UTF32.GetString(dataBytes);
                    yield return data;
                }
            }
        }

        public async Task<int> SaveData(string data, CancellationToken token)
        {
            await _fileLock.WaitAsync(token);
            try
            {
                using var fs2 = new FileStream(FILE_DATA, FileMode.Append);
                
                var currBytePosition = fs2.Position;
                var recordData = new RecordData(data);
                await fs2.WriteAsync(recordData.Bytes, token);

                using var fs = new FileStream(FILE_NAME, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);

                using var pointer = PointerManager.GetId();
                var record = new Record(pointer.Value, false, currBytePosition., recordData.Bytes.LongLength);

                fs.Position = Record.TOTAL_SIZE * pointer.Value;

                await fs.WriteAsync(record.Bytes, token);

                if (!token.IsCancellationRequested)
                {
                    pointer.Commit();
                    return pointer.Value;
                }
                return -1;
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public async Task DeleteData(int pointer)
        {

            await _fileLock.WaitAsync();
            try
            {
                using var fs = new FileStream(FILE_NAME, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

                var bytes = new byte[Record.TOTAL_SIZE];
                await fs.ReadExactlyAsync(bytes, Record.TOTAL_SIZE * pointer, Record.TOTAL_SIZE, CancellationToken.None);
                var record = Record.FromBytes(bytes);

                await fs.WriteAsync(record.Bytes);
                
                PointerManager.AddReplaceAble(pointer);
            }
            finally { 
                _fileLock.Release(); 
            }
        }
    }
}
