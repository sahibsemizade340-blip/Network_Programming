using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace Client
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
        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            TcpClient client = new TcpClient();

            try
            {
                client.Connect("192.168.0.102", 27001);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Serverə qoşulma uğurludur!");
                Console.ResetColor();

                NetworkStream stream = client.GetStream();

                Thread listenerThread = new Thread(() => ListenServer(stream));
                listenerThread.Start();

                bool isLoggedin = false;

                while (!isLoggedin)
                {
                    Console.WriteLine("\n--- GİRİŞ PANELİ ---");
                    Console.Write("İstifadəçi adı (Username): ");
                    string username = Console.ReadLine();
                    Console.Write("Şifrə (Password): ");
                    string password = Console.ReadLine();

                    string request = $"LOGIN:{username}:{password}";
                    byte[] data = Encoding.UTF8.GetBytes(request);
                    stream.Write(data, 0, data.Length);

                    byte[] buffer = new byte[1024];
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    string response = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    if (response == "LOGIN_SUCCESS")
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("\nGiriş uğurludur! Sistemə daxil oldunuz.");
                        Console.ResetColor();
                        isLoggedin = true;
                    }
                    else if (response.StartsWith("LOGIN_FAIL:"))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("\nXəta: " + response.Substring(11));
                        Console.ResetColor();
                    }
                }

                Console.WriteLine("\nÇıxış etmək üçün 'Exit' yazın.");
                while (true)
                {
                    string input = Console.ReadLine();
                    if (input?.Trim().Equals("Exit", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        byte[] exitData = Encoding.UTF8.GetBytes("EXIT");
                        stream.Write(exitData, 0, exitData.Length);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Bağlantı xətası: " + ex.Message);
                Console.ResetColor();
            }
            finally
            {
                client.Close();
                Console.WriteLine("Proqram bağlandı.");
            }
        }

        private static void ListenServer(NetworkStream stream)
        {
            try
            {
                byte[] buffer = new byte[4096];
                while (true)
                {
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;

                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    if (message.StartsWith("USERLIST:"))
                    {
                        string jsonList = message.Substring(9);
                        var users = JsonSerializer.Deserialize<List<User>>(jsonList);

                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("\n\n--- AKTİV İSTİFADƏÇİLƏR SİYAHISI ---");
                        if (users != null)
                        {
                            foreach (var u in users)
                            {
                                string status = u.IsOnline ? "Onlayn [🟢]" : "Oflayn [🔴]";
                                Console.WriteLine($"- {u.Username} | Status: {status} | IP: {(string.IsNullOrEmpty(u.RemoteEndpoint) ? "Yoxdur" : u.RemoteEndpoint)}");
                            }
                        }
                        Console.WriteLine("--------------------------------------\n");
                        Console.ResetColor();
                    }
                }
            }
            catch
            {
            }
        }
    }
}