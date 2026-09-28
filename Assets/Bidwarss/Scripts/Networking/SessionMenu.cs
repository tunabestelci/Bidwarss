using System.Collections;
using System.Collections.Generic;
using System.Net;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bidwarss
{
    public sealed class SessionMenu : MonoBehaviour
    {
        public NetworkManager network;
        public Camera lobbyCamera;
        readonly Dictionary<ulong, int> seats = new Dictionary<ulong, int>();
        string address = "127.0.0.1";
        string status = "Ilk prototip - LAN / IP baglantisi";
        bool restarting;
        const ushort Port = 7777;

        void Awake()
        {
            Application.runInBackground = true;
            network.NetworkConfig.ConnectionApproval = true;
            network.ConnectionApprovalCallback = Approve;
            network.OnClientDisconnectCallback += Disconnected;
            network.OnClientStopped += Stopped;
            network.OnTransportFailure += TransportFailed;
        }

        void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            // Reserve immediately so simultaneous connection requests cannot overbook four seats.
            if (!seats.TryGetValue(request.ClientNetworkId, out int seat))
            {
                seat = -1;
                for (int i = 0; i < 4; i++) if (!seats.ContainsValue(i)) { seat = i; break; }
                if (seat >= 0) seats.Add(request.ClientNetworkId, seat);
            }
            response.Approved = seat >= 0;
            response.CreatePlayerObject = response.Approved;
            response.Pending = false;
            response.Reason = response.Approved ? "" : "Oda dolu (4 oyuncu).";
            response.Position = new Vector3(-2.4f + Mathf.Max(0, seat) * 1.6f, .15f, -10);
            response.Rotation = Quaternion.identity;
        }

        void Disconnected(ulong client)
        {
            seats.Remove(client);
            if (!string.IsNullOrEmpty(network.DisconnectReason)) Debug.LogWarning(network.DisconnectReason);
        }

        void TransportFailed() { status = "Baglanti baslatilamadi. Console'u kontrol et."; }
        void Stopped(bool wasHost) { if (!restarting) StartCoroutine(ReturnToMenu()); }

        IEnumerator ReturnToMenu()
        {
            restarting = true;
            WarehousePlayer.LockCursor(false);
            while (network != null && network.ShutdownInProgress) yield return null;
            if (network != null) Destroy(network.gameObject);
            yield return null;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        void Update()
        {
            if (lobbyCamera != null) lobbyCamera.gameObject.SetActive(WarehousePlayer.Local == null);
        }

        void OnDestroy()
        {
            if (network == null) return;
            network.OnClientDisconnectCallback -= Disconnected;
            network.OnClientStopped -= Stopped;
            network.OnTransportFailure -= TransportFailed;
        }

        void StartSession(bool host)
        {
            if (!IPAddress.TryParse(address, out var ip) || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            { status = "Gecerli bir IPv4 adresi yaz (ornek: 192.168.1.10)."; return; }
            var transport = network.GetComponent<UnityTransport>();
            transport.SetConnectionData(address, Port, "0.0.0.0");
            bool started = host ? network.StartHost() : network.StartClient();
            status = started ? "Baglaniyor..." : "Baslatilamadi; port ve Console'u kontrol et.";
        }

        void OnGUI()
        {
            if (network == null) return;
            var world = WarehouseWorld.Instance;
            if (network.IsListening && world != null)
            {
                GUI.Box(new Rect(20, 20, 310, 100), "BIDWARSS / DEPO");
                GUI.Label(new Rect(35, 48, 290, 25), "Yerlesen test esyasi: " + world.PlacedCount + " / " + world.Items.Count);
                GUI.Label(new Rect(35, 73, 290, 25), "WASD hareket | E al/yerlestir | Q birak");
                if (world.Items.Count > 0 && world.PlacedCount == world.Items.Count)
                    GUI.Box(new Rect(Screen.width / 2 - 150, 65, 300, 40), "TEST ESYALARI YERLESTIRILDI!");
                if (WarehousePlayer.Local != null && Cursor.lockState == CursorLockMode.Locked)
                {
                    GUI.Label(new Rect(Screen.width / 2 - 4, Screen.height / 2 - 12, 20, 24), "+");
                    GUI.Box(new Rect(Screen.width / 2 - 180, Screen.height - 85, 360, 32), WarehousePlayer.Local.CurrentHint);
                    return;
                }
            }
            GUILayout.BeginArea(new Rect(Screen.width / 2 - 180, Screen.height / 2 - 150, 360, 300), GUI.skin.box);
            GUILayout.Label("BIDWARSS - CO-OP TEMEL PROTOTIP");
            GUILayout.Space(15);
            GUILayout.Label(status);
            if (!network.IsListening && !restarting)
            {
                GUILayout.Label("Host IPv4 / port 7777");
                address = GUILayout.TextField(address, 45);
                if (GUILayout.Button("Oda kur (host)", GUILayout.Height(35))) StartSession(true);
                if (GUILayout.Button("Odaya katil", GUILayout.Height(35))) StartSession(false);
            }
            else if (!restarting)
            {
                if (WarehousePlayer.Local != null && GUILayout.Button("Oyuna don", GUILayout.Height(35))) WarehousePlayer.LockCursor(true);
                if (GUILayout.Button("Odadan ayril / Baglantiyi iptal et", GUILayout.Height(35))) network.Shutdown();
            }
            GUILayout.Label("Birlikte tasi, ana depoyu duzenle. Esyalar simdilik test kutulari.");
            GUILayout.EndArea();
        }
    }
}
