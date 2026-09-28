using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Bidwarss
{
    // One authoritative state ledger. Clients request actions, never assign item ownership.
    // NetworkList provides snapshots for players joining an existing session.
    public sealed class WarehouseWorld : NetworkBehaviour
    {
        public static WarehouseWorld Instance { get; private set; }
        public ItemCatalog catalog;
        public Material itemMaterial;
        public Transform[] slots;
        [Tooltip("Development samples only; this is not the final crate distribution.")]
        public bool spawnTestItems = true;
        public Transform[] testItemSpawns;
        public NetworkList<ItemState> Items;
        readonly Dictionary<int, GameObject> views = new Dictionary<int, GameObject>();
        readonly MaterialPropertyBlock tint = new MaterialPropertyBlock();

        void Awake() { Items = new NetworkList<ItemState>(); }

        public override void OnNetworkSpawn()
        {
            Instance = this;
            if (IsServer)
            {
                NetworkManager.OnClientDisconnectCallback += ReleaseDisconnectedPlayer;
                if (spawnTestItems && catalog != null && catalog.entries != null && catalog.entries.Length > 0)
                {
                    for (int i = 0; i < testItemSpawns.Length; i++)
                    {
                        int type = i % catalog.entries.Length;
                        Vector3 position = testItemSpawns[i].position;
                        position.y = catalog.entries[type].size.y * .5f;
                        Items.Add(new ItemState { id = i, kind = type, position = position,
                            yaw = 0, holder = ItemState.Nobody, slot = -1 });
                    }
                }
            }
        }

        public override void OnNetworkDespawn()
        {
            if (NetworkManager != null)
                NetworkManager.OnClientDisconnectCallback -= ReleaseDisconnectedPlayer;
            foreach (var view in views.Values) if (view != null) Destroy(view);
            views.Clear();
            if (Instance == this) Instance = null;
        }

        public int HeldIndex(ulong client)
        {
            for (int i = 0; i < Items.Count; i++) if (Items[i].holder == client) return i;
            return -1;
        }

        public int PlacedCount
        {
            get { int n = 0; for (int i = 0; i < Items.Count; i++) if (Items[i].slot >= 0) n++; return n; }
        }

        public string Hint(InteractionTarget target, ulong client)
        {
            if (target == null) return "";
            int held = HeldIndex(client);
            if (target.kind == TargetKind.Crate)
                return "Kasa icerigi sonraki asamada";
            if (target.kind == TargetKind.Slot) return held >= 0 ? "E - Rafa yerlestir" : "Raf yeri";
            if (target.id < 0 || target.id >= Items.Count) return "";
            var item = Items[target.id];
            return item.holder != ItemState.Nobody ? "Baska oyuncu tasiyor" :
                held >= 0 ? "Once elindeki esyayi birak" : "E - Al: " + catalog.entries[item.kind].title;
        }

        public void Act(WarehousePlayer player, TargetKind kind, int id)
        {
            if (!IsServer || player == null) return;
            ulong client = player.OwnerClientId;
            int held = HeldIndex(client);
            if (kind == TargetKind.Crate)
            {
                return; // No auction or crate-content system in the foundation milestone.
            }
            else if (kind == TargetKind.Item)
            {
                if (id < 0 || id >= Items.Count || held >= 0) return;
                var item = Items[id];
                if (item.holder != ItemState.Nobody || !CanReach(player, item.position)) return;
                item.holder = client;
                item.slot = -1;
                Items[id] = item;
            }
            else if (kind == TargetKind.Slot)
            {
                if (id < 0 || id >= slots.Length || held < 0 || !CanReach(player, slots[id].position)) return;
                for (int i = 0; i < Items.Count; i++) if (Items[i].slot == id) return;
                var item = Items[held];
                item.position = slots[id].position + Vector3.up * (.035f + catalog.entries[item.kind].size.y * .5f);
                item.holder = ItemState.Nobody;
                item.slot = id;
                item.yaw = slots[id].eulerAngles.y;
                Items[held] = item;
            }
        }

        bool CanReach(WarehousePlayer player, Vector3 target)
        {
            Vector3 origin = player.transform.position + Vector3.up * 1.5f;
            Vector3 delta = target - origin;
            if (delta.sqrMagnitude > 3.5f * 3.5f) return false;
            // Block interacting through scenery, but ignore the player's own capsule.
            foreach (var hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(player.transform)) continue;
                if (hit.collider.GetComponent<InteractionTarget>() != null) continue;
                if (hit.distance < delta.magnitude - .15f) return false;
            }
            return true;
        }

        public void Drop(WarehousePlayer player)
        {
            if (!IsServer) return;
            int index = HeldIndex(player.OwnerClientId);
            if (index < 0) return;
            var item = Items[index];
            Vector3 center = player.transform.position + player.transform.forward * 1.2f;
            center.y = catalog.entries[item.kind].size.y * .5f + .02f;
            Vector3 half = catalog.entries[item.kind].size * .49f;
            // Prevent dropping into walls, other items or shelving. Keep holding on failure.
            foreach (var collider in Physics.OverlapBox(center, half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                if (!collider.transform.IsChildOf(player.transform)) return;
            item.position = center;
            item.yaw = 0;
            item.holder = ItemState.Nobody;
            Items[index] = item;
        }

        void ReleaseDisconnectedPlayer(ulong client)
        {
            if (!IsServer) return;
            int index = HeldIndex(client);
            if (index < 0) return;
            var item = Items[index];
            // Return to its last supported position. Reserve no shelf while carried.
            item.holder = ItemState.Nobody;
            item.slot = -1;
            // Dedicated recovery strip at the entrance avoids colliding with a reused shelf slot.
            item.position = new Vector3(-10f + (item.id % 30) * .67f,
                catalog.entries[item.kind].size.y * .5f, -12f - (item.id / 30) * .7f);
            item.yaw = 0;
            Items[index] = item;
        }

        void LateUpdate()
        {
            if (!IsSpawned) return;
            for (int i = 0; i < Items.Count; i++)
            {
                var item = Items[i];
                if (!views.TryGetValue(item.id, out var view))
                {
                    view = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    view.name = catalog.entries[item.kind].title + " #" + item.id;
                    view.transform.localScale = catalog.entries[item.kind].size;
                    view.GetComponent<Renderer>().sharedMaterial = itemMaterial;
                    tint.SetColor("_BaseColor", catalog.entries[item.kind].color);
                    view.GetComponent<Renderer>().SetPropertyBlock(tint);
                    var target = view.AddComponent<InteractionTarget>();
                    target.kind = TargetKind.Item;
                    target.id = item.id;
                    views.Add(item.id, view);
                }
                Vector3 position = item.position;
                Quaternion rotation = Quaternion.Euler(0, item.yaw, 0);
                if (item.holder != ItemState.Nobody &&
                    WarehousePlayer.Players.TryGetValue(item.holder, out var owner) && owner != null)
                {
                    position = owner.transform.position + Vector3.up * 1.05f + owner.transform.forward * 1.0f;
                    rotation = owner.transform.rotation;
                }
                view.transform.SetPositionAndRotation(position, rotation);
                view.GetComponent<Collider>().enabled = item.holder == ItemState.Nobody;
            }
        }
    }
}
