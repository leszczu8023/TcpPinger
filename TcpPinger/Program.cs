using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

/*********************************************
 * 
 *  Basic TCP Protocol Pinger application
 *  
 *              (c) 2017 Jan Leszczyński
 *               
 *********************************************/

namespace TcpPinger
{
    class Program
    {
        private static int Correct = 0;
        private static int Loss = 0;

        private static List<long> Avg = new List<long>();
        private static long Minimum = -1;
        private static long Maximum = -1;

        static void Main(string[] args)
        {
            Console.WriteLine("TCPPing v1.0, written by Jan Leszczyński @ leszczu8023.blogspot.com\n");

            if (args.Length == 0)
            {
                Console.WriteLine("For help, add -? argument.");
                return;
            }
            var ap = new ArgumentParser(args);

            if (ap.CheckArg("?"))
            {
                Console.WriteLine(Properties.Resources.Help + "\n");
                return;
            }

            try
            {
                var host = args[0];
                if (host == null || host.StartsWith("-")) throw new Exception("No host specified. Try -? argument.");

                var h2 = GetIpAddress(host);

                if (h2 == null)
                {
                    throw new Exception("Error: Cannot resolve host: " + h2);
                }

                var port = ap.GetArg("p");
                int nport;
                nport = port == null ? 80 : int.Parse(port);

                var count = ap.GetArg("n");
                int ncount;
                ncount = count == null ? 4 : int.Parse(count);

                var delay = ap.GetArg("d");
                int ndelay;
                ndelay = delay == null ? 1000 : int.Parse(delay);

                var pinger = new Pinger(h2, nport);

                Console.WriteLine("Sending TCP packets for host {0}:{1}{2}:\n", host, nport, host == h2 ? "" : " [" + h2 + ":" + nport + "]");


                pinger.PingReceived += (sender, eventArgs) =>
                {
                    if (eventArgs.IsDelivered)
                    {
                        Console.WriteLine("Request for host {0}:{1} - time: {2} milli-seconds",
                            eventArgs.Host, eventArgs.Port, eventArgs.Delay);
                        Correct++;

                        if (Minimum > eventArgs.Delay || Minimum == - 1)
                        {
                            Minimum = eventArgs.Delay;
                        }

                        if (Maximum < eventArgs.Delay || Maximum == -1)
                        {
                            Maximum = eventArgs.Delay;
                        }

                        Avg.Add(eventArgs.Delay);
                    }
                    else
                    {
                        Console.WriteLine("Request for host {0}:{1} - Connection refused",
                            eventArgs.Host, eventArgs.Port);
                        Loss++;
                    }
                    
                };

                Console.CancelKeyPress += (sender, eventArgs) =>
                {
                    PrintStats(h2, nport);
                };

                pinger.Execute(ncount, ndelay);
                PrintStats(h2, nport);
                
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);   
            }
        }

        static void PrintStats(string host, int port)
        {
            long c = 0;
            foreach (var a in Avg)
            {
                c += a;
            }

            c = c / Avg.Count;

            Console.WriteLine("\nTCP Ping statistics for [{0}:{1}]:\n     Packets: Send = {2}, Delivered = {3}, Lost = {4} ({5}% loss)\nApproximate round trip times in milli-seconds:\n     Minimum = {6}ms, Maximum = {7}ms, Average = {8}ms",
                host, port, Avg.Count, Correct, Loss, Loss * 100 / Avg.Count, Minimum, Maximum, c);
        }

        static string GetIpAddress(string input)
        {
            IPAddress address;
            if (IPAddress.TryParse(input, out address))
            {
                return input;
            }

            try
            {
                IPAddress[] addresslist = Dns.GetHostAddresses(input);

                foreach (IPAddress theaddress in addresslist)
                {
                    return theaddress.ToString();
                }
            }
            catch (Exception)
            {
                // just do nothing
            }
            return null;
        }
    }

    // Pinger Object
    class Pinger
    {
        public string Host { get; }
        public int Port { get; }

        public delegate void OnPingReceived(Pinger sender, PingerReceivedEventArgs args);

        public event OnPingReceived PingReceived;

        public Pinger(string host, int port)
        {
            Host = host;
            Port = port;
        }

        public void Execute(int n, int delay)
        {
            if (n == 0)
            {
                long i = 0;
                while (true)
                {
                    if (i != 0) Thread.Sleep(delay);
                    var watch = Stopwatch.StartNew();
                    var status = CheckStatus(Host, Port);
                    watch.Stop();
                    var elapsedMs = watch.ElapsedMilliseconds;

                    if (i != 0)
                        PingReceived?.Invoke(this,
                            new PingerReceivedEventArgs
                            {
                                Delay = elapsedMs,
                                Host = this.Host,
                                IsDelivered = status,
                                Port = this.Port
                            });
                    i++;
                }
            }
            else
            {
                for (int i = 0; i != n + 1; i++)
                {
                    if (i != 0) Thread.Sleep(delay);
                    var watch = Stopwatch.StartNew();
                    var status = CheckStatus(Host, Port);
                    watch.Stop();
                    var elapsedMs = watch.ElapsedMilliseconds;

                    if (i != 0) PingReceived?.Invoke(this, new PingerReceivedEventArgs { Delay = elapsedMs, Host = this.Host, IsDelivered = status, Port = this.Port });
                }
            }
        }

        private bool CheckStatus(string host, int port)
        {
            using (TcpClient tcpClient = new TcpClient())
            {
                try
                {
                    tcpClient.Connect(host, port);   
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }
    }

    class PingerReceivedEventArgs
    {
        public long Delay;
        public bool IsDelivered;
        public string Host;
        public int Port;
    }

    // Simple Argument Parser
    class ArgumentParser
    {
        public string[] Args { get; }

        public ArgumentParser(string[] arg)
        {
            Args = arg;
        }

        // Get argument value by name
        public string GetArg(string name)
        {
            int i = 0;
            foreach (var v in Args)
            {
                if (v == "-" + name)
                {
                    
                    if (Args.Length > i + 1)
                        if (!Args[i + 1].StartsWith("-")) return Args[i + 1];
                    throw new Exception("Error: Expected value for parameter \"-" + name + "\"");
                }
                i++;
            }
            return null;
        }

        //Check only argument existment, not argument value
        public bool CheckArg(string name)
        {
            return Args.Contains("-" + name);
        }
    }
}
