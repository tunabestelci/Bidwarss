using System;
using System.Collections;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Bidwarss
{
    [Serializable] public sealed class LeaderboardPage {public RunScore[] entries;}
    public sealed class LeaderboardClient : MonoBehaviour
    {
        public static LeaderboardClient Instance {get;private set;}
        [Tooltip("Public HTTPS base URL. Empty means the online service is not connected.")]
        public string serviceUrl="";
        public RunScore[] Entries {get;private set;}=new RunScore[0];
        public string Status {get;private set;}="Dünya sıralaması henüz bağlı değil.";
        public bool Busy {get;private set;}
        string submittedRun;
        float retryAt;
        int submitAttempts;
        bool submitting;
        RunScore pendingScore;
        void Awake(){Instance=this;}
        void OnDestroy(){if(Instance==this)Instance=null;}
        public void Refresh(string rules,int players)
        {
            if(Busy)return;
            if(string.IsNullOrWhiteSpace(serviceUrl)){Status="Dünya sıralaması henüz bağlı değil. Yerel rekorların kaydediliyor.";return;}
            if(!ValidUrl(serviceUrl)){Status="Sıralama için HTTPS adresi gerekli.";return;}
            StartCoroutine(Fetch(rules,players));
        }
        static bool ValidUrl(string url)
        {
            if(!Uri.TryCreate(url,UriKind.Absolute,out var uri))return false;
            return uri.Scheme=="https" || (uri.Scheme=="http" && uri.IsLoopback);
        }
        IEnumerator Fetch(string rules,int players)
        {
            Busy=true;Status="Dünya sıralaması yükleniyor…";
            string url=serviceUrl.TrimEnd('/')+"/v1/leaderboard?rules_hash="+UnityWebRequest.EscapeURL(rules)+"&player_count="+Mathf.Clamp(players,1,4);
            using(var request=UnityWebRequest.Get(url))
            {
                request.timeout=15;yield return request.SendWebRequest();
                if(request.result!=UnityWebRequest.Result.Success)Status="Sıralamaya ulaşılamadı. Yerel skorlar güvende.";
                else
                {
                    try{Entries=(JsonUtility.FromJson<LeaderboardPage>(request.downloadHandler.text)?.entries??new RunScore[0]).Where(s=>s!=null&&!string.IsNullOrEmpty(s.rules_hash)&&!string.IsNullOrEmpty(s.team)).Take(100).ToArray();Status=Entries.Length==0?"Bu kategori için henüz doğrulanmış skor yok.":"Doğrulanmış sunucu skorları";}
                    catch(Exception){Status="Sıralama yanıtı okunamadı.";}
                }
            }
            Busy=false;
        }
        void Update()
        {
            var world=WarehouseWorld.Instance;
            // LAN hosts cannot upload competitive scores. Never ship signing keys in builds/assets.
            if(world==null||!world.IsServer||world.IsClient||!Application.isBatchMode||!world.Completed.Value||submitting||Time.unscaledTime<retryAt)return;
            string id=world.RunId.Value.ToString();
            if(submittedRun==id)return;
            if(pendingScore==null || pendingScore.run_id!=id){pendingScore=RunScore.FromWorld(world);submitAttempts=0;}
            string endpoint=Environment.GetEnvironmentVariable("BIDWARSS_SCORE_URL");
            string secret=Environment.GetEnvironmentVariable("BIDWARSS_SCORE_SECRET");
            if(string.IsNullOrWhiteSpace(endpoint)||!ValidUrl(endpoint)||string.IsNullOrEmpty(secret)||secret.Length<32)return;
            StartCoroutine(Submit(pendingScore,endpoint,secret));
        }
        IEnumerator Submit(RunScore score,string endpoint,string secret)
        {
            submitting=true;
            string body=JsonUtility.ToJson(score);
            string timestamp=DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
            string nonce=Guid.NewGuid().ToString("N"),signature;
            using(var hmac=new HMACSHA256(Encoding.UTF8.GetBytes(secret)))
                signature=BitConverter.ToString(hmac.ComputeHash(Encoding.UTF8.GetBytes(timestamp+"\n"+nonce+"\n"+body))).Replace("-","").ToLowerInvariant();
            using(var request=new UnityWebRequest(endpoint.TrimEnd('/')+"/v1/runs","POST"))
            {
                request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));request.downloadHandler=new DownloadHandlerBuffer();request.timeout=15;
                request.SetRequestHeader("Content-Type","application/json");request.SetRequestHeader("X-Bidwarss-Time",timestamp);
                request.SetRequestHeader("X-Bidwarss-Nonce",nonce);request.SetRequestHeader("X-Bidwarss-Signature",signature);
                yield return request.SendWebRequest();
                if(request.result==UnityWebRequest.Result.Success){submittedRun=score.run_id;submitAttempts=0;Debug.Log("Bidwarss: doğrulanmış skor sunucuya kaydedildi.");}
                else{submitAttempts++;retryAt=Time.unscaledTime+Mathf.Min(300,10*Mathf.Pow(2,Mathf.Min(5,submitAttempts)));Debug.LogWarning("Bidwarss: skor servisine gönderilemedi; yerel sonuç kaydı korunuyor.");}
            }
            submitting=false;
        }
    }
}
