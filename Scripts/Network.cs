using System;
using System.Net;
using System.Net.Sockets;
using Godot;

namespace Arcomage.Scripts
{
    // TODO: move GetLocalIpAddress() to Global.cs
    public partial class Network : Node
    {
        public static string IpAddress = "0.0.0.0";

        public override void _Ready()
        {
            IpAddress = GetLocalIpAddress();
        }

        private static string GetLocalIpAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                    return ip.ToString();
            
            Logger.Error("No network adapters with an IPv4 address in the system!");
            return "0.0.0.0";
        }
    }
}
