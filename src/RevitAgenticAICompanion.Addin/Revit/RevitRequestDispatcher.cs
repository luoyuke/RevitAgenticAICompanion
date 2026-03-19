using System.Collections.Concurrent;
using System.Threading.Tasks;
using Autodesk.Revit.UI;

namespace RevitAgenticAICompanion.Revit
{
    public sealed class RevitRequestDispatcher : IExternalEventHandler
    {
        private readonly ConcurrentQueue<RevitRequest> _queue = new ConcurrentQueue<RevitRequest>();
        private ExternalEvent _externalEvent;

        public void BindExternalEvent(ExternalEvent externalEvent)
        {
            _externalEvent = externalEvent;
        }

        public Task<T> Enqueue<T>(RevitRequest<T> request)
        {
            _queue.Enqueue(request);
            _externalEvent.Raise();
            return request.Task;
        }

        public void Execute(UIApplication app)
        {
            var context = new RevitRequestContext(app);
            while (_queue.TryDequeue(out var request))
            {
                request.Execute(context);
            }
        }

        public string GetName()
        {
            return "Revit Agentic AI Companion Request Dispatcher";
        }
    }
}
