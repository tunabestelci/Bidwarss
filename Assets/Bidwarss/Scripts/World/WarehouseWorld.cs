using System;
using System.Collections.Generic;
using Bidwarss.Domain;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Bidwarss
{
    [DefaultExecutionOrder(200)]
    public sealed class WarehouseWorld : NetworkBehaviour
    {
        public static WarehouseWorld Instance { get; private set; }
        public ItemCatalog catalog;
        public Material itemMaterial, dustMaterial;
        public Transform[] crates, itemOrigins, slots;
        public Transform recoveryOrigin;
        public int recoveryColumns=40;
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
        FixedString64Bytes viewedRun;
        float nextHoldUpdate;
        // Reused every frame so presentation does not allocate.
        readonly Dictionary<ulong,int> carryIndices=new Dictionary<ulong,int>();
        readonly Dictionary<int,int> revealOrder=new Dictionary<int,int>();
        StackLayout[] layouts;
        bool[] layoutsReady;
        bool targetsEnsured;
        public GameRules Rules => catalog.CreateRules(crates.Length, totalGroups);
        public double Elapsed => StartedAt.Value <= 0 ? 0 : Math.Max(0, (Completed.Value ? FinishedAt.Value : NetworkManager.ServerTime.Time) - StartedAt.Value);
        public int OpenCount { get { int n=0; for(int i=0;i<Crates.Count;i++) if(Crates[i].opened)n++; return n; } }
        // Containers that hold something in this depot; the rest of the scene's containers stay shut.
        public int ActiveCrateCount { get { int n=0; for(int i=0;i<Crates.Count;i++) if(Crates[i].active)n++; return n; } }

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
            ClearViews(); viewedRun = RunId.Value; knownOpen = new bool[Crates.Count];
            // Existing opened crates at late join are snapshots, not new reveal events.
            for(int i=0;i<Crates.Count;i++) knownOpen[i]=Crates[i].opened;
        }
        public void ServerStartRound(int seed)
        {
            if (!IsServer) return;
            var rules = Rules; rules.Validate();
            if(slots.Length < totalGroups || stackLabels==null || stackLabels.Length < totalGroups || itemOrigins.Length != crates.Length || lids.Length != crates.Length)
                throw new InvalidOperationException("Depo sahnesini Bidwarss menüsünden V2 için yeniden oluştur.");
            Engine = new RoundEngine(rules, seed);
            Completed.Value=false; StartedAt.Value=0; FinishedAt.Value=0; FinalDollars.Value=0;
            SecuredDollars.Value=0; PlacedCount.Value=0; PeakPlayers.Value=0; Seed.Value=seed;
            RulesHash.Value=Engine.RulesHash; TeamNames.Value=""; roster.Clear();
            Items.Clear(); Crates.Clear(); Stacks.Clear();
            holdStarted=new double[crates.Length];
            for(int i=0;i<crates.Length;i++) Crates.Add(new CrateState { opener=ItemState.Nobody, active=Engine.IsActive(i), openingMode=OpeningModeFor(i) });
            for(int i=0;i<Engine.StackCount;i++) Stacks.Add(new StackState { kind=Engine.StackKind(i), capacity=Engine.StackCapacity(i) });
            foreach(var item in Engine.Items) Items.Add(ToState(item, Vector3.zero));
            foreach(var p in WarehousePlayer.Players.Values) p.ServerResetForRound();
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
        public OpeningMode OpeningModeFor(int crate)
        {
            var profile=crates[crate].GetComponent<CrateOpeningProfile>();
            return profile!=null?profile.mode:OpeningMode.CutThenPry;
        }
        public float OpeningReach(int crate)=>(crate<Crates.Count?Crates[crate].openingMode:OpeningModeFor(crate))==OpeningMode.Hands?3.5f:.8f;
        float OpeningDuration(int crate)
        {
            var profile=crates[crate].GetComponent<CrateOpeningProfile>();
            return Mathf.Max(profile!=null?profile.duration:openSeconds,OpeningSequence.MinimumDuration(OpeningModeFor(crate)));
        }
        public bool TryOpening(ulong client,out CrateState state,out OpeningMode mode)
        {
            for(int i=0;i<Crates.Count;i++)
                if(!Crates[i].opened && Crates[i].opener==client)
                {state=Crates[i];mode=state.openingMode;return true;}
            state=default;mode=OpeningMode.Hands;return false;
        }
        public string Hint(InteractionTarget target, ulong client)
        {
            if(Completed.Value) return "Depo tamamlandı! Sonuçlara TAB ile bak.";
            if(target==null) return "Kutuyu aç • Eşyaları türüne göre istifle";
            int kind; int held=HeldCount(client,out kind);
            if(target.kind==TargetKind.Crate)
            {
                if(target.id<0 || target.id>=Crates.Count)return "";
                if(!Crates[target.id].active)return "Bu konteyner bu turda boş • kapalı kalır";
                if(Crates[target.id].opened)return "Kutu boşaltıldı";
                if(held>0)return "Önce elindeki eşyaları bırak veya istifle";
                if(WarehousePlayer.Players.TryGetValue(client,out var player) && player.IsOwner && player.LookDistance>OpeningReach(target.id))return "Aracı kullanmak için kasaya yaklaş";
                var tool=OpeningSequence.Sample(Crates[target.id].openingMode,Crates[target.id].progress,out _);
                return tool==OpeningTool.BoxCutter?"E BASILI TUT • Maket bıçağıyla bandı kes":tool==OpeningTool.PryBar?"E BASILI TUT • Çivi sökücüyle kapağı gevşet":"E BASILI TUT • Kapıyı aç";
            }
            if(target.kind==TargetKind.Slot)
            {
                if(target.id>=Stacks.Count) return "";
                var stack=Stacks[target.id];
                return catalog.entries[stack.kind].title+" • "+stack.count+"/"+stack.capacity+(held>0 ? (kind==stack.kind ? " • E: İstifle" : " • Farklı eşya türü") : "");
            }
            if(target.id<0 || target.id>=Items.Count)return "";
            var item=Items[target.id];
            return "E: Al • "+catalog.entries[item.kind].title+SizeText(catalog.entries[item.kind])+" • "+GameRules.ConditionNames[(int)item.condition]+" • $"+item.dollars;
        }
        static string SizeText(ItemCatalog.Entry entry)
        {
            int size=Math.Max(entry.heightCm,Math.Max(entry.widthCm,entry.depthCm));
            return size>0?" ("+size+" cm"+(entry.kg>0?", "+entry.kg.ToString("0.#",System.Globalization.CultureInfo.InvariantCulture)+" kg":"")+")":"";
        }
        void Update()
        {
            if(!IsSpawned || !IsServer || Engine==null || Completed.Value)return;
            if(StartedAt.Value>0) TrackRoster();
            if(Time.unscaledTime<nextHoldUpdate)return;
            nextHoldUpdate=Time.unscaledTime+.05f;
            for(int i=0;i<Crates.Count;i++)
            {
                var state=Crates[i]; if(state.opened||!state.active)continue;
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
                    if(owner.ServerLookHit(TargetKind.Crate,i,OpeningReach(i),out var contact))
                    {state.contactPoint=contact.point;state.contactNormal=contact.normal;}
                    state.progress=Mathf.Clamp01((float)((NetworkManager.ServerTime.Time-holdStarted[i])/OpeningDuration(i)));
                    if(state.progress>=1)
                    {
                        string error;
                        if(Engine.Open(i,out error))
                        {
                            state.opened=true; state.openedAt=NetworkManager.ServerTime.Time; state.opener=ItemState.Nobody;
                            foreach(var item in Engine.Items) if(item.crate==i) Items[item.id]=ToState(item,RevealPosition(item),RevealYaw(item));
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
                Engine.HeldCount(player.OwnerClientId,out kind)==0 && player.ServerLookHit(TargetKind.Crate,crate,OpeningReach(crate),out _);
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
        // Pieces spill out in a loose grid. Position and heading are jittered from the seed so no two spills look alike.
        Vector3 RevealPosition(RoundItem item)
        {
            var jitter=new System.Random(unchecked(Engine.Seed*7919+item.id*104729+17));
            int n=item.crateSlot;
            float x=(n%5)*.48f+(float)(jitter.NextDouble()-.5)*.14f,z=(n/5)*.48f+(float)(jitter.NextDouble()-.5)*.14f;
            return itemOrigins[item.crate].position+itemOrigins[item.crate].rotation*new Vector3(x,.23f,z);
        }
        static float RevealYaw(RoundItem item)
        {
            var spin=new System.Random(unchecked(item.id*15485863+3));
            return (float)(spin.NextDouble()*360);
        }
        // Two rows of five objects. Rotation follows the pallet, independently of its model scale.
        public Vector3 StackPosition(int stack,int index)
        {
            var layout=LayoutFor(stack);
            return layout!=null?layout.Center(index):slots[stack].position+slots[stack].rotation*new Vector3((index%2-.5f)*.47f,.30f,(index/2-2)*.40f);
        }
        // GetComponent is cached per pallet; slots never change after the scene loads.
        StackLayout LayoutFor(int stack)
        {
            if(layouts==null||layouts.Length!=slots.Length){layouts=new StackLayout[slots.Length];layoutsReady=new bool[slots.Length];}
            if(stack<0||stack>=layouts.Length)return null;
            if(!layoutsReady[stack]){layouts[stack]=slots[stack]!=null?slots[stack].GetComponent<StackLayout>():null;layoutsReady[stack]=true;}
            return layouts[stack];
        }
        Quaternion StackRotation(int stack,int index)
        {var layout=LayoutFor(stack);return layout!=null?layout.Rotation(index):slots[stack].rotation;}
        ItemState ToState(RoundItem item,Vector3 position,float yaw=0)
        {
            bool sealedItem=item.location==ItemLocation.Sealed;
            return new ItemState { id=item.id,kind=sealedItem?-1:item.kind,crate=item.crate,
                condition=sealedItem?ItemCondition.VeryBad:item.condition,dollars=sealedItem?0:item.dollars,
                location=item.location,holder=item.holder,slot=item.stack,stackIndex=item.stackIndex,position=position,yaw=yaw };
        }
        void PublishProgress()
        {
            for(int i=0;i<Stacks.Count;i++) Stacks[i]=new StackState { kind=Engine.StackKind(i),count=Engine.StackCountAt(i),capacity=Engine.StackCapacity(i) };
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
            {player.Feedback("Burada eşyayı bırakacak zemin yok.");return;}
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
                    ?recoveryOrigin.position+recoveryOrigin.rotation*new Vector3((id%Math.Max(1,recoveryColumns))*.6f,.23f,-(id/Math.Max(1,recoveryColumns))*.46f)
                    :new Vector3(-12f+(id%40)*.6f,.23f,-13f-(id/40)*.46f);
                Items[id]=ToState(Engine.Items[id],pos);
            }
        }
        static void FitStackLabel(TextMesh label)
        {
            if(label.font==null)return;
            label.richText=false;
            label.font.RequestCharactersInTexture(label.text,label.fontSize,label.fontStyle);
            float width=0,lineWidth=0;
            int lines=1;
            foreach(char c in label.text)
            {
                if(c=='\n'){width=Mathf.Max(width,lineWidth);lineWidth=0;lines++;continue;}
                if(label.font.GetCharacterInfo(c,out var glyph,label.fontSize,label.fontStyle))lineWidth+=glyph.advance;
            }
            width=Mathf.Max(width,lineWidth);
            // TextMesh maps font pixels into characterSize / 10 world units.
            // Keep names inside one 2.2 m rack sign, including the count line.
            var scale=label.transform.lossyScale;
            float byWidth=22f/(Mathf.Max(1,width)*Mathf.Max(.001f,Mathf.Abs(scale.x)));
            float byHeight=5.5f/(Mathf.Max(1,label.fontSize)*lines*Mathf.Max(.001f,Mathf.Abs(scale.y)));
            label.characterSize=Mathf.Min(.1f,byWidth,byHeight);
        }
        // A piece that appears right after its container opened flies out of the doors; late joiners just see it lying there.
        void StartRevealIfFresh(ItemVisual view,ItemState item)
        {
            if(Application.isBatchMode||item.location!=ItemLocation.Loose||item.crate<0||item.crate>=Crates.Count||item.crate>=crates.Length)return;
            var crate=Crates[item.crate];
            if(!crate.opened||NetworkManager.ServerTime.Time-crate.openedAt>2.5)return;
            int order; revealOrder.TryGetValue(item.crate,out order); revealOrder[item.crate]=order+1;
            float delay=.18f+Mathf.Min(1.1f,order*.055f);
            view.BeginReveal(crates[item.crate].position+Vector3.up*.9f,delay,.7f+Mathf.Min(.2f,order*.01f),.7f+(order%4)*.12f);
        }
        void LateUpdate()
        {
            if(!IsSpawned)return;
            // Older generated scenes gain a usable target on the entire pallet without regeneration.
            if(!targetsEnsured)
            {
                targetsEnsured=true;
                for(int i=0;i<slots.Length;i++)
                    if(slots[i]!=null && slots[i].GetComponent<InteractionTarget>()==null)
                    {var target=slots[i].gameObject.AddComponent<InteractionTarget>();target.kind=TargetKind.Slot;target.id=i;}
            }
            if(!viewedRun.Equals(RunId.Value) || knownOpen==null || knownOpen.Length!=Crates.Count)ResetPresentation();
            for(int i=0;i<Crates.Count;i++)
            {
                bool open=Crates[i].opened;
                if(open && !knownOpen[i])
                {
                    revealOrder[i]=0;
                    if(!Application.isBatchMode) RevealEffects.Play(crates[i].position+Vector3.up*.7f,dustMaterial);
                }
                knownOpen[i]=open;
                if(lids[i]!=null)lids[i].localRotation=Quaternion.Slerp(lids[i].localRotation,Quaternion.Euler(open?-110:0,0,0),Time.deltaTime*9);
            }
            for(int i=0;i<slots.Length;i++)
            {
                if(slots[i]==null)continue;
                slots[i].gameObject.SetActive(i<Stacks.Count);
                if(i<Stacks.Count && i<stackLabels.Length && stackLabels[i]!=null)
                {
                    var s=Stacks[i];
                    string labelText=catalog.entries[s.kind].title+"\n"+s.count+" / "+s.capacity;
                    if(stackLabels[i].text!=labelText)
                    {
                        stackLabels[i].text=labelText;
                        FitStackLabel(stackLabels[i]);
                    }
                    stackLabels[i].color=s.count>=s.capacity&&s.capacity>0?new Color(.3f,1,.55f):Color.white;
                }
            }
            int carryIndex=0;
            carryIndices.Clear();
            for(int i=0;i<Items.Count;i++)
            {
                var item=Items[i]; if(item.location==ItemLocation.Sealed)continue;
                ItemVisual view;
                if(!views.TryGetValue(item.id,out view))
                {
                    view=ItemVisual.Create(item,catalog.entries[item.kind],itemMaterial); views.Add(item.id,view);
                    StartRevealIfFresh(view,item);
                }
                Vector3 pos=item.position; Quaternion rot=item.location==ItemLocation.Stacked && item.slot>=0 ? StackRotation(item.slot,item.stackIndex) : Quaternion.Euler(0,item.yaw,0);
                float visualScale=item.location==ItemLocation.Stacked&&LayoutFor(item.slot)!=null?.85f:1;
                if(item.location==ItemLocation.Held && WarehousePlayer.Players.TryGetValue(item.holder,out var owner))
                {
                    carryIndices.TryGetValue(item.holder,out carryIndex); carryIndices[item.holder]=carryIndex+1;
                    int kind;int count=HeldCount(item.holder,out kind);
                    if(owner.Grip!=null)owner.Grip.ItemPose(carryIndex,count,view.ModelBounds,out pos,out rot,out visualScale);
                    else{pos=owner.transform.position+Vector3.up*1.1f+owner.transform.forward*.55f;rot=owner.transform.rotation;}
                }
                view.UpdateState(item,pos,rot,visualScale);
            }
        }
    }
}
