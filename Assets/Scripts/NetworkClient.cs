using UnityEngine;
using Unity.Collections;
using Unity.Networking.Transport;
using NetworkMessages;
using NetworkObjects;
using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
public class NetworkClient : MonoBehaviour
{
    //only to know where to send and where to get from
    public NetworkDriver m_Driver;
    public NetworkConnection m_Connection;
    public string serverIP;
    public ushort serverPort;
    //
    public NativeList<NetworkConnection> m_Connections;
    public List<NetworkObjects.NetworkPlayer> currentPlayers;
    public List<NetworkObjects.NetworkPlayer> newPlayers;
    public List<NetworkObjects.NetworkPlayer> lostPlayers;

    public GameObject playerPrefab;//player physical look
    public NetworkObjects.NetworkPlayer myPlayer;//player in general



    void Start()
    {
        m_Driver = NetworkDriver.Create();
        m_Connection = default(NetworkConnection);
        var endpoint = NetworkEndPoint.Parse(serverIP, serverPort);
        m_Connection = m_Driver.Connect(endpoint);
        m_Connections = new NativeList<NetworkConnection>(16, Allocator.Persistent);
        RequestMyID();
        //tell aoubt yourself in order to get your own id
        //InvokeRepeating() - goint to be constantly sending handshakes
        //InvokeRepeating() - going to be constantly sending its current position
        InvokeRepeating("SendRepeatedHandshake", 0.0f, 1.0f);
    }




    void SendRepeatedHandshake()
    {
            HandshakeMsg m = new HandshakeMsg();
            m.player.id = myPlayer.id;
            SendToServer(JsonUtility.ToJson(m));
            Debug.Log("(Client: " + m.player.id.ToString() + ") Sending a handshake");
        
    }


    void Update()
    {
        m_Driver.ScheduleUpdate().Complete(); //it's just doing its job, no matter what it is
        if (!m_Connection.IsCreated)
        {
            return;
        }

        //update current player (OnData)

        DataStreamReader stream;
        NetworkEvent.Type cmd;
        cmd = m_Connection.PopEvent(m_Driver, out stream);
        while (cmd != NetworkEvent.Type.Empty)
        {

            if (cmd == NetworkEvent.Type.Data)
            {
                OnData(stream);
            }
            cmd = m_Connection.PopEvent(m_Driver, out stream);
        }

    }


        public void RequestMyID()//asks the 
        {
            IDUpdateMsg m = new IDUpdateMsg();
            m.id = 0;
            SendToServer(JsonUtility.ToJson(m));
        }



        public void PlayerPositionUpdate() //send its current position to the server
        {
            PlayerUpdateMsg m = new PlayerUpdateMsg();
            SendToServer(JsonUtility.ToJson(m));
        }

        public void OnConnect(int i) //spawns players
        {
            currentPlayers[i].body = Instantiate(playerPrefab, new Vector3(0, 0, 0), Quaternion.identity);
            currentPlayers[i].id = i;
        }

        public void OnDisconnect(int i) //destroys players
        {
            Destroy(currentPlayers[i].body);
            currentPlayers.Remove(currentPlayers[i]);
            //m_Connection.Disconnect(m_Driver);
            //m_Connection = default(NetworkConnection);
        }

        void OnData(DataStreamReader stream)
        {
        //these guys are converting unreadable data to readable and the final version will be called recMsg
        NativeArray<byte> bytes = new NativeArray<byte>(stream.Length, Allocator.Temp);
        stream.ReadBytes(bytes);
        string recMsg = Encoding.ASCII.GetString(bytes.ToArray());
        NetworkHeader header = JsonUtility.FromJson<NetworkHeader>(recMsg);
        //
               switch(header.cmd)
               {
                   case Commands.ID_UPDATE:
               
                        IDUpdateMsg idMsg = JsonUtility.FromJson<IDUpdateMsg>(recMsg);
                        myPlayer.id = idMsg.id;
                        Debug.Log("Client: Server returned me my new id: " + myPlayer.id.ToString());
                        break;

                        case Commands.HANDSHAKE:
                        HandshakeMsg m = JsonUtility.FromJson<HandshakeMsg>(recMsg);
                        Debug.Log("(Server) Hello, player " + myPlayer.id.ToString());
                        break;
               
                        default:
                        Debug.Log("Unrecognized message received!");
                        break;
               }




        }




        //send to server knows where to send
        void SendToServer(string message)//converts all the readable data to the unreadable
        {
            var writer = m_Driver.BeginSend(m_Connection);
            NativeArray<byte> bytes = new NativeArray<byte>(Encoding.ASCII.GetBytes(message), Allocator.Temp);
            writer.WriteBytes(bytes);
            m_Driver.EndSend(writer);
        }
    

}