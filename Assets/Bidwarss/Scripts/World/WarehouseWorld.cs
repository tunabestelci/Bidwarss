using System;
using System.Collections.Generic;
using Bidwarss.Domain;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Bidwarss
{
    public sealed class WarehouseWorld : NetworkBehaviour
    {
        public static WarehouseWorld Instance { get; private set; }
        public ItemCatalog catalog;
        public Material itemMaterial, dustMaterial;
        public Transform[] crates, itemOrigins, slots;
        public Transform recoveryOrigin;
        public Transform[] lids;
        public TextMesh[] stackLabels;
        [Range(1,30)] public int totalGroups = 12;
        [Range(.5f,5)] public float openSeconds = 1.35f;
        public NetworkList<ItemState> Items;
        public NetworkList<CrateState> Crates;
        public NetworkList<StackState> Stacks;
        public readonly NetworkVariable<int> Seed = new NetworkVariable<int>();
        public readonly NetworkVariable<int> SecuredDollars = new NetworkVariable<int>();
        public readonly NetworkVariable<int> FinalDollars = new NetworkVariable<int>();
        public readonly NetworkVariable<int> PlacedCount = new NetworkVariable<int>();
        public readonly NetworkVariable<int> PeakPlayers = new NetworkVariable<int>();
        public readonly NetworkVariable<double> StartedAt = new NetworkVariable<double>();
        public readonly NetworkVariable<double> FinishedAt = new NetworkVariable<double>();
        public readonly NetworkVariable<bool> Completed = new NetworkVariable<bool>();
        public readonly NetworkVariable<FixedString64Bytes> RunId = new NetworkVariable<FixedString64Bytes>();
        public readonly NetworkVariable<FixedString128Bytes> RulesHash = new NetworkVariable<FixedString128Bytes>();
        public readonly NetworkVariable<FixedString512Bytes> TeamNames = new NetworkVariable<FixedString512Bytes>();
        public RoundEngine Engine { get; private set; }
        readonly Dictionary<int, ItemVisual> views = new Dictionary<int, ItemVisual>();
        readonly Dictionary<ulong, string> roster = new Dictionary<ulong, string>();
        double[] holdStarted;
        bool[] knownOpen;
        string viewedRun;
        float nextHoldUpdate;
        public GameRules Rules => catalog.CreateRules(crates.Length, totalGroups);
        public double Elapsed => StartedAt.Value <= 0 ? 0 : Math.Max(0, (Completed.Value ? FinishedAt.Value : NetworkManager.ServerTime.Time) - StartedAt.Value);
        public int OpenCount { get { int n=0; for(int i=0;i<Crates.Count;i++) if(Crates[i].opened)n++; return n; } }

        void Awake() { Items = new NetworkList<ItemState>(); Crates = new NetworkList<CrateState>(); Stacks = new NetworkList<StackState>(); }
        public override void OnNetworkSpawn()
        {
            Instance = this;
            for(int i=0;i<slots.Length;i++)
                if(slots[i]!=null && slots[i].GetComponent<PalletVisual>()==null && slots[i].GetComponent<MeshRenderer>()!=null && slots[i].name.StartsWith("Istif "))
                    slots[i].gameObject.AddComponent<PalletVisual>();
            if (IsServer)
            {
                NetworkManager.OnClientDisconnectCallback += Disconnected;
                ServerStartRound(SessionMenu.RequestedSeed);
            }
            RunId.OnValueChanged += RunChanged;
            ResetPresentation();
        }
        public override void OnNetworkDespawn()
        {
            if (NetworkManager != null) NetworkManager.OnClientDisconnectCallback -= Disconnected;
            RunId.OnValueChanged -= RunChanged;
            ClearViews();
            if (Instance == this) Instance = null;
        }
        void RunChanged(FixedString64Bytes previous, FixedString64Bytes current) { ResetPresentation(); }
        void ClearViews() { foreach(var v in views.Values) if(v != null) Destroy(v.gameObject); views.Clear(); }
        void ResetPresentation()
        {
            ClearViews(); viewedRun = RunId.Value.ToString(); knownOpen = new bool[Crates.Count];
            // Existing opened crates at late join are snapshots, not new reveal events.
            for(int i=0;i<Crates.Count;i++) knownOpen[i]=Crates[i].opened;
        }
        public void ServerStartRound(int seed)
        {
            if (!IsServer) return;
            var rules = Rules; rules.Validate();
            if(slots.Length < totalGroups || itemOrigins.Length != crates.Length || lids.Length != crates.Length)
                throw new InvalidOperationException("Depo sahnesini Bidwarss menüsünden V2 için yeniden oluştur.");
            Engine = new RoundEngine(rules, seed);
            Completed.Value=false; StartedAt.Value=0; FinishedAt.Value=0; FinalDollars.Value=0;
            SecuredDollars.Value=0; PlacedCount.Value=0; PeakPlayers.Value=0; Seed.Value=seed;
            RulesHash.Value=Engine.RulesHash; TeamNames.Value=""; roster.Clear();
            Items.Clear(); Crates.Clear(); Stacks.Clear();
            holdStarted=new double[crates.Length];
            for(int i=0;i<crates.Length;i++) Crates.Add(new CrateState { opener=ItemState.Nobody });
            for(int i=0;i<Engine.StackCount;i++) Stacks.Add(new StackState { kind=Engine.StackKind(i) });
            foreach(var item in Engine.Items) Items.Add(ToState(item, Vector3.zero));
            foreach(var p in WarehousePlayer.Players.Values) p.ClearOpenIntent();
            RunId.Value=Guid.NewGuid().ToString("N");
        }
        public int HeldCount(ulong client, out int kind)
        {
            kind=-1; int n=0;
            for(int i=0;i<Items.Count;i++) if(Items[i].holder==client && Items[i].location==ItemLocation.Held) { n++; kind=Items[i].kind; }
            return n;
        }
        public int HeldValue(ulong client)
        { int total=0; for(int i=0;i<Items.Count;i++) if(Items[i].holder==client) total+=Items[i].dollars; return total; }
        public string Hint(InteractionTarget target, ulong client)
        {
            if(Completed.Value) return "Depo tamamlandı! Sonuçlara TAB ile bak.";
            if(target==null) return "Kutuyu aç • Eşyaları türüne göre 10'lu istifle";
            int kind; int held=HeldCount(client,out kind);
            if(target.kind==TargetKind.Crate)
                return Crates[target.id].opened ? "Kutu boşaltıldı" : "E BASILI TUT • Kutuyu aç";
            if(target.kind==TargetKind.Slot)
            {
                if(target.id>=Stacks.Count) return "";
                var stack=Stacks[target.id];
                return catalog.entries[stack.kind].title+" • "+stack.count+"/10"+(held>0 ? (kind==stack.kind ? " • E: İstifle" : " • Farklı eşya türü") : "");
            }
            if(target.id<0 || target.id>=Items.Count)return "";
            var item=Items[target.id];
            return "E: Al • "+catalog.entries[item.kind].title+" • "+GameRules.ConditionNames[(int)item.condition]+" • $"+item.dollars;
        }
        void Update()
        {
            if(!IsSpawned || !IsServer || Engine==null || Completed.Value)return;
            if(StartedAt.Value>0) TrackRoster();
            if(Time.unscaledTime<nextHoldUpdate)return;
            nextHoldUpdate=Time.unscaledTime+.05f;
            for(int i=0;i<Crates.Count;i++)
            {
                var state=Crates[i]; if(state.opened)continue;
                WarehousePlayer owner=null;
                if(state.opener!=ItemState.Nobody) WarehousePlayer.Players.TryGetValue(state.opener,out owner);
                if(!ValidOpener(owner,i))
                {
                    state.opener=ItemState.Nobody; state.progress=0; holdStarted[i]=0;
                    foreach(var candidate in WarehousePlayer.Players.Values)
                        if(ValidOpener(candidate,i)) { owner=candidate; state.opener=owner.OwnerClientId; holdStarted[i]=NetworkManager.ServerTime.Time; break; }
                }
                if(state.opener!=ItemState.Nobody)
                {
                    if(StartedAt.Value<=0) { StartedAt.Value=NetworkManager.ServerTime.Time; TrackRoster(); }
                    state.progress=Mathf.Clamp01((float)((NetworkManager.ServerTime.Time-holdStarted[i])/openSeconds));
                    if(state.progress>=1)
                    {
                        string error;
                        if(Engine.Open(i,out error))
                        {
                            state.opened=true; state.opener=ItemState.Nobody;
                            foreach(var item in Engine.Items) if(item.crate==i) Items[item.id]=ToState(item,RevealPosition(item.id,i));
                        }
                    }
                }
                if(!state.Equals(Crates[i]))Crates[i]=state;
            }
        }
        bool ValidOpener(WarehousePlayer player,int crate)
        {
            int kind;
            return player!=null && player.WantsCrate==crate && player.InputFresh &&
                Engine.HeldCount(player.OwnerClientId,out kind)==0 && player.ServerLooksAt(TargetKind.Crate,crate);
        }
        void TrackRoster()
        {
            PeakPlayers.Value=Math.Max(PeakPlayers.Value,NetworkManager.ConnectedClients.Count);
            foreach(var p in WarehousePlayer.Players.Values) roster[p.OwnerClientId]=p.PlayerName.Value.ToString();
            var names=new List<string>(roster.Values); names.Sort(StringComparer.Ordinal);
            // Cap departed-player history to fit the fixed network string and result UI.
            if(names.Count>8)names.RemoveRange(8,names.Count-8);
            TeamNames.Value=string.Join(" / ",names.ToArray());
        }
        Vector3 RevealPosition(int id,int crate)
        {
            int n=id/crates.Length;
            return itemOrigins[crate].position+new Vector3((n%5)*.48f,.23f,(n/5)*.48f);
        }
        // Two rows of five objects. Rotation follows the pallet, independently of its model scale.
        public Vector3 StackPosition(int stack,int index) => slots[stack].position+slots[stack].rotation*new Vector3((index%2-.5f)*.47f,.30f,(index/2-2)*.40f);
        ItemState ToState(RoundItem item,Vector3 position)
        {
            bool sealedItem=item.location==ItemLocation.Sealed;
            return new ItemState { id=item.id,kind=sealedItem?-1:item.kind,crate=item.crate,
                condition=sealedItem?ItemCondition.Terrible:item.condition,dollars=sealedItem?0:item.dollars,
                location=item.location,holder=item.holder,slot=item.stack,stackIndex=item.stackIndex,position=position };
        }
        void PublishProgress()
        {
            for(int i=0;i<Stacks.Count;i++) Stacks[i]=new StackState { kind=Engine.StackKind(i),count=Engine.StackCountAt(i) };
            PlacedCount.Value=Engine.PlacedCount; SecuredDollars.Value=Engine.SecuredDollars;
            if(Engine.Completed && !Completed.Value)
            {
                TrackRoster(); FinalDollars.Value=Engine.FinalDollars;
                FinishedAt.Value=NetworkManager.ServerTime.Time; Completed.Value=true;
            }
        }
        public void Act(WarehousePlayer player,TargetKind kind,int id)
        {
            if(!IsServer || Engine==null || Completed.Value || !player.ServerLooksAt(kind,id))return;
            string error="";
            if(kind==TargetKind.Item)
            {
                if(Engine.Take(id,player.OwnerClientId,out error))
                { Items[id]=ToState(Engine.Items[id],Items[id].position); PublishProgress(); }
            }
            else if(kind==TargetKind.Slot)
            {
                if(Engine.Place(id,player.OwnerClientId,out error)>0)
                {
                    foreach(var item in Engine.Items) if(item.stack==id) Items[item.id]=ToState(item,StackPosition(id,item.stackIndex));
                    PublishProgress();
                }
            }
            if(!string.IsNullOrEmpty(error))player.Feedback(error);
        }
        public void Drop(WarehousePlayer player)
        {
            if(!IsServer || Engine==null || Completed.Value)return;
            int id=-1;
            for(int i=0;i<Items.Count;i++)if(Items[i].holder==player.OwnerClientId)id=i;
            if(id<0)return;
            Vector3 center=player.transform.position+player.transform.forward*1.15f;
            if(!Physics.Raycast(center+Vector3.up*.8f,Vector3.down,out var floor,2.5f,~0,QueryTriggerInteraction.Ignore))
            {player.Feedback("Burada esyayi birakacak zemin yok.");return;}
            center=floor.point+Vector3.up*.23f;
            Physics.SyncTransforms();
            foreach(var hit in Physics.OverlapBox(center,new Vector3(.22f,.2f,.22f),Quaternion.identity,~0,QueryTriggerInteraction.Ignore))
                if(!hit.transform.IsChildOf(player.transform)) { player.Feedback("Önündeki alan dolu."); return; }
            if(Engine.Drop(id,player.OwnerClientId)) Items[id]=ToState(Engine.Items[id],center);
        }
        void Disconnected(ulong client)
        {
            if(!IsServer || Engine==null)return;
            var released=new List<int>();
            foreach(var item in Engine.Items)if(item.holder==client)released.Add(item.id);
            Engine.Disconnect(client);
            foreach(int id in released)
            {
                // Unique recovery grid for every item, never inside a filled stack.
                Vector3 pos=recoveryOrigin!=null
                    ?recoveryOrigin.position+recoveryOrigin.rotation*new Vector3((id%40)*.6f,.23f,-(id/40)*.46f)
                    :new Vector3(-12f+(id%40)*.6f,.23f,-13f-(id/40)*.46f);
                Items[id]=ToState(Engine.Items[id],pos);
            }
        }
        void LateUpdate()
        {
            if(!IsSpawned)return;
            // Older generated scenes gain a usable target on the entire pallet without regeneration.
            for(int i=0;i<slots.Length;i++)
                if(slots[i]!=null && slots[i].GetComponent<InteractionTarget>()==null)
                {var target=slots[i].gameObject.AddComponent<InteractionTarget>();target.kind=TargetKind.Slot;target.id=i;}
            if(viewedRun!=RunId.Value.ToString() || knownOpen==null || knownOpen.Length!=Crates.Count)ResetPresentation();
            for(int i=0;i<Crates.Count;i++)
            {
                bool open=Crates[i].opened;
                if(open && !knownOpen[i] && !Application.isBatchMode) RevealEffects.Play(crates[i].position+Vector3.up*.7f,dustMaterial);
                knownOpen[i]=open;
                if(lids[i]!=null)lids[i].localRotation=Quaternion.Slerp(lids[i].localRotation,Quaternion.Euler(open?-110:0,0,0),Time.deltaTime*9);
            }
            for(int i=0;i<slots.Length;i++)
            {
                slots[i].gameObject.SetActive(i<Stacks.Count);
                if(i<Stacks.Count && stackLabels[i]!=null)
                {
                    var s=Stacks[i]; stackLabels[i].text=catalog.entries[s.kind].title+"\n"+s.count+" / 10";
                    stackLabels[i].color=s.count==10?new Color(.3f,1,.55f):Color.white;
                }
            }
            int carryIndex=0;
            var carryIndices=new Dictionary<ulong,int>();
            for(int i=0;i<Items.Count;i++)
            {
                var item=Items[i]; if(item.location==ItemLocation.Sealed)continue;
                ItemVisual view;
                if(!views.TryGetValue(item.id,out view))
                { view=ItemVisual.Create(item,catalog.entries[item.kind],itemMaterial); views.Add(item.id,view); }
                Vector3 pos=item.position; Quaternion rot=item.location==ItemLocation.Stacked && item.slot>=0 ? slots[item.slot].rotation : Quaternion.Euler(0,item.yaw,0);
                if(item.location==ItemLocation.Held && WarehousePlayer.Players.TryGetValue(item.holder,out var owner))
                {
                    carryIndices.TryGetValue(item.holder,out carryIndex); carryIndices[item.holder]=carryIndex+1;
                    pos=owner.transform.position+Vector3.up*(.73f+carryIndex*.065f)+owner.transform.forward*.9f+owner.transform.right*.32f;
                    rot=owner.transform.rotation;
                }
                view.UpdateState(item,pos,rot);
            }
        }
    }
}
