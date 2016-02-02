namespace tcpServer
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.IO;

    public class TcpServer
    {
        private const string WORD_WHO = ":who";
        private const string WORD_QUIT = ":quit";
        private const string WORD_MEET = ":meet";
        private const string WORD_WHISPER = ":whisper";
        private const string WORD_START = "start";
        private const string WORD_P = "-p";
        private const string WORD_N = "-n";
        private const string WORD_M = "-m";

        private static Dictionary<string, TcpClient> clientsInServer = new Dictionary<string, TcpClient>();
        private static Dictionary<string, TcpClient> clientsToQuit = new Dictionary<string, TcpClient>();
        private static List<TcpClient> clientsWaitingToJoin = new List<TcpClient>();

        private static TcpListener tcpServer;

        private static StreamWriter streamWriter;
        private static StreamReader streamReader;

        private static bool someoneQuit = false;

        public static int MaxNumberConnections { get; set; }

        public static int PortNumber { get; set; }

        public static string WelcomeMessage { get; set; }

        public static void Main()
        {
            ConfigServer();

            while (true)
            {

                if (tcpServer.Pending())
                {
                    PendingClients(tcpServer);
                }

                if (clientsWaitingToJoin.Count > 0)
                {
                    IndetificationClients();
                }

                if (clientsInServer.Count > 0)
                {
                    TransferMessages();
                }

            }
        }
        /// <summary>
        /// Configurate tcp server and checking console commands
        /// </summary>
        public static void ConfigServer()
        {
            Console.Title = "TCP CHAT SERVER";

            Console.WriteLine("-= Hello, I'm TCP CHAT SERVER =-");
            Console.WriteLine();
            Console.WriteLine("-----------------------------------------------------------------------------------");
            Console.WriteLine("COMMANDS:");
            Console.WriteLine(" start - to turn on server");
            Console.WriteLine(" -p<port number> - to set a port number");
            Console.WriteLine(" -n<max connections> - to set a maximum number of connected clients");
            Console.WriteLine(" -m<welcome message> - to set a welcome message");
            Console.WriteLine();
            Console.WriteLine("Warning: Please use commands with triangular brackets for correct format!");
            Console.WriteLine("-----------------------------------------------------------------------------------");

            Console.Write("Please insert your command:");

            bool isServerStarted = false;
            int defaultPortNumber = 4567;
            int defaultMaxNumberConnections = 100;
            string defaultWelcomeMessage = "Hello";

            PortNumber = defaultPortNumber;
            MaxNumberConnections = defaultMaxNumberConnections;
            WelcomeMessage = defaultWelcomeMessage;

            while (!isServerStarted)
            {
                string clientInput = Console.ReadLine();

                if (clientInput.StartsWith(WORD_START))
                {
                    Console.WriteLine();
                    Console.WriteLine("SERVER IS STARTED!");
                    isServerStarted = true;
                }
                else
                {
                    if (clientInput.StartsWith(WORD_P))
                    {
                        try
                        {
                            PortNumber = int.Parse(ExtractingCommandValue(clientInput));
                        }
                        catch
                        {
                            Console.WriteLine("Wrong! You should use the correct format: -p<port number> with triangular brackets.");
                        }
                    }
                    else if (clientInput.StartsWith(WORD_N))
                    {
                        try
                        {
                            MaxNumberConnections = int.Parse(ExtractingCommandValue(clientInput));
                        }
                        catch
                        {
                            Console.WriteLine("Wrong! You should use the correct format: -n<maximum connections> with triangular brackets.");
                        }
                    }
                    else if (clientInput.StartsWith(WORD_M))
                    {
                        try
                        {
                            WelcomeMessage = ExtractingCommandValue(clientInput);
                        }
                        catch
                        {
                            Console.WriteLine("Wrong! You should use the correct format: -m<welcome message> with triangular brackets.");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Wrong command, please try again!");
                    }
                }
            }

            IPAddress ipAddress = IPAddress.Parse("127.0.0.1");
            tcpServer = new TcpListener(ipAddress, PortNumber);
            tcpServer.Start();
            Console.WriteLine("Listening port [{0}] is succesfully opened. Default maximum number of connected clients is [{1}]", PortNumber.ToString(), MaxNumberConnections.ToString());
        }
     
        public static void PendingClients(TcpListener tcpServer)
        {
            TcpClient currentClient = tcpServer.AcceptTcpClient();
            clientsWaitingToJoin.Add(currentClient);
            OpenSteam(currentClient);
            streamWriter.WriteLine("Welcome to the chat server");
            streamWriter.Flush();
        }
        /// <summary>
        /// Checking for indetification command from client. Add it in server.
        /// </summary>
        public static void IndetificationClients()
        {
            List<TcpClient> clientsForRemoveWaiting = new List<TcpClient>();

            foreach (var clientWaitingToJoin in clientsWaitingToJoin)
            {
                OpenSteam(clientWaitingToJoin);

                // With amountDataFromClient, server checking whether the client sends information.
                int amountDataFromClient = clientWaitingToJoin.Available;
                string clientInput = string.Empty;
                bool isCommand = false;

                if (amountDataFromClient > 0)
                {
                    clientInput = streamReader.ReadLine();
                    isCommand = clientInput.StartsWith(WORD_MEET);

                    if (isCommand)
                    {
                        try
                        {
                            string currentName = ExtractingCommandValue(clientInput);

                            if (clientsInServer.ContainsKey(currentName))
                            {
                                streamWriter.WriteLine("Problem! ?here is a client with name - {0}. Please try again with another name!", currentName);
                                streamWriter.Flush();
                            }
                            else
                            {
                                if (MaxNumberConnections > clientsInServer.Count)
                                {
                                    clientsInServer.Add(currentName, clientWaitingToJoin);
                                    clientsForRemoveWaiting.Add(clientWaitingToJoin);
                                    Console.WriteLine("{0} is connected!", currentName);
                                    streamWriter.WriteLine("{0}, {1}", WelcomeMessage, currentName);
                                    streamWriter.Flush();
                                }
                                else
                                {
                                    streamWriter.WriteLine("Server is full [{0}/{1}]! Try again later",clientsInServer.Count,MaxNumberConnections);
                                    streamWriter.Flush();
                                }
                            }
                        }
                        catch
                        {
                            streamWriter.WriteLine("Wrong! You should use the correct format - :meet<name> with triangular brackets.");
                            streamWriter.Flush();
                        }
                    }
                    else
                    {
                        streamWriter.WriteLine("You must use indentification command");
                        streamWriter.Flush();
                    }
                }
            }

            foreach (var client in clientsForRemoveWaiting)
            {
                clientsWaitingToJoin.Remove(client);
            }
        }
        /// <summary>
        /// Sending messages between clients.
        /// </summary>
        public static void TransferMessages()
        {
            foreach (var clientSender in clientsInServer)
            {
                OpenSteam(clientSender.Value);
                string clientInput = string.Empty;
                int amountData = clientSender.Value.Available;

                if (amountData > 0)
                {
                    clientInput = streamReader.ReadLine();
                    ProcessingClientCommands(clientInput, clientSender);
                }

                if (clientInput != string.Empty &&
                    clientInput != WORD_WHO &&
                    clientInput != WORD_QUIT &&
                    !clientInput.Contains(WORD_WHISPER))
                {
                    foreach (var clientReceiver in clientsInServer)
                    {
                        OpenSteam(clientReceiver.Value);

                        streamWriter.WriteLine("[{0}]{1} : {2}", Timer(), clientSender.Key, clientInput);
                        streamWriter.Flush();
                    }
                }
            }

            if (someoneQuit)
            {

                foreach (var client in clientsToQuit)
                {
                    Console.WriteLine("{0} is disconnected!", client.Key);
                    client.Value.Close();
                    clientsInServer.Remove(client.Key);
                }
                someoneQuit = false;
            }

        }
        /// <summary>
        /// Processing of client commands - who, whisper and quit.
        /// </summary>
        /// <param name="clientInput"></param>
        /// <param name="currentClient"></param>
        public static void ProcessingClientCommands(string clientInput, KeyValuePair<string, TcpClient> currentClient)
        {
            if (clientInput.StartsWith(WORD_WHO))
            {
                string allConnectedClients = string.Join(",", clientsInServer.Keys);
                streamWriter.WriteLine("All connected clients - {0}", allConnectedClients);
                streamWriter.Flush();
            }
            else if (clientInput.StartsWith(WORD_QUIT))
            {
                someoneQuit = true;
                clientsToQuit.Add(currentClient.Key, currentClient.Value);
            }
            else if (clientInput.StartsWith(WORD_WHISPER))
            {
                try
                {
                    string name = ExtractingCommandValue(clientInput);

                    int startIndexMessage = clientInput.LastIndexOf("<") + "<".Length;
                    int messageLength = clientInput.LastIndexOf(">") - startIndexMessage;
                    string message = clientInput.Substring(startIndexMessage, messageLength);

                    OpenSteam(clientsInServer[name]);
                    streamWriter.WriteLine("[{0}](Private){1} : {2}", Timer(), currentClient.Key, message);
                    streamWriter.Flush();
                }
                catch
                {
                    streamWriter.WriteLine("Wrong name of specified client or format - :whisper<client><message> with brackets!");
                    streamWriter.Flush();

                }
            }
        }
        /// <summary>
        /// Extracting value from console command.
        /// </summary>
        public static string ExtractingCommandValue(string clientInput)
        {
            int startIndexValue = clientInput.IndexOf('<') + "<".Length;
            int valueLength = clientInput.IndexOf(">");
             string commandValue = clientInput.Substring(startIndexValue, valueLength - startIndexValue);
             return commandValue;
        }
        /// <summary>
        /// Open stream between server and current client.
        /// </summary>
        /// <param name="currentClient"></param>
        public static void OpenSteam(TcpClient currentClient)
        {
            NetworkStream stream = currentClient.GetStream();
            streamWriter = new StreamWriter(stream);
            streamReader = new StreamReader(stream);
        }
        /// <summary>
        /// Timer is using for prefix name in chat.
        /// </summary>
        /// <returns></returns>
        public static string Timer()
        {
            DateTime dateTime = DateTime.Now;
            var hours = dateTime.Hour.ToString();
            var minutes = dateTime.Minute.ToString();
            var seconds = dateTime.Second.ToString();
            StringBuilder time = new StringBuilder();
            time.Append(hours + ":" + minutes + ":" + seconds);
            return time.ToString();
        }
    }
}
