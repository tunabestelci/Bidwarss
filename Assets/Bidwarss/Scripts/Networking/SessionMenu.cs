using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bidwarss
{
    public sealed class SessionMenu : MonoBehaviour
    {
        [Serializable] sealed class JoinData { public int protocol=2; public string name; public string rules; }
        public static SessionMenu Instance { get; private set; }
        public static int RequestedSeed { get; private set; }
        public NetworkManager network;
        public WarehouseWorld sceneWorld;
        public Camera lobbyCamera;
        public string Address="127.0.0.1",DisplayName="Oyuncu",SeedText="";
        public string Status {get;private set;}="Depo seni bekliyor.";
        public bool Daily;
        public bool Restarting {get;private set;}
        readonly Dictionary<ulong,int> seats=new Dictionary<ulong,int>();
        readonly Dictionary<ulong,string> names=new Dictionary<ulong,string>();
        const ushort Port=7777;
        static string lastStatus;
        void Awake()
        {
            Instance=this;Application.runInBackground=true;
            DisplayName=PlayerPrefs.GetString("Bidwarss.Name","Oyuncu");
            if(!string.IsNullOrEmpty(lastStatus)){Status=lastStatus;lastStatus=null;}
            network.NetworkConfig.ConnectionApproval=true;network.NetworkConfig.ProtocolVersion=2;
            network.ConnectionApprovalCallback=Approve;
            network.OnClientDisconnectCallback+=Disconnected;network.OnClientStopped+=Stopped;network.OnTransportFailure+=TransportFailed;
        }
        void Start()
        {
            if(Application.isBatchMode && Array.IndexOf(Environment.GetCommandLineArgs(),"-bidwarssServer")>=0)
            {
                RequestedSeed=FreshSeed();
                var transport=network.GetComponent<UnityTransport>();transport.SetConnectionData("127.0.0.1",Port,"0.0.0.0");
                sceneWorld.Rules.Validate();network.StartServer();
            }
        }
        public static int FreshSeed() => BitConverter.ToInt32(Guid.NewGuid().ToByteArray(),0);
        public string NameFor(ulong client)=>names.TryGetValue(client,out var value)?value:"Oyuncu";
        public static string CleanName(string value)
        {
            if(string.IsNullOrWhiteSpace(value))return "Oyuncu";
            var result=new StringBuilder();
            foreach(char c in value.Trim())if(!char.IsControl(c)&&c!='<'&&c!='>'&&c!='/'&&c!='\\'&&c!='|'&&result.Length<16)result.Append(c);
            return result.Length>0?result.ToString():"Oyuncu";
        }
        void Approve(NetworkManager.ConnectionApprovalRequest request,NetworkManager.ConnectionApprovalResponse response)
        {
            response.Pending=false;response.Approved=false;response.CreatePlayerObject=false;
            try
            {
                if(request.Payload==null || request.Payload.Length>1024)throw new Exception();
                var data=JsonUtility.FromJson<JoinData>(Encoding.UTF8.GetString(request.Payload));
                if(data==null || data.protocol!=2 || data.rules!=sceneWorld.Rules.Fingerprint())
                {response.Reason="Oyun sürümü veya eşya kataloğu farklı. Aynı build ile bağlan.";return;}
                int seat=-1;for(int i=0;i<4;i++)if(!seats.ContainsValue(i)){seat=i;break;}
                if(seat<0){response.Reason="Oda dolu: en fazla 4 oyuncu.";return;}
                seats[request.ClientNetworkId]=seat;names[request.ClientNetworkId]=CleanName(data.name);
                response.Approved=true;response.CreatePlayerObject=true;
                response.Position=new Vector3(-2.4f+seat*1.6f,.1f,-11);response.Rotation=Quaternion.identity;
            }
            catch(Exception){response.Reason="Bağlantı bilgisi okunamadı.";}
        }
        void Disconnected(ulong client)
        {
            seats.Remove(client);names.Remove(client);
            if(client==network.LocalClientId && !string.IsNullOrEmpty(network.DisconnectReason))lastStatus=network.DisconnectReason;
        }
        void TransportFailed(){Status="Bağlantı kurulamadı; adres ve UDP 7777 erişimini kontrol et.";lastStatus=Status;}
        void Stopped(bool wasHost){if(!Restarting && gameObject.activeInHierarchy)StartCoroutine(ReturnToMenu());}
        IEnumerator ReturnToMenu()
        {
            Restarting=true;WarehousePlayer.LockCursor(false);
            while(network!=null&&network.ShutdownInProgress)yield return null;
            if(network!=null)Destroy(network.gameObject);
            yield return null;SceneManager.LoadScene(SceneManager.GetActiveScene().path);
        }
        void Update(){if(lobbyCamera!=null)lobbyCamera.gameObject.SetActive(WarehousePlayer.Local==null&&!Application.isBatchMode);}
        void OnDestroy()
        {
            if(Instance==this)Instance=null;
            if(network==null)return;
            network.OnClientDisconnectCallback-=Disconnected;network.OnClientStopped-=Stopped;network.OnTransportFailure-=TransportFailed;
        }
        public void StartSession(bool host)
        {
            if(network.IsListening||Restarting)return;
            if(!IPAddress.TryParse(Address,out var ip)||ip.AddressFamily!=System.Net.Sockets.AddressFamily.InterNetwork)
            {Status="Geçerli IPv4 adresi yaz. Örnek: 192.168.1.10";return;}
            try {sceneWorld.Rules.Validate();}
            catch(Exception ex){Status=ex.Message;return;}
            if(Daily)RequestedSeed=int.Parse(DateTime.UtcNow.ToString("yyyyMMdd"));
            else if(string.IsNullOrWhiteSpace(SeedText))RequestedSeed=FreshSeed();
            else if(int.TryParse(SeedText,out int seed))RequestedSeed=seed;
            else {Status="Seed bir tam sayı olmalı.";return;}
            DisplayName=CleanName(DisplayName);PlayerPrefs.SetString("Bidwarss.Name",DisplayName);PlayerPrefs.Save();
            network.NetworkConfig.ConnectionData=Encoding.UTF8.GetBytes(JsonUtility.ToJson(new JoinData {name=DisplayName,rules=sceneWorld.Rules.Fingerprint()}));
            network.GetComponent<UnityTransport>().SetConnectionData(Address,Port,"0.0.0.0");
            Status=(host?network.StartHost():network.StartClient())?"Bağlanıyor…":"Oda açılamadı; Console'u kontrol et.";
        }
        public void Leave(){if(network!=null&&!Restarting){lastStatus="Odadan ayrıldın.";network.Shutdown();}}
        public void NewRound(bool sameSeed)
        {
            var world=WarehouseWorld.Instance;
            if(world!=null && network.IsHost && world.Completed.Value)world.ServerStartRound(sameSeed?world.Seed.Value:FreshSeed());
        }
    }
}
