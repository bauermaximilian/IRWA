using System;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

#nullable enable

namespace IRWA
{
    public abstract class HttpServiceBase
    {
        public const int DefaultThreadPoolSize = 3;
        public const int ContextHandlerTerminationTimeout = 3000;
        public const int WebServerShutdownTimeout = 3000;

        private readonly int port;
        private readonly X509Certificate? httpsCertificate;
        private readonly ThreadPool contextHandlers;
        private Thread? listenerThread = null;
        private CancellationTokenSource? serverCts;

        protected HttpServiceBase(int httpPort, ThreadPool? threadPool = null)
        {
            port = httpPort;
            contextHandlers = threadPool
                ?? new ThreadPool(DefaultThreadPoolSize);
        }

        protected HttpServiceBase(int httpsPort, X509Certificate httpsCertificate,
            ThreadPool? threadPool = null) : this(httpsPort, threadPool)
        {
            this.httpsCertificate = httpsCertificate;
        }

        public void Start()
        {
            Stop();

            contextHandlers.Start();

            serverCts = new CancellationTokenSource();
            listenerThread = new Thread(RunListener);
            listenerThread.Start();
        }

        public void Stop()
        {
            serverCts?.Cancel();
            if (listenerThread != null)
            {
                if (!listenerThread.Join(WebServerShutdownTimeout))
                {
                    listenerThread.Abort();
                }
                listenerThread = null;
            }

            contextHandlers.Stop();
        }

        protected abstract void HandleClient(HttpListenerContext context);

        private void RunListener()
        {
            if (serverCts == null)
            {
                return;
            }

            var token = serverCts.Token;
            while (!token.IsCancellationRequested)
            {
                if (TryStartListener(out var listener))
                {
                    var listenerStoppedEvent = new AutoResetEvent(false);
                    var contextRetrievalThread = new Thread(() =>
                    {
                        GetAndHandleContexts(listener, token);
                        listenerStoppedEvent.Set();
                    });
                    contextRetrievalThread.Start();

                    WaitHandle.WaitAny(new WaitHandle[]
                    {
                        token.WaitHandle, listenerStoppedEvent
                    });

                    try
                    {
                        listener.Stop();
                    }
                    catch
                    {
                        // No specific error handling required.
                    }

                    if (!contextRetrievalThread.Join(WebServerShutdownTimeout))
                    {
                        contextRetrievalThread.Abort();
                    }
                }
            }
        }

        private bool TryStartListener(out HttpListener listener)
        {
            try
            {
                listener = new HttpListener(httpsCertificate != null ? 
                    "https" : "http", port);
                listener.HttpsCert = httpsCertificate;
                listener.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls11;
                listener.Start();
                if (listener.IsListening)
                {
                    return true;
                }
                else
                {
                    throw new Exception();
                }
            }
            catch
            {
                listener = null!;
                return false;
            }
        }

        private void GetAndHandleContexts(HttpListener listener,
            CancellationToken cancellationToken)
        {
            while (listener.IsListening &&
                !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var context = listener.GetContext();
                    if (context != null)
                    {
                        contextHandlers.RunAsync(() =>
                        {
                            try
                            {
                                HandleClient(context);
                                context.Response.Close();
                            }
                            catch
                            {
                                // No specific error handling required.
                            }
                            finally
                            {
                                context.Close();
                            }
                        });
                    }
                    else
                    {
                        break;
                    }
                }
                catch
                {
                    continue;
                }
            }
        }
    }
}
