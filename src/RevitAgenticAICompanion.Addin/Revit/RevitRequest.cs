using System;
using System.Threading.Tasks;

namespace RevitAgenticAICompanion.Revit
{
    public abstract class RevitRequest
    {
        internal abstract void Execute(RevitRequestContext context);
    }

    public abstract class RevitRequest<T> : RevitRequest
    {
        private readonly TaskCompletionSource<T> _taskCompletionSource = new TaskCompletionSource<T>();

        public Task<T> Task
        {
            get { return _taskCompletionSource.Task; }
        }

        protected abstract T ExecuteCore(RevitRequestContext context);

        internal override void Execute(RevitRequestContext context)
        {
            try
            {
                _taskCompletionSource.TrySetResult(ExecuteCore(context));
            }
            catch (Exception ex)
            {
                _taskCompletionSource.TrySetException(ex);
            }
        }
    }
}
