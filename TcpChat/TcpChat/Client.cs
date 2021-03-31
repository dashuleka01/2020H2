using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace TcpChat
{
    public class Client
    {
        private readonly string ip = "127.0.0.1";
        private readonly int port = 1234;
        private readonly Socket _socket;
        private readonly IPEndPoint ipPoint;

        public Client()
        {
            try
            {
                ipPoint = new IPEndPoint(IPAddress.Parse(ip), port);
                _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _socket.Connect(ipPoint);
                _socket.Blocking = false;


                Console.WriteLine(_socket.LocalEndPoint);

                Console.WriteLine("Enter your username: ");
                SendMessage();

                var thread = new Thread(() => GetMessages());
                thread.Start();

                while (true)
                {
                    SendMessage();
                }
            }
            catch (Exception ex)
            {
                _socket.Shutdown(SocketShutdown.Both);
                _socket.Close();
                Console.WriteLine(ex.GetBaseException());
            }
        }

        public void GetMessages()
        {
            while (true)
            {
                try
                {
                    GetMessage();
                }
                catch (SocketException ex)
                {
                    if (ex.SocketErrorCode == SocketError.WouldBlock ||
                        ex.SocketErrorCode == SocketError.TryAgain) continue;
                    Console.WriteLine("Oops, the server was down...");
                    Environment.Exit(0);

                }
            }
        }

        public void SendMessage()
        {
            byte[] data = Encoding.Unicode.GetBytes($"{Console.ReadLine()}");
            byte[] dataSize = BitConverter.GetBytes(data.Length);
            byte[] packet = new byte[data.Length + dataSize.Length];
            dataSize.CopyTo(packet, 0);
            data.CopyTo(packet, dataSize.Length);
            _socket.Send(packet);
        }

        public void GetMessage()
        {
            StringBuilder builder = new StringBuilder();
            byte[] data = new byte[8];

            byte[] bytesSize = new byte[4];
            _socket.Receive(bytesSize);
            int size = BitConverter.ToInt32(bytesSize);

            byte[] bytesDate = new byte[8];
            _socket.Receive(bytesDate);
            var date = DateTimeOffset.FromUnixTimeSeconds(BitConverter.ToInt64(bytesDate)).LocalDateTime;

            do
            {
                int bytes = _socket.Receive(data);
                builder.Append(Encoding.Unicode.GetString(data, 0, bytes));
            }
            while (_socket.Available > 0 && _socket.Available < size);

            if (size > 0) Console.WriteLine($"<{date}> {builder}");
        }
    }
}
