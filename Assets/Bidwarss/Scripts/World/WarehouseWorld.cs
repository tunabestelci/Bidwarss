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
        [Tooltip("Konteynerlerin içindeki ahşap kasalar (Tools > Bidwars > Loot > 2 menüsü doldurur). Her kasadan neredeyse her zaman 1 eşya çıkar.")]
        public Transform[] boxes;
        [Tooltip("Her ahşap kasanın türü (boxes ile aynı uzunlukta). Hangi eşyaların çıkabileceğini bu belirler.")]
        public CrateKind[] crateKinds;
        public TextMesh[] stackLabels;
        [Tooltip("Her kasada hangi eşya var (katalog anahtarı; Loot > 2 doldurur). Kasalar bu içeriğe göre boyutlanıp dizildi.")]
        public string[] crateItemKeys;
        [Tooltip("Ust uste konan kasalar: boxSupport[i] = i. kasanin ustunde durdugu kasa (yoksa -1). Loot > 2 doldurur; alttaki kasa bos olsa da ustteki doluysa gorunur kalir.")]
        public int[] boxSupport;
        bool[] holdsTop;
        [Range(1,600)] public int totalGroups = 12;
        [Tooltip("Sahnede kasa animasyonu yoksa bir kasayı açmak için E basılı tutma süresi (animasyonlu kasalarda kasanın kendi süresi geçerli: vida başına / bant kesme).")]
        [Range(.5f,8)] public float openSeconds = 3.4f;
        public NetworkList<ItemState> Items;
        public NetworkList<CrateState> Crates;   // ahşap kasalar (boxes)
        public NetworkList<CrateState> Doors;    // konteyner kapıları (crates)
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
        double[] holdStarted, doorHoldStarted;   // last server tick a player worked on the crate / door
        bool[] knownOpen;
        const float DoorSeconds = .8f;
        int[] knownStack;
        string viewedRun;
        float nextHoldUpdate;
        // Tool tuning (server side). Unlock thresholds live in Bidwarss.Domain.Upgrades.
        public const float MagnetRadius = 2.6f, MagnetReach = 6f, DroneRange = 14f;
        // What the local player is currently "working on": drives scanner glow and pallet beacons.
        public int GuideKind { get; private set; } = -1;
        public bool ScannerActive { get; private set; }
        bool[] guided = new bool[0];
        public bool IsGuided(int stack) { return stack >= 0 && stack < guided.Length && guided[stack]; }
        public GameRules Rules
        {
            get
            {
                if(boxes==null||boxes.Length==0)
                    throw new InvalidOperationException("Depoda ahşap kasa yok. Unity'de Tools > Bidwars > Loot > 2) Kasaları depoya yerleştir menüsünü çalıştır.");
                var kinds=CrateKindInts();
                // Random depot: every pallet of the scene is the pallet budget of a round (a stale totalGroups from an older
                // Loot > 2 run must not shrink the depot); the round itself uses 90-100% of it.
                int groups=totalGroups;
                if(catalog.randomRound&&slots!=null&&slots.Length>0)groups=Mathf.Clamp(slots.Length,1,GameRules.MaxStacks);
                var rules=catalog.CreateRules(boxes.Length, groups, kinds, kinds!=null&&Array.IndexOf(kinds,(int)CrateKind.Cardboard)>=0?(int)CrateKind.Cardboard:-1);
                rules.crateItems=catalog.CrateItemsFromKeys(crateItemKeys,boxes.Length);
                return rules;
            }
        }
        int[] CrateKindInts()
        {
            if(crateKinds==null||boxes==null||crateKinds.Length!=boxes.Length)return null;
            var kinds=new int[crateKinds.Length];
            for(int i=0;i<kinds.Length;i++)kinds[i]=(int)crateKinds[i];
            return kinds;
        }
        ICrateStage[] stages;
        // The wooden crate #box, if the scene has one.
        ICrateStage Stage(int box)
        {
            if(boxes==null||box<0||box>=boxes.Length||boxes[box]==null)return null;
            if(stages==null||stages.Length!=boxes.Length)stages=new ICrateStage[boxes.Length];
            var stage=stages[box];
            if(stage==null||(stage is UnityEngine.Object o&&o==null))
            {stage=boxes[box].GetComponentInChildren<ICrateStage>(true);stages[box]=stage;}
            return stage;
        }
        int CrateOrdinal(ItemState item)
        {
            int n=0;
            for(int i=0;i<item.id&&i<Items.Count;i++)if(Items[i].crate==item.crate)n++;
            return n;
        }
        public double Elapsed => StartedAt.Value <= 0 ? 0 : Math.Max(0, (Completed.Value ? FinishedAt.Value : NetworkManager.ServerTime.Time) - StartedAt.Value);
        public int OpenCount { get { int n=0; for(int i=0;i<Crates.Count;i++) if(Crates[i].opened)n++; return n; } }
        // Random depot: crates that hold something this round (the other reserved crates are hidden).
        public bool IsBoxActive(int box){return box>=0&&box<Crates.Count&&!Crates[box].inactive;}
        public int ActiveBoxCount { get { int n=0; for(int i=0;i<Crates.Count;i++) if(!Crates[i].inactive)n++; return n; } }
        // 1-based number of crate #box among the active crates (what the HUD shows).
        public int ActiveBoxNumber(int box){int n=0;for(int i=0;i<=box&&i<Crates.Count;i++)if(!Crates[i].inactive)n++;return n;}

        void Awake() { Items = new NetworkList<ItemState>(); Crates = new NetworkList<CrateState>(); Doors = new NetworkList<CrateState>(); Stacks = new NetworkList<StackState>(); }
        // Silinmis / bos palet referanslarini (ve etiketlerini) at: indeksler kayar ama her palet gecerli kalir.
        void CompactSlots()
        {
            if(slots==null)return;
            bool any=false; foreach(var t in slots) if(t==null){any=true;break;}
            if(!any)return;
            var s=new System.Collections.Generic.List<Transform>(); var l=new System.Collections.Generic.List<TextMesh>();
            for(int i=0;i<slots.Length;i++)
            {
                if(slots[i]==null)continue;
                s.Add(slots[i]); l.Add(stackLabels!=null&&i<stackLabels.Length?stackLabels[i]:null);
            }
            Debug.LogWarning("[Depo] "+(slots.Length-s.Count)+" palet referansi bostu, atlandi. Tools > Bidwars > Loot > 2 calistirinca eksik paletler yeniden eklenir.");
            slots=s.ToArray(); stackLabels=l.ToArray();
            // indeksler kaydi: palet hedeflerinin kimligi yeni sirayla
            for(int i=0;i<slots.Length;i++)
            {
                var it=slots[i].GetComponent<InteractionTarget>();
                if(it!=null&&it.kind==TargetKind.Slot)it.id=i;
            }
        }
        public override void OnNetworkSpawn()
        {
            CompactSlots();
            Instance = this;
            if(GetComponent<WarehouseGuides>()==null)gameObject.AddComponent<WarehouseGuides>();
            if(boxes!=null)
                for(int i=0;i<boxes.Length;i++)
                {
                    if(boxes[i]==null)continue;
                    var t=boxes[i].GetComponent<InteractionTarget>();
                    if(t==null)t=boxes[i].gameObject.AddComponent<InteractionTarget>();
                    t.kind=TargetKind.Box;t.id=i;
                }
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
            knownStack=new int[Stacks.Count];
            for(int i=0;i<Stacks.Count;i++) knownStack[i]=Stacks[i].count;
            FeedbackHub.ResetRun();
        }
        public void ServerStartRound(int seed)
        {
            if (!IsServer) return;
            var rules = Rules; rules.Validate();
            if(slots.Length < (rules.Manifest ? rules.ManifestStacks : totalGroups) || itemOrigins.Length != crates.Length || lids.Length != crates.Length)
                throw new InvalidOperationException("Depo sahnesini Bidwarss menüsünden V2 için yeniden oluştur.");
            if(boxes==null||boxes.Length!=rules.crateCount)
                throw new InvalidOperationException("Ahşap kasalar eksik: Tools > Bidwars > Loot > 2) Kasaları depoya yerleştir menüsünü çalıştır.");
            Engine = new RoundEngine(rules, seed);
            Completed.Value=false; StartedAt.Value=0; FinishedAt.Value=0; FinalDollars.Value=0;
            SecuredDollars.Value=0; PlacedCount.Value=0; PeakPlayers.Value=0; Seed.Value=seed;
            RulesHash.Value=Engine.RulesHash; TeamNames.Value=""; roster.Clear();
            Items.Clear(); Crates.Clear(); Doors.Clear(); Stacks.Clear();
            holdStarted=new double[boxes.Length]; doorHoldStarted=new double[crates.Length];
            for(int i=0;i<boxes.Length;i++)
            {
                bool dead=!Engine.IsActive(i);
                Vector3 shape=Vector3.zero; byte variant=0;
                if(!dead)variant=ChooseShape(i,seed,out shape);
                Crates.Add(new CrateState { opener=ItemState.Nobody, variant=variant, shape=shape, inactive=dead });
            }
            for(int i=0;i<crates.Length;i++) Doors.Add(new CrateState { opener=ItemState.Nobody });
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
        public string Hint(InteractionTarget target, ulong client)
        {
            if(Completed.Value) return "Depo tamamlandı! Sonuçlara TAB ile bak.";
            if(target==null) return "Kasaları 2 (levye), kolileri 3 (maket bıçağı) ile aç • Eşyaları türüne göre 10'lu istifle";
            int kind; int held=HeldCount(client,out kind);
            if(target.kind==TargetKind.Crate)
                return target.id>=Doors.Count||Doors[target.id].opened ? "" : "E BASILI TUT • Konteyner kapısını aç";
            if(target.kind==TargetKind.Box)
            {
                if(target.id>=Crates.Count)return "";
                bool cardboard=IsCardboard(target.id);
                if(Crates[target.id].opened)return ItemsInside(target.id)>0?"İçindeki eşyayı al (eşyaya bak, E)":(cardboard?"Boş koli":"Boş kasa");
                var me=WarehousePlayer.Local;
                if(held>0)return "Elin dolu • önce eşyayı istifle (Q: bırak)";
                int tool=ToolFor(target.id);
                if(me==null||me.Tool!=tool)return cardboard?"3: Maket bıçağını eline al":"2: Kasa açıcıyı eline al";
                return cardboard?"E BASILI TUT • Bandı kes":"E BASILI TUT • Vidaları sök";
            }
            if(target.kind==TargetKind.Slot)
            {
                if(target.id>=Stacks.Count) return "";
                var stack=Stacks[target.id];
                return catalog.entries[stack.kind].title+" • "+stack.count+"/"+stack.Cap+(held>0 ? (kind==stack.kind ? (stack.Full ? " • Palet dolu" : " • E: İstifle") : " • Farklı eşya türü") : "");
            }
            if(target.id<0 || target.id>=Items.Count)return "";
            var item=Items[target.id];
            return "E: Al"+(Upgrades.Unlocked(Upgrade.Magnet,PlacedCount.Value,Items.Count)?" • Mıknatıs yakındaki aynı türü de çeker":"");
        }
        void Update()
        {
            if(!IsSpawned || !IsServer || Engine==null || Completed.Value)return;
            if(StartedAt.Value>0) TrackRoster();
            if(Time.unscaledTime<nextHoldUpdate)return;
            nextHoldUpdate=Time.unscaledTime+.05f;
            HoldLoop(Doors,doorHoldStarted,false);
            HoldLoop(Crates,holdStarted,true);
        }
        // Which tool opens crate #box: the box cutter for a taped cardboard box, the crate opener for a wooden crate.
        public bool IsCardboard(int box)=>crateKinds!=null&&box>=0&&box<crateKinds.Length&&crateKinds[box]==CrateKind.Cardboard;
        public int ToolFor(int box)=>IsCardboard(box)?WarehousePlayer.CutterSlot:WarehousePlayer.OpenerSlot;
        // Loose items still lying inside crate #box.
        public int ItemsInside(int box)
        {int n=0;for(int i=0;i<Items.Count;i++)if(Items[i].crate==box&&Items[i].location==ItemLocation.Loose&&Items[i].pose!=0)n++;return n;}
        // Shape of crate #box for this round: it is fitted to what is inside (a parcel of 3-5 things, or one big thing).
        byte ChooseShape(int box,int seed,out Vector3 shape)
        {
            shape=Vector3.zero;
            var stage=Stage(box); if(stage==null)return 0;
            var cells=new List<Vector3>();
            foreach(var item in Engine.Items) if(item.crate==box) cells.Add(CellOf(box,item.kind));
            return (byte)Mathf.Clamp(stage.ChooseShape(cells.ToArray(),unchecked(seed*31+box*7919),out shape),0,255);
        }
        // Does this item lie down in crate #box? Parcels: tall things. Wooden: only in the lying crate.
        bool LiesIn(int box,int kind)
        {
            if(IsCardboard(box))return CratePacking.LiesInParcel(ItemSize(kind));
            return crateKinds!=null&&box<crateKinds.Length&&crateKinds[box]==CrateKind.Lying&&catalog!=null&&kind>=0&&kind<catalog.entries.Length&&catalog.entries[kind].lyingOK;
        }
        // 1 as modelled, 2 on its back, 3 on its side (CratePacking.Pose): laid down things rest on their largest face.
        byte PoseIn(int box,int kind){return CratePacking.Pose(ItemSize(kind),LiesIn(box,kind));}
        Vector3 CellOf(int box,int kind){return CratePacking.CellFor(ItemSize(kind),PoseIn(box,kind));}
        // Where the tool works on crate #box right now (for the first-person hand animation).
        public bool WorkPoint(int box,out Vector3 point,out Vector3 along,out Vector3 normal)
        {
            var stage=Stage(box);
            if(stage==null){point=along=normal=Vector3.zero;return false;}
            return stage.WorkPoint(out point,out along,out normal);
        }
        public int WorkSteps(int box){var stage=Stage(box);return stage!=null?Mathf.Max(1,stage.WorkSteps):1;}
        // Seconds of holding E that open crate #box (screws one by one / the whole tape).
        float WorkSeconds(int box)
        {
            var stage=Stage(box);
            float s=stage!=null?stage.WorkSeconds:openSeconds;
            return Mathf.Max(.2f,float.IsNaN(s)?openSeconds:s);
        }
        // Hold-E progress for container doors (bare hands) and crates (crate opener / box cutter), server side.
        // Letting go keeps the progress: whoever holds E on it next continues from there (screw 3 stays screw 3).
        void HoldLoop(NetworkList<CrateState> list,double[] started,bool box)
        {
            double now=NetworkManager.ServerTime.Time;
            for(int i=0;i<list.Count&&i<started.Length;i++)
            {
                var state=list[i]; if(state.opened||state.inactive)continue;
                WarehousePlayer owner=null;
                if(state.opener!=ItemState.Nobody) WarehousePlayer.Players.TryGetValue(state.opener,out owner);
                if(!ValidOpener(owner,i,box))
                {
                    state.opener=ItemState.Nobody; started[i]=0;
                    foreach(var candidate in WarehousePlayer.Players.Values)
                        if(ValidOpener(candidate,i,box)) { owner=candidate; state.opener=owner.OwnerClientId; started[i]=now; break; }
                }
                if(state.opener!=ItemState.Nobody)
                {
                    if(StartedAt.Value<=0) { StartedAt.Value=now; TrackRoster(); }
                    double step=Math.Min(.25,Math.Max(0,now-started[i])); started[i]=now;
                    state.progress=Mathf.Clamp01(state.progress+(float)(step/(box?WorkSeconds(i):DoorSeconds)));
                    if(state.progress>=1)
                    {
                        if(!box){ state.opened=true; state.openedAt=now; state.opener=ItemState.Nobody; }
                        else
                        {
                            string error;
                            if(Engine.Open(i,out error))
                            {
                                state.opened=true; state.openedAt=now; state.opener=ItemState.Nobody;
                                int count=0,ordinal=0;
                                foreach(var item in Engine.Items) if(item.crate==i) count++;
                                foreach(var item in Engine.Items) if(item.crate==i)
                                {
                                    float yaw;byte pose;
                                    Vector3 at=RestInside(ordinal,count,i,item.kind,out yaw,out pose);
                                    Items[item.id]=ToState(item,at,yaw,pose); ordinal++;
                                }
                            }
                        }
                    }
                }
                if(!state.Equals(list[i]))list[i]=state;
            }
        }
        bool ValidOpener(WarehousePlayer player,int index,bool box)
        {
            int kind;
            if(player==null||!player.InputFresh||Engine.HeldCount(player.OwnerClientId,out kind)!=0)return false;
            if(box)return player.WantsBox==index && player.Tool==ToolFor(index) && player.ServerLooksAt(TargetKind.Box,index);
            return player.WantsDoor==index && player.ServerLooksAt(TargetKind.Crate,index);
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
        // Where an item rests once crate #box is open: INSIDE the crate, on its inner floor. Lying things lie on their back.
        // The long side of the item follows the long side of the crate; several items (a parcel) stand side by side.
        Vector3 RestInside(int n,int count,int box,int kind,out float yaw,out byte pose)
        {
            Vector3 size=ItemSize(kind);
            yaw=0;pose=1;
            var stage=Stage(box);
            Vector3 floor,axis;float length,width,height;
            if(stage==null||!stage.Interior(out floor,out axis,out length,out width,out height))
            {
                floor=boxes[box]!=null?boxes[box].position+Vector3.up*.15f:Vector3.zero;axis=boxes[box]!=null?boxes[box].right:Vector3.right;
                length=width=height=1f;
            }
            pose=PoseIn(box,kind);
            Vector3 cell=CratePacking.CellFor(size,pose);
            // the item's longest horizontal side follows the crate's long side
            yaw=CratePacking.AlongForward(size,pose)?Mathf.Atan2(axis.x,axis.z)*Mathf.Rad2Deg:Mathf.Atan2(-axis.z,axis.x)*Mathf.Rad2Deg;
            Vector3 across=Vector3.Cross(Vector3.up,axis).normalized;
            Vector3 offset=Vector3.zero;
            if(count>1)
            {
                if(IsCardboard(box))offset=across*CratePacking.ParcelOffset(cell,n,count);
                else{var g=CratePacking.GridOffset(cell,n,count);offset=axis*g.x+across*g.y;}
            }
            return floor+offset+Vector3.up*(cell.y*.5f+.004f);
        }
        // Real-world size of an item type (metres). The visual scales its model to this while the item lies loose.
        public Vector3 ItemSize(int kind)
        {
            if(catalog==null||kind<0||kind>=catalog.entries.Length)return new Vector3(.4f,.4f,.4f);
            var s=catalog.entries[kind].realSize;
            return new Vector3(Mathf.Max(.05f,s.x),Mathf.Max(.05f,s.y),Mathf.Max(.05f,s.z));
        }
        // Two rows of five objects. Rotation follows the pallet, independently of its model scale.
        // A pallet has ten cells. Small things share a cell (2 or 2x2 per cell, pallet capacity 20 / 40): cell = index % 10,
        // the sub-position inside the cell = index / 10. Big things use one cell each.
        public Vector3 StackPosition(int stack,int index)
        {
            var layout=slots[stack].GetComponent<StackLayout>();
            int cell=index%10,sub=index/10;
            int cap=Engine!=null&&stack<Engine.StackCount?Engine.StackCapacity(stack):GameRules.StackSize;
            int subs=(cap+9)/10;
            Vector3 center=layout!=null?layout.Center(cell):slots[stack].position+slots[stack].rotation*new Vector3((cell%2-.5f)*.47f,.30f,(cell/2-2)*.40f);
            if(subs>1)
            {
                Quaternion r=layout!=null?layout.Rotation(cell):slots[stack].rotation;
                Vector3 offset=subs==2?new Vector3((sub==0?-1:1)*.11f,0,0):new Vector3(((sub&1)==0?-1:1)*.11f,0,((sub&2)==0?-1:1)*.085f);
                center+=r*offset+Vector3.down*.07f;
            }
            return center;
        }
        Quaternion StackRotation(int stack,int index)
        {var layout=slots[stack].GetComponent<StackLayout>();return layout!=null?layout.Rotation(index%10):slots[stack].rotation;}
        ItemState ToState(RoundItem item,Vector3 position,float yaw=0,byte pose=0)
        {
            bool sealedItem=item.location==ItemLocation.Sealed;
            bool loose=item.location==ItemLocation.Loose;
            return new ItemState { id=item.id,kind=sealedItem?-1:item.kind,crate=item.crate,
                condition=sealedItem?ItemCondition.Terrible:item.condition,dollars=sealedItem?0:item.dollars,
                location=item.location,holder=item.holder,slot=item.stack,stackIndex=item.stackIndex,position=position,
                yaw=loose?yaw:0,pose=loose?pose:(byte)0 };
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
                { Items[id]=ToState(Engine.Items[id],Items[id].position); MagnetPull(player,id); PublishProgress(); }
            }
            else if(kind==TargetKind.Slot)
            {
                if(Engine.Place(id,player.OwnerClientId,out error)>0)PublishPlacement(id);
            }
            if(!string.IsNullOrEmpty(error))player.Feedback(error);
        }
        void PublishPlacement(int stack)
        {
            foreach(var item in Engine.Items) if(item.stack==stack) Items[item.id]=ToState(item,StackPosition(stack,item.stackIndex));
            PublishProgress();
        }
        // Mıknatıs Eldiven: taking one item also pulls the loose items of the same type lying right next to it.
        void MagnetPull(WarehousePlayer player,int anchorId)
        {
            if(!Upgrades.Unlocked(Upgrade.Magnet,Engine.PlacedCount,Engine.Items.Count))return;
            ulong client=player.OwnerClientId;
            int kind;int held=Engine.HeldCount(client,out kind);
            if(held==0||held>=GameRules.StackSize)return;
            Vector3 anchor=Items[anchorId].position,eye=player.transform.position;
            var candidates=new List<int>();
            for(int i=0;i<Items.Count;i++)
            {
                var state=Items[i];
                if(state.location!=ItemLocation.Loose||state.kind!=kind)continue;
                if((state.position-anchor).sqrMagnitude>MagnetRadius*MagnetRadius||(state.position-eye).sqrMagnitude>MagnetReach*MagnetReach)continue;
                candidates.Add(i);
            }
            candidates.Sort((a,b)=>(Items[a].position-anchor).sqrMagnitude.CompareTo((Items[b].position-anchor).sqrMagnitude));
            int pulled=0;string ignored;
            foreach(int i in candidates)
            {
                if(!Engine.Take(i,client,out ignored))break;
                Items[i]=ToState(Engine.Items[i],Items[i].position);pulled++;
            }
            if(pulled>0)player.Feedback("Mıknatıs: +"+pulled+" eşya");
        }
        // Depo Dronu: sends everything in your hands to the nearest matching pallet that still has room.
        public void AutoPlace(WarehousePlayer player)
        {
            if(!IsServer||Engine==null||Completed.Value)return;
            if(!Upgrades.Unlocked(Upgrade.Drone,Engine.PlacedCount,Engine.Items.Count))
            {player.Feedback("Depo Dronu kilitli: eşyaların %"+Upgrades.UnlockPercent[(int)Upgrade.Drone]+"'i yerleşince açılır.");return;}
            int kind;int held=Engine.HeldCount(player.OwnerClientId,out kind);
            if(held==0){player.Feedback("Elinde eşya yok.");return;}
            int best=-1;float bestDistance=DroneRange*DroneRange;
            for(int s=0;s<Engine.StackCount;s++)
            {
                if(Engine.StackKind(s)!=kind||Engine.StackCountAt(s)>=Engine.StackCapacity(s))continue;
                float d=(slots[s].position-player.transform.position).sqrMagnitude;
                if(d<bestDistance){bestDistance=d;best=s;}
            }
            if(best<0){player.Feedback("Yakında ("+DroneRange+" m) boş "+catalog.entries[kind].title+" paleti yok.");return;}
            string error;
            if(Engine.Place(best,player.OwnerClientId,out error)>0)PublishPlacement(best);
            else if(!string.IsNullOrEmpty(error))player.Feedback(error);
        }
        public void Drop(WarehousePlayer player)
        {
            if(!IsServer || Engine==null || Completed.Value)return;
            int id=-1;
            for(int i=0;i<Items.Count;i++)if(Items[i].holder==player.OwnerClientId)id=i;
            if(id<0)return;
            Vector3 size=ItemSize(Items[id].kind);
            Vector3 center=player.transform.position+player.transform.forward*(.9f+Mathf.Max(size.x,size.z)*.5f);
            if(!Physics.Raycast(center+Vector3.up*.8f,Vector3.down,out var floor,2.5f,~0,QueryTriggerInteraction.Ignore))
            {player.Feedback("Burada esyayi birakacak zemin yok.");return;}
            center=floor.point+Vector3.up*(size.y*.5f);
            Physics.SyncTransforms();
            foreach(var hit in Physics.OverlapBox(center+Vector3.up*.05f,new Vector3(Mathf.Max(.2f,size.x*.4f),Mathf.Max(.15f,size.y*.4f),Mathf.Max(.2f,size.z*.4f)),Quaternion.identity,~0,QueryTriggerInteraction.Ignore))
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
                float lift=ItemSize(Engine.Items[id].kind).y*.5f;
                Vector3 pos=recoveryOrigin!=null
                    ?recoveryOrigin.position+recoveryOrigin.rotation*new Vector3((id%Math.Max(1,recoveryColumns))*1.4f,lift,-(id/Math.Max(1,recoveryColumns))*1.4f)
                    :new Vector3(-12f+(id%40)*1.4f,lift,-13f-(id/40)*1.4f);
                Items[id]=ToState(Engine.Items[id],pos);
            }
        }
        // Which pallets should glow for the local player: the ones that want what is in their hands, or,
        // with the scanner on, what they are looking at.
        void UpdateGuide()
        {
            if(guided.Length!=Stacks.Count)guided=new bool[Stacks.Count];
            for(int i=0;i<guided.Length;i++)guided[i]=false;
            GuideKind=-1;ScannerActive=false;
            var local=WarehousePlayer.Local;
            if(local==null||Completed.Value)return;
            int kind;
            bool holding=HeldCount(local.OwnerClientId,out kind)>0;
            if(holding)GuideKind=kind;
            else if(local.Looked!=null&&local.Looked.kind==TargetKind.Item&&local.Looked.id>=0&&local.Looked.id<Items.Count)
            {
                var looked=Items[local.Looked.id];
                if(looked.location!=ItemLocation.Sealed)GuideKind=looked.kind;
            }
            ScannerActive=local.ScannerOn&&Upgrades.Unlocked(Upgrade.Scanner,PlacedCount.Value,Items.Count);
            if(GuideKind<0||!(holding||ScannerActive))return;
            for(int i=0;i<guided.Length;i++)
                guided[i]=Stacks[i].kind==GuideKind&&!Stacks[i].Full;
        }
        void LateUpdate()
        {
            if(!IsSpawned)return;
            // Older generated scenes gain a usable target on the entire pallet without regeneration.
            for(int i=0;i<slots.Length;i++)
                if(slots[i]!=null && slots[i].GetComponent<InteractionTarget>()==null)
                {var target=slots[i].gameObject.AddComponent<InteractionTarget>();target.kind=TargetKind.Slot;target.id=i;}
            if(viewedRun!=RunId.Value.ToString() || knownOpen==null || knownOpen.Length!=Crates.Count || knownStack==null || knownStack.Length!=Stacks.Count)ResetPresentation();
            UpdateGuide();
            for(int i=0;i<Crates.Count&&i<knownOpen.Length;i++)knownOpen[i]=Crates[i].opened;
            // Random depot: crates that hold nothing this round are hidden (their stage resets itself when it is shown again).
            if(boxes!=null)
            {
                if(holdsTop==null||holdsTop.Length!=boxes.Length)holdsTop=new bool[boxes.Length];
                System.Array.Clear(holdsTop,0,holdsTop.Length);
                if(boxSupport!=null&&boxSupport.Length==boxes.Length)
                    for(int j=0;j<boxes.Length&&j<Crates.Count;j++)
                        if(boxSupport[j]>=0&&boxSupport[j]<boxes.Length&&!Crates[j].inactive)holdsTop[boxSupport[j]]=true;
                for(int i=0;i<boxes.Length&&i<Crates.Count;i++)
                {
                    if(boxes[i]==null)continue;
                    bool on=!Crates[i].inactive||holdsTop[i];
                    if(boxes[i].gameObject.activeSelf!=on)boxes[i].gameObject.SetActive(on);
                }
            }
            for(int i=0;i<slots.Length;i++)
            {
                if(slots[i]==null)continue;
                slots[i].gameObject.SetActive(i<Stacks.Count);
                if(i<Stacks.Count)
                {
                    var s=Stacks[i];
                    if(s.Full && knownStack[i]<s.Cap && !Application.isBatchMode)
                        FeedbackHub.StackCompleted(slots[i].position+Vector3.up*.5f,catalog.entries[s.kind].title);
                    knownStack[i]=s.count;
                }
                if(i<Stacks.Count && stackLabels!=null && i<stackLabels.Length && stackLabels[i]!=null)
                {
                    var s=Stacks[i]; stackLabels[i].text=catalog.entries[s.kind].title+"\n"+s.count+" / "+s.Cap;
                    stackLabels[i].color=s.Full?new Color(.3f,1,.55f):(IsGuided(i)?Color.Lerp(Color.white,new Color(.35f,1,.9f),.5f+.5f*Mathf.Sin(Time.time*6f)):Color.white);
                }
            }
            int carryIndex=0;
            var carryIndices=new Dictionary<ulong,int>();
            for(int i=0;i<Items.Count;i++)
            {
                var item=Items[i]; if(item.location==ItemLocation.Sealed)continue;
                ItemVisual view;
                if(!views.TryGetValue(item.id,out view))
                {
                    // The crate is still being opened: the item inside stays hidden until the lid / flaps are off.
                    // Then it simply is there, inside the crate; the players take it out themselves.
                    var stage=item.location==ItemLocation.Loose&&item.pose!=0?Stage(item.crate):null;
                    if(stage!=null&&!stage.ItemsReleased)continue;
                    view=ItemVisual.Create(item,catalog.entries[item.kind],itemMaterial,dustMaterial); views.Add(item.id,view);
                }
                float carryScale=1;
                Vector3 pos=item.position; Quaternion rot=item.location==ItemLocation.Stacked && item.slot>=0 ? StackRotation(item.slot,item.stackIndex) : Quaternion.Euler(0,item.yaw,0)*(item.location==ItemLocation.Loose?CratePacking.PoseRotation(item.pose):Quaternion.identity);
                if(item.location==ItemLocation.Held && WarehousePlayer.Players.TryGetValue(item.holder,out var owner))
                {
                    carryIndices.TryGetValue(item.holder,out carryIndex); carryIndices[item.holder]=carryIndex+1;
                    view.CarryIndex=carryIndex;
                    int heldKind;int heldCount=HeldCount(item.holder,out heldKind);
                    if(owner.Grip!=null)owner.Grip.ItemPose(carryIndex,heldCount,view.ModelBounds,out pos,out rot,out carryScale);
                    else {pos=owner.transform.position+Vector3.up*1.04f+owner.transform.forward*.54f;rot=owner.transform.rotation;}
                }
                view.SetHighlight(ScannerActive&&GuideKind>=0&&item.kind==GuideKind&&item.location==ItemLocation.Loose?1f:0f);
                view.UpdateState(item,pos,rot,item.location==ItemLocation.Stacked&&slots[item.slot].GetComponent<StackLayout>()!=null?(item.slot<Stacks.Count&&Stacks[item.slot].Cap>GameRules.StackSize?.45f:.85f):carryScale);
            }
        }
    }
}
