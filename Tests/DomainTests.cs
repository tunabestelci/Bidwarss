using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Bidwarss.Domain;

public static class DomainTests
{
    static int checks;
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    static GameRules Rules()=>new GameRules {items=new[]{
        new ItemRule{key="mirror",baseDollars=80},new ItemRule{key="table",baseDollars=100},
        new ItemRule{key="chair",baseDollars=60},new ItemRule{key="radio",baseDollars=120},new ItemRule{key="lamp",baseDollars=50}}};
    // The shipped market (Assets/Bidwarss/Data/ItemCatalog.json), turned into rules the way ItemCatalog.CreateRules does.
    static GameRules MarketRules(out JsonElement items)
    {
        string dir=AppContext.BaseDirectory,path=null;
        while(dir!=null&&path==null){var candidate=Path.Combine(dir,"Assets","Bidwarss","Data","ItemCatalog.json");if(File.Exists(candidate))path=candidate;else dir=Path.GetDirectoryName(dir);}
        Check(path!=null,"Market catalog JSON found");
        var root=JsonDocument.Parse(File.ReadAllText(path)).RootElement;
        items=root.GetProperty("items");
        var rules=new GameRules{items=items.EnumerateArray().Select(e=>new ItemRule{
            key=e.GetProperty("key").GetString(),baseDollars=e.GetProperty("baseDollars").GetInt32(),
            selectionWeight=e.GetProperty("selectionWeight").GetInt32(),maxGroups=e.GetProperty("maxGroups").GetInt32(),
            minDollars=e.GetProperty("minDollars").EnumerateArray().Select(v=>v.GetInt32()).ToArray(),
            maxDollars=e.GetProperty("maxDollars").EnumerateArray().Select(v=>v.GetInt32()).ToArray()}).ToArray()};
        var weights=root.GetProperty("conditions").EnumerateArray().Select(c=>c.GetProperty("weight").GetInt32()).ToArray();
        for(int i=0;i<7;i++)rules.conditions[i].weight=weights[i];
        return rules;
    }
    static void MarketTests()
    {
        JsonElement json;var market=MarketRules(out json);market.Validate();
        Check(market.items.Length>=40,"Whole site catalog shipped");
        var elements=json.EnumerateArray().ToArray();
        for(int k=0;k<market.items.Length;k++)
        {
            var item=market.items[k];int coll=elements[k].GetProperty("collector").GetInt32();
            int mid,midMax;market.PriceBand(k,ItemCondition.Average,out mid,out midMax);
            Check(mid<=item.baseDollars&&item.baseDollars<=midMax,"Average condition brackets the market value: "+item.key);
            Check(!string.IsNullOrWhiteSpace(elements[k].GetProperty("owned").GetString()),"Every item has a possessed form for famous owners: "+item.key);
            int topMin,topMax,bottomMax,bottomMin;
            market.PriceBand(k,ItemCondition.Legendary,out topMin,out topMax);market.PriceBand(k,ItemCondition.Terrible,out bottomMin,out bottomMax);
            Check(bottomMax<mid,"Terrible is cheaper than Average: "+item.key);
            if(coll>=6)Check(topMin>=8*item.baseDollars,"Collectibles are worth far more at the top: "+item.key);
            if(coll==0)Check(topMax<=8*item.baseDollars,"Mass-produced goods cannot become treasures: "+item.key);
        }
        // The reported bugs: an antique must out-price plain goods in every condition.
        int Index(string key)=>Array.FindIndex(market.items,x=>x.key==key);
        int clock=Index("boy-saati"),mill=Index("antika-biber-degirmeni"),bin=Index("cop-kovasi"),table=Index("vintage-masa");
        Check(clock>=0&&mill>=0&&bin>=0&&table>=0,"Reference items exist");
        Check(market.items[mill].baseDollars<market.items[clock].baseDollars,"A pepper mill is not worth more than a grandfather clock");
        int lo,hi,lo2,hi2;
        market.PriceBand(clock,ItemCondition.Terrible,out lo,out hi);market.PriceBand(bin,ItemCondition.Legendary,out lo2,out hi2);
        Check(lo>hi2/3,"Even a wrecked antique clock is not trash-can money");
        market.PriceBand(table,ItemCondition.Legendary,out lo,out hi);
        Check(lo>=1500,"A legendary vintage table is worth real money");
        // Every drawn price stays inside its item's band; the same seed gives the same depot.
        long total=0;
        for(int seed=0;seed<400;seed++)
        {
            var round=new RoundEngine(market,seed);
            Check(round.Items.Count==120&&round.Items.GroupBy(x=>x.kind).Count()==12,"Twelve distinct types per depot");
            foreach(var item in round.Items)
            {
                int a,b;market.PriceBand(item.kind,item.condition,out a,out b);
                Check(item.dollars>=a&&item.dollars<=b,"Price inside its condition band");
            }
            total+=round.TotalDollars;
        }
        Check(total/400>5000&&total/400<60000,"Average depot value is sane: "+total/400);
        var again=new RoundEngine(market,77);var twin=new RoundEngine(market,77);
        Check(again.Items.Select(x=>x.kind+":"+x.dollars).SequenceEqual(twin.Items.Select(x=>x.kind+":"+x.dollars)),"Market depots reproduce from a seed");
        // Selection weights: of many types only a few fit, and heavy ones appear far more often.
        var weighted=new GameRules{totalGroups=3,items=Enumerable.Range(0,20).Select(i=>new ItemRule{key="t"+i,baseDollars=50,selectionWeight=i<2?200:1,maxGroups=2}).ToArray()};
        int heavy=0,light=0;
        for(int seed=0;seed<300;seed++)foreach(var kind in new RoundEngine(weighted,seed).Items.Select(x=>x.kind).Distinct()){if(kind<2)heavy++;else light++;}
        Check(heavy>400&&light<500,"Heavy types dominate when there are more types than groups: "+heavy+"/"+light);
        // Invalid or overlapping markets are rejected, and balance changes re-key the ranking board.
        var broken=MarketRules(out json);broken.items[0].minDollars[3]=broken.items[0].maxDollars[2];
        bool threw=false;try{broken.Validate();}catch(ArgumentException){threw=true;}Check(threw,"Overlapping condition bands rejected");
        var short6=MarketRules(out json);short6.items[1].minDollars=short6.items[1].minDollars.Take(6).ToArray();
        threw=false;try{short6.Validate();}catch(ArgumentException){threw=true;}Check(threw,"Incomplete market rejected");
        var repriced=MarketRules(out json);repriced.items[2].maxDollars[6]+=10;
        Check(repriced.Fingerprint()!=market.Fingerprint(),"Repricing changes the rules hash");
    }
    static void ProvenanceTests()
    {
        Check(!Provenance.Applies(ItemCondition.Good)&&Provenance.Applies(ItemCondition.VeryGood)&&Provenance.Applies(ItemCondition.Legendary),"Only epic and legendary finds get a famous owner");
        Check(Provenance.Pick(5,3,ItemCondition.Average)==Provenance.None,"Ordinary condition has no owner");
        var names=new HashSet<string>();
        for(int i=0;i<Provenance.NameCount;i++){var n=Provenance.Owner(i);Check(n.Length>3&&names.Add(n),"Owner names are unique: "+n);}
        Check(Provenance.Owner(-1)==""&&Provenance.Owner(Provenance.NameCount)=="","Out-of-range owner is empty");
        // Turkish genitive: vowel harmony and the buffer n after a final vowel.
        Check(Provenance.Genitive("Mira Starling")=="Mira Starling'in","Genitive i -> in");
        Check(Provenance.Genitive("Luna Kozmo")=="Luna Kozmo'nun","Genitive o + final vowel -> nun");
        Check(Provenance.Genitive("Naz Cascade")=="Naz Cascade'nin","Genitive e + final vowel -> nin");
        Check(Provenance.Genitive("Bora Halcyon")=="Bora Halcyon'un","Genitive o -> un");
        Check(Provenance.Genitive("Selin Aurelio")=="Selin Aurelio'nun","Genitive trailing o -> nun");
        Check(Provenance.Genitive("Ayla Karaman")=="Ayla Karaman'ın","Genitive a -> ın");
        Check(Provenance.Genitive("Deniz Yüce")=="Deniz Yüce'nin","Genitive ü/e -> nin");
        Check(Provenance.Genitive("Deniz Yüz")=="Deniz Yüz'ün","Genitive ü -> ün");
        string t=Provenance.Title("spor motosikleti",0);
        Check(t==Provenance.Genitive(Provenance.Owner(0))+" Spor Motosikleti","Title joins owner and possessed item: "+t);
        Check(Provenance.Title("ışık lambası",0).EndsWith(" Işık Lambası"),"Turkish capital I");
        Check(Provenance.Title("iğne kutusu",0).EndsWith(" İğne Kutusu"),"Turkish capital İ");
        Check(Provenance.Title("",3)==""&&Provenance.Title("x",-1)=="x","Missing owner or form falls back");
        // In a round: only epic/legendary items carry an owner, deterministically, and the draw does not disturb the economy.
        var rules=Rules();int stars=0;var owners=new HashSet<int>();
        for(int seed=0;seed<300;seed++)
        {
            var a=new RoundEngine(rules,seed);var b=new RoundEngine(rules,seed);
            for(int i=0;i<a.Items.Count;i++)
            {
                var x=a.Items[i];
                Check(x.star==b.Items[i].star,"Same seed, same owners");
                if(x.condition>=ItemCondition.VeryGood){Check(x.star>=0&&x.star<Provenance.NameCount,"Epic/legendary item has an owner");stars++;owners.Add(x.star);}
                else Check(x.star==Provenance.None,"Ordinary item has no owner");
            }
        }
        Check(stars>2000,"Epic and legendary finds appear: "+stars);
        Check(owners.Count>100,"Owners vary across rounds: "+owners.Count);
    }
    public static void Main()
    {
        var rules=Rules();string error;
        for(int seed=-1000;seed<1000;seed++)
        {
            var round=new RoundEngine(rules,seed);
            Check(round.Items.Count==120,"Budget must be exact");
            foreach(var group in round.Items.GroupBy(x=>x.kind))
            {
                Check(group.Count()%10==0,"Per-type totals must be divisible by ten");
                Check(group.Count()<=rules.items[group.Key].maxGroups*10,"Type cap exceeded");
            }
            Check(round.Items.Select(x=>x.id).Distinct().Count()==120,"Unique ids");
            Check(round.Items.GroupBy(x=>x.crate).All(g=>g.Count()==12),"Balanced boxes");
            foreach(var item in round.Items)
            {
                var band=rules.conditions[(int)item.condition];
                Check(item.dollars>=rules.items[item.kind].baseDollars*band.minimumPercent/100 && item.dollars<=rules.items[item.kind].baseDollars*band.maximumPercent/100,"Condition price bounds");
            }
        }
        var a=new RoundEngine(rules,11);var b=new RoundEngine(rules,11);
        Check(a.Items.Select(x=>x.kind+":"+x.dollars+":"+x.crate).SequenceEqual(b.Items.Select(x=>x.kind+":"+x.dollars+":"+x.crate)),"Seed reproducibility");
        Check(!a.Take(0,0,out error),"Sealed item cannot be picked up");
        Check(a.Open(0,out error),"Open once");Check(!a.Open(0,out error),"Duplicate open rejected");
        Check(a.Take(0,0,out error),"Host client id zero supported");Check(!a.Take(0,1,out error),"Double pickup rejected");
        Check(!a.Drop(0,1),"Other player cannot drop held item");
        a.Disconnect(0);Check(a.Items[0].location==ItemLocation.Loose,"Disconnect releases item");
        for(int c=1;c<a.CrateCount;c++)a.Open(c,out error);
        int first=a.Items[0].kind;
        foreach(var item in a.Items.Where(x=>x.kind==first).Take(10))Check(a.Take(item.id,1,out error),"Collect ten");
        int foreign=a.Items.First(x=>x.kind!=first).id;
        Check(!a.Take(foreign,1,out error),"Mixed carry rejected");
        var extra=a.Items.FirstOrDefault(x=>x.kind==first&&x.location==ItemLocation.Loose);
        if(extra!=null)Check(!a.Take(extra.id,1,out error),"Carry cap");
        int correct=Enumerable.Range(0,a.StackCount).First(s=>a.StackKind(s)==first);
        int wrong=Enumerable.Range(0,a.StackCount).First(s=>a.StackKind(s)!=first);
        Check(a.Place(wrong,1,out error)==0,"Wrong category placement rejected");
        Check(a.Place(correct,1,out error)==10,"Deposit complete bundle");
        var placed=a.Items.Where(x=>x.stack==correct).OrderBy(x=>x.stackIndex).ToArray();
        Check(!a.Take(placed[0].id,2,out error),"Cannot remove middle of stack");
        Check(a.Take(placed[9].id,2,out error),"Can retrieve top item");
        Check(a.PlacedCount==9,"Retrieve updates progress");
        Check(a.Place(correct,2,out error)==1,"Return item");
        Check(a.Place(correct,2,out error)==0,"Duplicate placement has no payout");
        for(int stack=0;stack<a.StackCount;stack++)
        {
            foreach(var item in a.Items.Where(x=>x.kind==a.StackKind(stack)&&x.location==ItemLocation.Loose).Take(10).ToArray())a.Take(item.id,3,out error);
            a.Place(stack,3,out error);
            // A full stack can leave carried items; place them in the next matching stack.
            int kind;if(a.HeldCount(3,out kind)>0)
            {
                int dest=Enumerable.Range(0,a.StackCount).First(s=>a.StackKind(s)==kind&&a.StackCountAt(s)<10);
                a.Place(dest,3,out error);
            }
        }
        Check(a.Completed,"Complete only when all items and stacks done");
        Check(a.FinalDollars==a.Items.Sum(x=>x.dollars),"Exact payout sum");
        int paid=a.FinalDollars;
        Check(!a.Take(0,0,out error)&&a.Place(0,0,out error)==0&&a.FinalDollars==paid,"Finished run immutable");
        var invalid=Rules();invalid.totalGroups=30;
        bool threw=false;try{new RoundEngine(invalid,1);}catch(ArgumentException){threw=true;}Check(threw,"Impossible capacity rejected");
        var constrained=Rules();constrained.totalGroups=5;foreach(var i in constrained.items)i.maxGroups=1;
        var limited=new RoundEngine(constrained,8);Check(limited.Items.GroupBy(x=>x.kind).All(g=>g.Count()==10),"Exact caps");
        var changed=Rules();changed.items[0].baseDollars++;
        Check(changed.Fingerprint()!=rules.Fingerprint(),"Changed balance uses different ranking board");
        MarketTests();
        ProvenanceTests();
        Console.WriteLine("PASS: "+checks+" assertions; 2000 generated scenarios; inventory, races, completion, pricing and limits.");
    }
}
