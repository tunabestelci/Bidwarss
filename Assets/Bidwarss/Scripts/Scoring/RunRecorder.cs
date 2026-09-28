using UnityEngine;
namespace Bidwarss
{
    public sealed class RunRecorder : MonoBehaviour
    {
        string recordedRun;
        float retryAt;
        void Update()
        {
            var world=WarehouseWorld.Instance;
            if(world==null||!world.IsSpawned||!world.Completed.Value||world.RunId.Value.ToString()==recordedRun||Time.unscaledTime<retryAt)return;
            var score=RunScore.FromWorld(world);
            if(ScoreHistory.Save(score))recordedRun=score.run_id;else retryAt=Time.unscaledTime+10;
        }
    }
}
