using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Bidwarss
{
    [Serializable] public sealed class RunScore
    {
        public string run_id,rules_hash,team,finished_utc;
        public int seed,total_dollars,elapsed_milliseconds,player_count,item_count;
        public static RunScore FromWorld(WarehouseWorld world) => new RunScore {
            run_id=world.RunId.Value.ToString(),rules_hash=world.RulesHash.Value.ToString(),team=world.TeamNames.Value.ToString(),
            finished_utc=DateTime.UtcNow.ToString("O"),seed=world.Seed.Value,total_dollars=world.FinalDollars.Value,
            elapsed_milliseconds=(int)Math.Min(int.MaxValue,world.Elapsed*1000),player_count=world.PeakPlayers.Value,item_count=world.Items.Count };
    }
    [Serializable] public sealed class ScoreCollection {public List<RunScore> entries=new List<RunScore>();}
    public static class ScoreHistory
    {
        static ScoreCollection data;
        public static string LastError {get;private set;}
        static string FilePath=>Path.Combine(Application.persistentDataPath,"bidwarss-results-v2.json");
        public static IReadOnlyList<RunScore> Entries {get{Load();return data.entries.AsReadOnly();}}
        static void Load()
        {
            if(data!=null)return;
            data=new ScoreCollection();
            try
            {
                if(File.Exists(FilePath))data=JsonUtility.FromJson<ScoreCollection>(File.ReadAllText(FilePath))??new ScoreCollection();
                if(data.entries==null)data.entries=new List<RunScore>();
                data.entries.RemoveAll(e=>e==null||e.total_dollars<0||string.IsNullOrEmpty(e.run_id)||string.IsNullOrEmpty(e.rules_hash)||string.IsNullOrEmpty(e.team));
            }
            catch(Exception ex){LastError="Skor geçmişi okunamadı: "+ex.Message;}
        }
        public static bool Save(RunScore score)
        {
            Load();if(data.entries.Exists(s=>s.run_id==score.run_id))return true;
            var previous = new List<RunScore>(data.entries);
            data.entries.Add(score);
            data.entries=data.entries.OrderByDescending(s=>s.total_dollars).ThenBy(s=>s.elapsed_milliseconds).Take(100).ToList();
            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                string temp=FilePath+".tmp";File.WriteAllText(temp,JsonUtility.ToJson(data,true));
                if(File.Exists(FilePath))File.Replace(temp,FilePath,FilePath+".bak");else File.Move(temp,FilePath);
                LastError=null;return true;
            }
            catch(Exception ex){data.entries=previous;LastError="Skor kaydedilemedi: "+ex.Message;return false;}
        }
    }
}
