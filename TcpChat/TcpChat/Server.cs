using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace TcpChat
{
    public class ClientObject
    {
        public const int BufferSize = 1024;
        public string Name = String.Empty;
        public byte[] buffer = new byte[BufferSize];
        public StringBuilder stringBuilder = new StringBuilder();
        public Socket workSocket = null;
        public ManualResetEvent progressEvent = new ManualResetEvent(false);
    }
    public class Server
    {
        private readonly string ip = "127.0.0.1";
        private readonly int port = 1234;
        private readonly Dictionary<Socket, string> _clients;
        private readonly List<ClientObject> clients;
        public static ManualResetEvent connectorEvent = new ManualResetEvent(false);

        public Server()
        {
            clients = new List<ClientObject>();
            _clients = new Dictionary<Socket, string>();
            IPEndPoint ipPoint = new IPEndPoint(IPAddress.Parse(ip), port);
            var listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listenSocket.Blocking = false;

            try
            {
                listenSocket.Bind(ipPoint);
                listenSocket.Listen(int.MaxValue);
                Console.WriteLine("The server is running. Waiting for connections...");

                while (true)
                {
                    connectorEvent.Reset();
                    listenSocket.BeginAccept(new AsyncCallback(AcceptCallback), listenSocket);
                    connectorEvent.WaitOne();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public void AcceptCallback(IAsyncResult ar)
        {
            connectorEvent.Set();

            Socket listener = (Socket)ar.AsyncState;
            Socket handler = listener.EndAccept(ar);

            ClientObject state = new ClientObject
            {
                workSocket = handler
            };
            state.workSocket.Blocking = false;

            clients.Add(state);
            Console.WriteLine("Connected clients: " + clients.Count);
            handler.BeginReceive(state.buffer, 0, ClientObject.BufferSize, 0,
                new AsyncCallback(ReadCallback), state);
        }

        public void ReadCallback(IAsyncResult ar)
        {

            String content = String.Empty;

            ClientObject state = (ClientObject)ar.AsyncState;
            Socket handler = state.workSocket;
            state.stringBuilder = new StringBuilder();
            int bytesRead = 0;

            try
            {
                bytesRead = handler.EndReceive(ar);
            }
            catch
            {
                clients.Remove(state);
                BroadcastMessage(handler, $"{state.Name} is disconnected");
                return;
            }

            if (bytesRead > 0)
            {
                int size = BitConverter.ToInt32(state.buffer.Take(4).ToArray());
                if (size == 0) return;
                byte[] data = state.buffer.Skip(4).ToArray();
                if (size < data.Length) state.stringBuilder.Append(Encoding.Unicode.GetString(data, 0, size));
                else state.stringBuilder.Append(Encoding.Unicode.GetString(data, 0, data.Length));

                content = state.stringBuilder.ToString();
                if (Encoding.Unicode.GetByteCount(content) == size)
                {
                    Console.WriteLine("Read {0} bytes from socket. \nData : {1}\n", content.Length, content);
                    if (String.IsNullOrEmpty(state.Name))
                    {
                        if (clients.Where(client => client.Name == content).Any())
                        {
                            var packet = MakePacket("This username is already taken. Please enter another username");
                            handler.BeginSend(packet, 0, packet.Length, 0, new AsyncCallback(SendCallback), handler);
                        }
                        else
                        {
                            state.Name = content;
                            BroadcastMessage(handler, $"{state.Name} is entered");
                        }
                    }
                    else BroadcastMessage(handler, $"[{state.Name}]: {content}");
                }
                else
                {
                    handler.BeginReceive(state.buffer, 0, ClientObject.BufferSize, 0,
                    new AsyncCallback(ReadCallback), state);
                }
            }

            handler.BeginReceive(state.buffer, 0, ClientObject.BufferSize, 0,
                new AsyncCallback(ReadCallback), state);
        }

        public byte[] MakePacket(string message)
        {
            byte[] bytesMessage = Encoding.Unicode.GetBytes(message);
            byte[] bytesSize = BitConverter.GetBytes(bytesMessage.Length);
            byte[] bytesDate = BitConverter.GetBytes(DateTimeOffset.Now.ToUnixTimeSeconds());
            byte[] packet = new byte[bytesMessage.Length + bytesSize.Length + bytesDate.Length];
            bytesSize.CopyTo(packet, 0);
            bytesDate.CopyTo(packet, bytesSize.Length);
            bytesMessage.CopyTo(packet, bytesSize.Length + bytesDate.Length);

            return packet;
        }

        public void BroadcastMessage(Socket fromSocket, string message)
        {
            Console.WriteLine("Sending data: " + message);
            var packet = MakePacket(message);

            foreach (ClientObject client in clients)
            {
                var handler = client.workSocket;
                if (fromSocket == handler) continue;

                handler.BeginSend(packet, 0, packet.Length, 0,
                    new AsyncCallback(SendCallback), handler);
            }
        }

        private static void SendCallback(IAsyncResult ar)
        {
            try
            {
                Socket handler = (Socket)ar.AsyncState;
                int bytesSent = handler.EndSend(ar);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}