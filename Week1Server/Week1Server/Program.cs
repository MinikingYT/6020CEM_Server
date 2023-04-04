using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Diagnostics;

namespace Week1Server
{
    internal class Program
    {
        static Dictionary<int, EndPoint> gameObjectOwners = new Dictionary<int, EndPoint>();
        static Dictionary<EndPoint, DateTime> clientHeartbeats = new Dictionary<EndPoint, DateTime>();
        static Dictionary<int, byte[]> gameState = new Dictionary<int, byte[]>(); //initialise this at the start of the program
        static Socket newsock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp); //make a socket using UDP. The parameters passed are enums used by the constructor of Socket to configure the socket.
        static IPEndPoint[] sender = new IPEndPoint[30];
        static List<EndPoint> Remote = new List<EndPoint>();
        static int lastAssignedGlobalID = 1; //I arbitrarily start at 12 so it’s easy to see if it’s working 
        static Dictionary<EndPoint, float> clientHealths = new Dictionary<EndPoint, float>();




        static void Main(string[] args)
        {

            //starting code that should only be run on the start of the server in this funtion
            initializeServer();


            //all loops are separated in different threads so they can run simultaneosly
           // Thread thr1 = new Thread(SendData);
            Thread thr2 = new Thread(KeyCheker);
            Thread thr3 = new Thread(ReceiveData);
            Thread thr4 = new Thread(checkConnections);
            Thread thr5 = new Thread(CheckDisconnectedClients);
            

            //thr1.Start();
            thr2.Start();
            thr3.Start();
            thr4.Start();
            thr5.Start();
        }

        static void initializeServer()
        {
            //task 1
            //10.1.162.32
            IPEndPoint ipep = new IPEndPoint(IPAddress.Parse("25.13.185.212"), 9050); //our server IP. This is set to local (127.0.0.1) on socket 9050. If 9050 is firewalled, you might want to try another!


            newsock.Bind(ipep); //bind the socket to our given IP
            Console.WriteLine("Socket open..."); //if we made it this far without any networking errors, it’s a good start!
        }

        static private void SendData()
        {
            byte[] data = new byte[1024];



            //while (true)
            //{

            //loops through all remots (all clients)
            for (int i = 0; i < Remote.Count; i++)
            {

               


                //if client exists send information
                if (Remote[i] != null)
                {

                    foreach (KeyValuePair<int, byte[]> kvp in gameState.ToList())
                    {
                        newsock.SendTo(kvp.Value, kvp.Value.Length, SocketFlags.None, Remote[i]);
                    }

                }

            }
        }

        static private void ReceiveData()
        {


            byte[] data = new byte[1024]; // the (expected) packet size. Powers of 2 are good. Typically for a game we want small, optimised packets travelling fast. The 1024 bytes chosen here is arbitrary – you should adjust it.
            int recv;

            //task 2

            //while the loop keeps going (server is running)
            while (true)
            {
               //new connection
                    EndPoint newRemote = new IPEndPoint(IPAddress.Any, 0);

                //receive data
                    data = new byte[1024];
                    recv = newsock.ReceiveFrom(data, ref newRemote);
                
                //here we receive the data in ascii and transfor it to string
                string text = Encoding.ASCII.GetString(data, 0, recv); 
               
                //different if checks for different messages, so we can process the different requests from the clients, example: FirstEntrance is called the first time player joins the server
                if(text == "FirstEntrance") {
                    //we store a message to send to the client 
                    string hi = "Yep, you just connected!";
                    Console.WriteLine("New connection with the ip " + newRemote.ToString());
                    //remember we need to convert anything to bytes to send it
                    data = Encoding.ASCII.GetBytes(hi);
                    //we send the information to the client, so that the client knows that he just connected
                    newsock.SendTo(data, data.Length, SocketFlags.None, newRemote);
                    
                    //pos = pos + 1; // read through all remote.lenght and if null remove
                    Remote.Add(newRemote);

                }
                else if (text.Contains("I need a UID for local object:"))
                {

                    Console.WriteLine(text.Substring(text.IndexOf(':')));

                    //parse the string into an into to get the local ID
                    int localObjectNumber = Int32.Parse(text.Substring(text.IndexOf(':') + 1));
                    //assign the ID
                    string returnVal = ("Assigned UID:" + localObjectNumber + ";" + lastAssignedGlobalID++);
                    Console.WriteLine(returnVal);
                    //playerInfo = "Pinged from server";
                    newsock.SendTo(Encoding.ASCII.GetBytes(returnVal), Encoding.ASCII.GetBytes(returnVal).Length, SocketFlags.None, newRemote);
                    gameObjectOwners[lastAssignedGlobalID - 1] = newRemote; //here we populate the dictionary to remove Objects with UIDS
                    //pos = pos + 1; // read through all remote.lenght and if null remove
                    //Remote[pos] = newRemote;

                }
                
                else if (text.Contains("Object data;"))
                {
                    //get the global id from the packet
                   // Console.WriteLine(text);
                    string globalId = text.Split(";")[1];
                    int intId = Int32.Parse(globalId);
                    if (gameState.ContainsKey(intId))
                    { //if true, we're already tracking the object
                        gameState[intId] = data; //data being the original bytes of the packet
                    }
                    else //the object is new to the game
                    {
                        gameState.Add(intId, data);
                    }
                }

                else if (text.Contains("Heartbeat"))
                {
                    //samething as objectdata, but for heartbeats
                    if (clientHeartbeats.ContainsKey(newRemote))
                    {
                        clientHeartbeats[newRemote] = DateTime.UtcNow;
                    }
                    else
                    {
                        clientHeartbeats.Add(newRemote, DateTime.UtcNow);
                    }
                }
                else if (text.Contains("causeDamage:"))
                {
                    Console.WriteLine(text);
                    //the damage received from the server
                    float damageTaken = float.Parse(text.Split(';')[0].Substring(text.IndexOf(':') + 1).Trim());
                    int globalID = int.Parse(text.Split(';')[1].Trim());

                    //client who will take the damage
                    EndPoint damagedPlayerEndPoint = FindEndPointByGlobalId(globalID);

                    //if client exists
                    if (damagedPlayerEndPoint != null)
                    {
                        float startingHP = 100.0f;

                        if (clientHealths.ContainsKey(damagedPlayerEndPoint))
                        {
                            // If hp value already existed
                            clientHealths[damagedPlayerEndPoint] = clientHealths[damagedPlayerEndPoint] - damageTaken;
                        }
                        else
                        {
                            // If client didn't have hp yet
                            clientHealths.Add(damagedPlayerEndPoint, startingHP);
                            clientHealths[damagedPlayerEndPoint] = clientHealths[damagedPlayerEndPoint] - damageTaken;
                        }

                        //check for cheats on hp
                        CheckHPCheat(clientHealths[damagedPlayerEndPoint], startingHP, damagedPlayerEndPoint);


                        //send to client new update
                        string healthUpdateConfirmation = ("HealthUpdate: " + clientHealths[damagedPlayerEndPoint]);
                        newsock.SendTo(Encoding.ASCII.GetBytes(healthUpdateConfirmation), Encoding.ASCII.GetBytes(healthUpdateConfirmation).Length, SocketFlags.None, damagedPlayerEndPoint);
                    }
                    else
                    {
                        Console.WriteLine("Unable to find EndPoint for globalID: " + globalID);
                    }


                }//after recieving all data, send the new data back
                    SendData();


            }
        }


        //when called checks if the current player hp is bigger then the starting hp, if it is, it disconnects the player
        static void CheckHPCheat(float hp, float startingHP, EndPoint client)
        {
            if (hp > startingHP){
                 Console.WriteLine("Client WAS CHEATING: " + client.ToString());
                 Remote.Remove(client);
                 RemoveGameObjectsAssociatedWithClient(client);
                 clientHeartbeats.Remove(client);
            }

        }

        //checks if esc is pressed and closes the server
        static private void KeyCheker()
        {

            while (true)
            {
                if (Console.ReadKey().Key == ConsoleKey.Escape)
                {
                    Environment.Exit(0);
                    return;
                }
            }
        }


        //gets a int that represents a global id and if it is inside the dictionary where all gameObjects are, returns it
        static private EndPoint FindEndPointByGlobalId(int globalID)
        {
            if (gameObjectOwners.ContainsKey(globalID))
            {
                return gameObjectOwners[globalID];
            }

            return null;
        }


        //code that says how many players are connected from time to time
        static private void checkConnections()
        {
            int playerNumber = 0;
            while (true)
            {
                for (int i = 0; i< Remote.Count; i++)
                {
                    if (Remote[i] != null)
                        playerNumber++;
                }
                Console.WriteLine("Players Connected: " + playerNumber);
                playerNumber = 0;

                Thread.Sleep(5000);
            }

        }


        //handles disconnecting players
        static private void CheckDisconnectedClients()
        {
            while (true)
            {
                //gets the time right now
                DateTime currentTime = DateTime.UtcNow;

                //loops through all clients
                foreach (EndPoint client in Remote.ToArray())
                {
                    //if the last heartbeat message sent by the client was sent over 5 seconds ago, the player disconncts
                    if (clientHeartbeats.ContainsKey(client) && (currentTime - clientHeartbeats[client]).TotalSeconds > 5)
                    {
                        Console.WriteLine("Client disconnected: " + client.ToString());
                        Remote.Remove(client);
                        RemoveGameObjectsAssociatedWithClient(client);
                        clientHeartbeats.Remove(client);
                    }
                }
                Thread.Sleep(5000);
            }
        }


        //further remove client info from dictionaries when heartbeat is not sent
        static private void RemoveGameObjectsAssociatedWithClient(EndPoint client)
        {

            List<int> gameObjectsToRemove = new List<int>();

            //finds the game object associated with the disconnected client
            foreach (var kvp in gameObjectOwners)
            {
                if (kvp.Value.Equals(client))
                {
                    gameObjectsToRemove.Add(kvp.Key);
                }
            }

            //removes the game object
            foreach (int gameObjectId in gameObjectsToRemove)
            {
                gameState.Remove(gameObjectId);
                gameObjectOwners.Remove(gameObjectId);
            }
        }

    }
}