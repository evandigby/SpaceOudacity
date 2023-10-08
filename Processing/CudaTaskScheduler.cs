using ManagedCuda;
using ManagedCuda.CudaFFT;
using ManagedCuda.VectorTypes;
using System.Collections.Concurrent;

namespace Processing
{
    public class CudaTaskScheduler : TaskScheduler, IDisposable
    {
        private readonly BlockingCollection<Task> taskQueue = new();
        private readonly List<Thread> threads = new();

        private readonly ThreadLocal<CudaContext> _cudaContext = new(() => new CudaContext());
        private readonly ThreadLocal<CudaFFTPlan1D> _cudaPlan;
        private readonly ThreadLocal<CudaDeviceVariable<cuFloatComplex>> _inputSignal;
        private readonly ThreadLocal<CudaDeviceVariable<cuFloatComplex>> _outputSignal;
        private readonly CancellationTokenSource cts = new();

        public CudaTaskScheduler(int signalSize, int maximumConcurrency)
        {
            _cudaPlan = new ThreadLocal<CudaFFTPlan1D>(() => new CudaFFTPlan1D(signalSize, cufftType.C2C, 1));
            _inputSignal = new ThreadLocal<CudaDeviceVariable<cuFloatComplex>>(() => new CudaDeviceVariable<cuFloatComplex>(signalSize));
            _outputSignal = new ThreadLocal<CudaDeviceVariable<cuFloatComplex>>(() => new CudaDeviceVariable<cuFloatComplex>(signalSize));

            for (int i = 0; i < maximumConcurrency; i++)
            {
                var thread = new Thread(new ThreadStart(CudaTaskRunner));

                if (!thread.IsAlive)
                {
                    thread.Start();
                }

                threads.Add(thread);
            }
        }

        public CudaContext CudaContext => _cudaContext.Value ?? throw new Exception("Cuda Context Null");
        public CudaFFTPlan1D CudaPlan => _cudaPlan.Value ?? throw new Exception("Cuda FFT Plan Null");
        public CudaDeviceVariable<cuFloatComplex> InputSignal => _inputSignal.Value ?? throw new Exception("Input Signal Null");
        public CudaDeviceVariable<cuFloatComplex> OutputSignal => _outputSignal.Value ?? throw new Exception("Output Signal Null");


        public void CudaTaskRunner()
        {
            using (CudaContext)
            using (CudaPlan)
            using (InputSignal)
            using (OutputSignal)
            {
                foreach (var task in taskQueue.GetConsumingEnumerable(cts.Token))
                {
                    TryExecuteTask(task);
                }
            }
        }

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            return taskQueue.AsEnumerable();
        }

        protected override void QueueTask(Task task)
        {
            taskQueue.Add(task);
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            return false;
        }

        public void Dispose()
        {
            cts.Cancel();
        }
    }
}
