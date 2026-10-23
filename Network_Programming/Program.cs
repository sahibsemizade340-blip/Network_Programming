using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace Network_Programming
{ 

    public class User
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public bool IsOnline { get; set; }
        public string RemoteEndpoint { get; set; }
    }

    class Program
    {
        private static List<User> users = new List<User>();
        private static readonly object lockObj = new object();
        private static List<TcpClient> connectedClients = new List<TcpClient>();

        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            LoadUsers();

            IPAddress ip = IPAddress.Parse("192.168.0.102");
            int port = 27001;
            TcpListener server = new TcpListener(ip, port);
            server.Start();

            Console.ForegroundColor = ConsoleColor.Green;
            WriteLine($"[SERVER] İşə düşdü: {ip}:{port}");
            Console.ResetColor();

            while (true)
            {
                TcpClient client = server.AcceptTcpClient();
                lock (lockObj)
                {
                    connectedClients.Add(client);
                }
                Thread clientThread = new Thread(() => HandleClient(client));
                clientThread.Start();
            }
        }

        private static void LoadUsers()
        {
            if (File.Exists("users.json"))
            {
                string json = File.ReadAllText("users.json");
                users = JsonSerializer.Deserialize<List<User>>(json) ?? new List<User>();
                foreach (var u in users)
                {
                    u.IsOnline = false;
                    u.RemoteEndpoint = "";
                }
                SaveUsers();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                WriteLine("WARNING users.json faylı tapılmadı!");
                Console.ResetColor();
            }
        }

        private static void SaveUsers()
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(users, options);
            File.WriteAllText("users.json", json);
        }

        private static void HandleClient(TcpClient client)
        {
            NetworkStream stream = client.GetStream();
            string currentUsername = null;
            string clientEndPoint = client.Client.RemoteEndPoint.ToString();

            try
            {
                byte[] buffer = new byte[1024];

                while (true)
                {
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;

                    string request = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

                    if (request.StartsWith("LOGIN:"))
                    {
                        string[] parts = request.Split(':');
                        if (parts.Length >= 3)
                        {
                            string username = parts[1];
                            string password = parts[2];

                            lock (lockObj)
                            {
                                var user = users.Find(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

                                if (user == null || user.Password != password)
                                {
                                    byte[] resp = Encoding.UTF8.GetBytes("LOGIN_FAIL:Yanlış istifadəçi adı və ya şifrə!");
                                    stream.Write(resp, 0, resp.Length);
                                }
                                else if (user.IsOnline)
                                {
                                    byte[] resp = Encoding.UTF8.GetBytes($"LOGIN_FAIL:Bu istifadəçi artıq {user.RemoteEndpoint} ünvanından onlayndır!");
                                    stream.Write(resp, 0, resp.Length);
                                }
                                else
                                {
                                    user.IsOnline = true;
                                    user.RemoteEndpoint = clientEndPoint;
                                    currentUsername = user.Username;
                                    SaveUsers();

                                    byte[] resp = Encoding.UTF8.GetBytes("LOGIN_SUCCESS");
                                    stream.Write(resp, 0, resp.Length);

                                    Console.ForegroundColor = ConsoleColor.Cyan;
                                    WriteLine($"[GİRİŞ] {currentUsername} qoşuldu ({clientEndPoint})");
                                    Console.ResetColor();

                                    BroadcastUserList();
                                }
                            }
                        }
                    }
                    else if (request == "EXIT")
                    {
                        break;
                    }
                }
            }
            catch
            {
            }
            finally
            {
                if (currentUsername != null)
                {
                    lock (lockObj)
                    {
                        var user = users.Find(u => u.Username == currentUsername);
                        if (user != null)
                        {
                            user.IsOnline = false;
                            user.RemoteEndpoint = "";
                            SaveUsers();
                        }
                    }

                    Console.ForegroundColor = ConsoleColor.Yellow;
                    WriteLine($"[ÇIXIŞ] {currentUsername} sistemdən ayrıldı.");
                    Console.ResetColor();

                    BroadcastUserList();
                }

                lock (lockObj)
                {
                    connectedClients.Remove(client);
                }
                client.Close();
            }
        }

        private static void BroadcastUserList()
        {
            string jsonList = JsonSerializer.Serialize(users);
            byte[] data = Encoding.UTF8.GetBytes("USERLIST:" + jsonList);

            lock (lockObj)
            {
                foreach (var c in connectedClients)
                {
                    try
                    {
                        if (c.Connected)
                        {
                            c.GetStream().Write(data, 0, data.Length);
                        }
                    }
                    catch { }
                }
            }

            Console.ForegroundColor = ConsoleColor.White;
            WriteLine("\n--- CARİ İSTİFADƏÇİ SİYAHISI ---");
            foreach (var u in users)
            {
                string status = u.IsOnline ? "Onlayn" : "Oflayn";
                WriteLine($"İstifadəçi: {u.Username} | Status: {status} | IP: {(string.IsNullOrEmpty(u.RemoteEndpoint) ? "-" : u.RemoteEndpoint)}");
            }
            WriteLine("---------------------------------\n");
            Console.ResetColor();
        }

        private static void WriteLine(string text)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {text}");
        }
    }
}