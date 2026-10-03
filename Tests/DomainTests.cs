using System;
using System.Collections.Generic;
using System.Linq;
using Bidwarss.Domain;

public static class DomainTests
{
    static int checks;
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    static GameRules Rules()=>new GameRules {items=new[]{
        new ItemRule{key="mirror",baseDollars=80},new ItemRule{key="table",baseDollars=100},
        new ItemRule{key="chair",baseDollars=60},new ItemRule{key="radio",baseDollars=120},new ItemRule{key="lamp",baseDollars=50}}};
    static void CheckCarryAndTools()
    {
        for(int count=1;count<=10;count++)
        {
            var layout=new CarryLayout(count);
            Check(layout.Width<=.61f && layout.Depth<=.45f,"Carry bundle must fit in arms");
            for(int i=0;i<count;i++)
            {
                layout.Position(i,out float x,out float y,out float z);
                Check(y>=0 && y+.42f*layout.Scale<=.45f,"Items must stay above the palms and below eye line");
                for(int j=0;j<i;j++)
                {
                    layout.Position(j,out float bx,out float by,out float bz);
                    bool separated=Math.Abs(x-bx)>=.44f*layout.Scale || Math.Abs(y-by)>=.42f*layout.Scale || Math.Abs(z-bz)>=.40f*layout.Scale;
                    Check(separated,"Carried item bounds overlap");
                }
            }
        }
        Check(OpeningSequence.Sample(OpeningMode.CutThenPry,.449f,out _) == OpeningTool.BoxCutter,"Cut must precede pry");
        Check(OpeningSequence.Sample(OpeningMode.CutThenPry,.45f,out var start) == OpeningTool.PryBar && start==0,"Pry starts at stage boundary");
        foreach(OpeningMode mode in Enum.GetValues(typeof(OpeningMode)))
            for(int p=-10;p<=110;p++)
            {
                OpeningSequence.Sample(mode,p/100f,out var phase);
                Check(phase>=0 && phase<=1,"Tool phase must be normalized");
            }
        Check(OpeningSequence.Sample(OpeningMode.Hands,.7f,out _)==OpeningTool.None,"Door mode must not show a blade");
    }
    public static void Main()
    {
        CheckCarryAndTools();
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
        Console.WriteLine("PASS: "+checks+" assertions; 2000 generated scenarios; inventory, races, completion, pricing and limits.");
    }
}
