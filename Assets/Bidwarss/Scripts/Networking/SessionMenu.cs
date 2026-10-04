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
    [DefaultExecutionOrder(-20000)]
    public sealed class SessionMenu : MonoBehaviour
    {
        [Serializable] sealed class JoinData { public int protocol=3; public string name; public string rules; }
        public static SessionMenu Instance { get; private set; }
        public static int RequestedSeed { get; private set; }
        public NetworkManager network;
        public WarehouseWorld sceneWorld;
        public Camera lobbyCamera;
        [Tooltip("Optional four spawn markers in your interior scene. Empty uses prototype positions.")]
        public Transform[] spawnPoints;
        public Vector3 SpawnPosition(int seat)
        {
            if(spawnPoints!=null&&spawnPoints.Length>0&&spawnPoints[seat%spawnPoints.Length]!=null)
                return spawnPoints[seat%spawnPoints.Length].position;
            return new Vector3(-2.4f+seat*1.6f,.1f,-11);
        }
        public Quaternion SpawnRotation(int seat)
        {
            if(spawnPoints!=null&&spawnPoints.Length>0&&spawnPoints[seat%spawnPoints.Length]!=null)
                return spawnPoints[seat%spawnPoints.Length].rotation;
            return Quaternion.identity;
        }
        public string Address="127.0.0.1",DisplayName="Oyuncu",SeedText="";
        public string Status {get;private set;}="Depo seni bekliyor.";
        public bool Daily;
        public bool Restarting {get;private set;}
        readonly Dictionary<ulong,int> seats=new Dictionary<ulong,int>();
        readonly Dictionary<ulong,string> names=new Dictionary<ulong,string>();
        const ushort Port=7777;
        static string lastStatus;
        bool starting;
        NetworkPrefabsList runtimePrefabs;
        void Awake()
        {
            Instance=this;Application.runInBackground=true;
            RemoveDuplicatePrefabRegistrations();
            DisplayName=PlayerPrefs.GetString("Bidwarss.Name","Oyuncu");
            if(!string.IsNullOrEmpty(lastStatus)){Status=lastStatus;lastStatus=null;}
            network.NetworkConfig.ConnectionApproval=true;network.NetworkConfig.ProtocolVersion=3;
            network.ConnectionApprovalCallback=Approve;
            network.OnClientDisconnectCallback+=Disconnected;network.OnClientStopped+=Stopped;network.OnTransportFailure+=TransportFailed;
        }
        void RemoveDuplicatePrefabRegistrations()
        {
            if(network==null||network.NetworkConfig==null)return;
            var lists=network.NetworkConfig.Prefabs.NetworkPrefabsLists;
            var unique=new List<NetworkPrefab>();
            var seen=new Dictionary<uint,NetworkPrefab>();
            int duplicates=0;
            foreach(var list in lists)
            {
                if(list==null)continue;
                foreach(var entry in list.PrefabList)
                {
                    if(entry==null)continue;
                    uint hash=entry.SourcePrefabGlobalObjectIdHash;
                    if(seen.TryGetValue(hash,out var previous)&&previous.Equals(entry))
                    {duplicates++;continue;}
                    // Distinct overrides or hash collisions require an explicit authoring fix.
                    if(!seen.ContainsKey(hash))seen.Add(hash,entry);
                    unique.Add(entry);
                }
            }
            if(duplicates==0)return;
            // Use a private runtime list; never mutate shared project assets.
            runtimePrefabs=ScriptableObject.CreateInstance<NetworkPrefabsList>();
            runtimePrefabs.name="Bidwarss unique session prefabs";
            foreach(var entry in unique)runtimePrefabs.Add(entry);
            lists.Clear();lists.Add(runtimePrefabs);
            Debug.Log("Bidwarss: "+duplicates+" yinelenen prefab kaydı birleştirildi.");
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
        public int SeatFor(ulong client)=>seats.TryGetValue(client,out var seat)?seat:0;
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
                if(data==null || data.protocol!=3 || data.rules!=sceneWorld.Rules.Fingerprint())
                {response.Reason="Oyun sürümü veya eşya kataloğu farklı. Aynı build ile bağlan.";return;}
                int seat=-1;for(int i=0;i<4;i++)if(!seats.ContainsValue(i)){seat=i;break;}
                if(seat<0){response.Reason="Oda dolu: en fazla 4 oyuncu.";return;}
                seats[request.ClientNetworkId]=seat;names[request.ClientNetworkId]=CleanName(data.name);
                response.Approved=true;response.CreatePlayerObject=true;
                response.Position=SpawnPosition(seat);
                response.Rotation=SpawnRotation(seat);
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
            if(starting||Restarting||(network!=null&&network.IsListening))return;
            starting=true;
            Status="Oda hazırlanıyor…";
            StartCoroutine(StartSessionRoutine(host));
        }
        IEnumerator StartSessionRoutine(bool host)
        {
            // Leave the IMGUI event before creating network objects and the player camera.
            yield return null;
            try { BeginSession(host); }
            catch(Exception ex)
            {
                Status="Başlatma hatası: "+ex.Message;
                Debug.LogException(ex);
                lastStatus=Status;
                if(network!=null&&network.IsListening)network.Shutdown();
            }
            if(network!=null&&network.IsListening)
            {
                float deadline=Time.realtimeSinceStartup+20f;
                while(network!=null&&network.IsListening&&!Restarting&&Time.realtimeSinceStartup<deadline)
                {
                    if(WarehousePlayer.Local!=null&&WarehouseWorld.Instance!=null)
                    {Status="Depo hazır.";starting=false;yield break;}
                    yield return null;
                }
                if(network!=null&&network.IsListening&&!Restarting)
                {
                    Status=WarehousePlayer.Local==null
                        ? "Oyuncu oluşturulamadı. Console'daki ilk kırmızı hatayı paylaş."
                        : "Depo ağ üzerinde başlatılamadı. Bidwarss menüsünden depo sahnesini yeniden oluştur.";
                    Debug.LogError(Status);lastStatus=Status;network.Shutdown();
                }
            }
            starting=false;
        }
        void BeginSession(bool host)
        {
            if(network==null)throw new InvalidOperationException("NetworkManager bağlantısı eksik.");
            if(sceneWorld==null||sceneWorld.catalog==null)throw new InvalidOperationException("Depo veya eşya kataloğu bağlantısı eksik.");
            var transport=network.GetComponent<UnityTransport>();
            if(transport==null)throw new InvalidOperationException("UnityTransport eksik.");
            network.NetworkConfig.NetworkTransport=transport;
            var prefab=network.NetworkConfig.PlayerPrefab;
            if(prefab==null||prefab.GetComponent<NetworkObject>()==null||prefab.GetComponent<WarehousePlayer>()==null)
                throw new InvalidOperationException("Oyuncu prefabı eksik veya geçersiz. Bidwarss > Build Uploaded Depot (Co-op) çalıştır.");
            if(sceneWorld.slots==null||sceneWorld.slots.Length<sceneWorld.totalGroups||sceneWorld.crates==null||sceneWorld.itemOrigins==null||sceneWorld.itemOrigins.Length!=sceneWorld.crates.Length||sceneWorld.lids==null||sceneWorld.lids.Length!=sceneWorld.crates.Length)
                throw new InvalidOperationException("Depo sahne bağlantıları eksik. Bidwarss > Build Uploaded Depot (Co-op) çalıştır.");
            if(!host&&(!IPAddress.TryParse(Address,out var ip)||ip.AddressFamily!=System.Net.Sockets.AddressFamily.InterNetwork))
            {Status="Geçerli IPv4 adresi yaz. Örnek: 192.168.1.10";return;}
            try {sceneWorld.Rules.Validate();}
            catch(Exception ex){Status=ex.Message;return;}
            if(Daily)RequestedSeed=int.Parse(DateTime.UtcNow.ToString("yyyyMMdd"));
            else if(string.IsNullOrWhiteSpace(SeedText))RequestedSeed=FreshSeed();
            else if(int.TryParse(SeedText,out int seed))RequestedSeed=seed;
            else {Status="Seed bir tam sayı olmalı.";return;}
            DisplayName=CleanName(DisplayName);PlayerPrefs.SetString("Bidwarss.Name",DisplayName);PlayerPrefs.Save();
            network.NetworkConfig.ConnectionData=Encoding.UTF8.GetBytes(JsonUtility.ToJson(new JoinData {name=DisplayName,rules=sceneWorld.Rules.Fingerprint()}));
            transport.SetConnectionData(host?"127.0.0.1":Address,Port,"0.0.0.0");
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
