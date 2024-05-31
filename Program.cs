using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Threading;

#nullable enable

namespace IRWA
{
    public class Program
    {
        public static void Main()
        {
            Debug.WriteLine("Waiting for Wifi network...");

            var address = WaitForNetwork(default, TimeSpan.FromSeconds(10));

            Debug.WriteLine($"Connection established as \"{address ?? "?"}\". Starting HTTP server...");

            IrSenderNec irSenderNec = new IrSenderNec(23);
            AppServer appServer = new AppServer(80, irSenderNec);
            appServer.Start();
            Debug.WriteLine("HTTP server started - system ready!");

            Thread.Sleep(Timeout.Infinite);
        }

        private static string? WaitForNetwork(CancellationToken token, TimeSpan timeout)
        {
            NetworkInterface networkInterface = NetworkInterface.GetAllNetworkInterfaces()[0];
            DateTime timeoutTime = default;
            if (timeout > TimeSpan.Zero)
            {
                timeoutTime = DateTime.UtcNow + timeout;
            }
            while (!token.IsCancellationRequested && 
                (DateTime.UtcNow < timeoutTime || timeoutTime == default))
            {
                if (networkInterface.IPv4Address != null &&
                    networkInterface.IPv4Address.Length > 0 &&
                    networkInterface.IPv4Address[0] != '0')
                {
                    return networkInterface.IPv4Address;
                }
                else
                {
                    token.WaitHandle.WaitOne(500, false);
                    networkInterface = NetworkInterface.GetAllNetworkInterfaces()[0];
                }
            }
            return null;
        }
    }
}
