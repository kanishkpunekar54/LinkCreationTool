//using System.Collections.Concurrent;

//namespace CrqAutomationApi.Services
//{
//    public class SseBroadcasterService
//    {
//        private readonly ConcurrentDictionary<Guid, StreamWriter> _clients = new();

//        public Guid AddClient(StreamWriter writer)
//        {
//            var id = Guid.NewGuid();
//            _clients.TryAdd(id, writer);
//            return id;
//        }

//        public void RemoveClient(Guid id)
//        {
//            _clients.TryRemove(id, out _);
//        }

//        public async Task BroadcastAsync(string message)
//        {
//            foreach (var client in _clients.Values)
//            {
//                try
//                {
//                    await client.WriteAsync($"data: {message}\n\n");
//                    await client.FlushAsync();
//                }
//                catch
//                {
//                    // Ignore exceptions for disconnected clients
//                }
//            }
//        }
//    }
//}
